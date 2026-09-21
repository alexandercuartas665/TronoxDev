using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Forms;

/// <summary>
/// Implementacion de IFormResponseService (RQ08, port ECOREX / ADR-0015). El documento de datos se
/// serializa como { fieldCode: { value, type } } (claves = FieldCode literal). El submit re-valida
/// TODO en servidor con FormFieldValidator; recomputa tablas (FormGridCalculator) y campos calculados
/// (FormExpressionEvaluator) DESCARTANDO los valores que mande el cliente para esos campos; y al
/// confirmar un formulario transaccional resuelve la identidad (clave natural / consecutivo).
///
/// Adaptaciones TRONOX: Guid->long. DIFERIDO: completar el paso de flujo BPMN al enviar (RQ08 x RQ11)
/// y la auto-resolucion de columnas por lookup (IFormLookupService sin fuentes todavia, RQ07); la
/// bandeja en vivo (broadcaster) tambien queda para mas adelante.
/// </summary>
public sealed class FormResponseService : IFormResponseService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string ConflictMessage = "Otro usuario modifico la respuesta. Recarga e intenta de nuevo.";

    private readonly IApplicationDbContext _db;
    private readonly Tenancy.ISequenceService _sequences;
    private readonly ITenantContext _tenant;
    // Fuentes de lookup del tenant (RQ08, ola F1). Hoy sin fuentes registradas (Tercero -> RQ07; Item /
    // DataContainer son modulos de ECOREX podados): degrada a vacio. Se inyecta para el autollenado y la
    // auto-resolucion de columnas cuando existan las fuentes.
    private readonly Lookups.IFormLookupService _lookup;

    public FormResponseService(
        IApplicationDbContext db, Tenancy.ISequenceService sequences,
        ITenantContext tenant, Lookups.IFormLookupService lookup)
    {
        _db = db;
        _sequences = sequences;
        _tenant = tenant;
        _lookup = lookup;
    }

    public async Task<FormResult<FormResponseDto>> GetOrCreateDraftAsync(long definitionId, string? reference, CancellationToken cancellationToken = default)
    {
        var definition = await _db.FormDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (definition is null)
        {
            return FormResult<FormResponseDto>.NotFound("Formulario no encontrado.");
        }
        if (definition.Status != FormStatus.Active || definition.IsArchived)
        {
            return FormResult<FormResponseDto>.Invalid("El formulario no esta activo.");
        }

        var normalizedReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (normalizedReference is not null)
        {
            var existing = await _db.FormResponses.AsNoTracking()
                .Where(r => r.DefinitionId == definitionId
                    && r.Reference == normalizedReference
                    && r.Status == FormResponseStatus.Draft)
                .OrderBy(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                return FormResult<FormResponseDto>.Ok(ToDto(existing));
            }
        }

        var response = new FormResponse
        {
            TenantId = definition.TenantId,
            DefinitionId = definitionId,
            Reference = normalizedReference,
            Status = FormResponseStatus.Draft,
            Data = "{}"
        };
        _db.FormResponses.Add(response);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormResponseDto>.Ok(ToDto(response));
    }

    public async Task<FormResponseDto?> GetAsync(long responseId, CancellationToken cancellationToken = default)
    {
        var response = await _db.FormResponses.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == responseId, cancellationToken);
        return response is null ? null : ToDto(response);
    }

    public async Task<FormResult<FormResponseDto>> SetReferenceAsync(
        long responseId, string reference, CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (normalized is null)
        {
            return FormResult<FormResponseDto>.Invalid("La referencia no puede estar vacia.");
        }

        var response = await _db.FormResponses.FirstOrDefaultAsync(r => r.Id == responseId, cancellationToken);
        if (response is null)
        {
            return FormResult<FormResponseDto>.NotFound("Respuesta no encontrada.");
        }

        // No destructivo: si ya quedo anclada, se respeta.
        if (string.IsNullOrWhiteSpace(response.Reference))
        {
            response.Reference = normalized;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return FormResult<FormResponseDto>.Ok(ToDto(response));
    }

    public async Task<FormResult<FormResponseDto>> SaveAsync(
        long responseId, IReadOnlyDictionary<string, FormFieldValue> data, bool submit,
        long? submittedByTenantUserId = null,
        IReadOnlyCollection<string>? hiddenFieldCodes = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _db.FormResponses.FirstOrDefaultAsync(r => r.Id == responseId, cancellationToken);
        if (response is null)
        {
            return FormResult<FormResponseDto>.NotFound("Respuesta no encontrada.");
        }
        if (response.Status == FormResponseStatus.Submitted)
        {
            return FormResult<FormResponseDto>.Invalid("La respuesta ya fue enviada y no puede modificarse.");
        }

        var questions = await _db.FormQuestions.AsNoTracking()
            .Where(q => q.DefinitionId == response.DefinitionId)
            .OrderBy(q => q.SortOrder)
            .ToListAsync(cancellationToken);
        var questionsByCode = questions.ToDictionary(q => q.FieldCode, StringComparer.Ordinal);

        // Solo se persisten claves que existen en la definicion (documento canonico).
        var document = new Dictionary<string, FormFieldValue>(StringComparer.Ordinal);
        foreach (var (fieldCode, value) in data)
        {
            if (questionsByCode.TryGetValue(fieldCode, out var question)
                && !FormFieldValidator.IsNonInput(question.ControlType))
            {
                document[fieldCode] = new FormFieldValue(value.Value, question.ControlType.ToString());
            }
        }

        // Tablas en SERVIDOR (ola F2, doc 01 D5): formula por fila + roll-up de columnas al encabezado,
        // con el helper compartido con el renderer. Los valores del ENCABEZADO son visibles para las
        // formulas de columna via {#campo}. Persiste las filas computadas.
        var headerValues = document.ToDictionary(kv => kv.Key, kv => kv.Value.Value, StringComparer.Ordinal);
        foreach (var question in questions.Where(q => q.ControlType == FormControlType.GridDetail))
        {
            var cols = Calc.FormGridCalculator.ParseColumns(question.OptionsJson);
            if (cols.Count == 0) { continue; }
            document.TryGetValue(question.FieldCode, out var gridField);
            var gridRows = FormFieldValidator.ParseGridRows(gridField?.Value)
                .Select(r => new Dictionary<string, string?>(r, StringComparer.Ordinal)).ToList();
            // TODO(RQ07): auto-resolucion multi-clave (VLOOKUP) AUTORITATIVA de las columnas 'resolve' via
            // _lookup.MatchAsync antes del calculo. DIFERIDA: TRONOX aun no tiene MatchAsync ni el parser de
            // columnas de lookup (fuentes de datos por construir). Sin ella el calculo usa las celdas tal cual.
            var (computed, rollups) = Calc.FormGridCalculator.Recompute(gridRows, cols, headerValues);
            document[question.FieldCode] = new FormFieldValue(
                computed.Count == 0 ? null : JsonSerializer.Serialize(computed, JsonOptions),
                question.ControlType.ToString());
            foreach (var (field, total) in rollups)
            {
                var type = questionsByCode.TryGetValue(field, out var tq) ? tq.ControlType.ToString() : FormControlType.Text.ToString();
                document[field] = new FormFieldValue(total, type);
                // Un roll-up ya es encabezado: queda visible para las tablas que se calculen despues.
                headerValues[field] = total;
            }
        }

        // Calculo en SERVIDOR (ola F2, doc 01 D5): recomputa los campos con CalcExpression con el MISMO
        // evaluador tipado del cliente. El cliente NO es fuente de verdad para montos: su valor se
        // descarta y se persiste el del servidor.
        var calcValues = document.ToDictionary(kv => kv.Key, kv => kv.Value.Value, StringComparer.Ordinal);
        foreach (var question in questions.Where(q => !string.IsNullOrWhiteSpace(q.CalcExpression)))
        {
            var computed = Calc.FormExpressionEvaluator.Evaluate(question.CalcExpression, calcValues)
                ?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            document[question.FieldCode] = new FormFieldValue(computed, question.ControlType.ToString());
            calcValues[question.FieldCode] = computed;
        }

        if (submit)
        {
            // VALIDACION SERVIDOR completa por tipo, con errores por fieldCode.
            var errors = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var question in questions)
            {
                // Campos ocultos por el disenador: no se pintan, no se validan.
                if (question.IsHidden)
                {
                    continue;
                }
                // Campos ocultos por CONDICION en runtime (D4): el renderer manda que campos quedaron
                // ocultos; no se exigen (p.ej. "Valor" cuando "Concreto una venta? = No").
                if (hiddenFieldCodes is not null && hiddenFieldCodes.Contains(question.FieldCode))
                {
                    continue;
                }
                if (FormFieldValidator.IsNonInput(question.ControlType))
                {
                    continue;
                }
                document.TryGetValue(question.FieldCode, out var field);
                var error = FormFieldValidator.Validate(
                    question.ControlType, question.Required, field?.Value,
                    FormFieldValidator.ParseOptions(question.OptionsJson),
                    FormFieldValidator.ParseRules(question.ValidationJson),
                    question.OptionsJson);
                if (error is not null)
                {
                    errors[question.FieldCode] = error;
                }
            }
            if (errors.Count > 0)
            {
                return FormResult<FormResponseDto>.ValidationFailed(errors);
            }
        }

        // Registro transaccional (ola F3, doc 01 D2/D3): confirmar = enviar. La identidad se resuelve
        // ANTES de abrir la transaccion (patron de ISequenceService: EnsureSequence + Next fuera de la tx
        // del caso de uso). Idempotente: si el registro ya esta Confirmed no reasigna.
        string? recordNumber = null;
        var assignRecord = false;
        if (submit)
        {
            var definition = await _db.FormDefinitions
                .FirstOrDefaultAsync(d => d.Id == response.DefinitionId, cancellationToken);
            if (definition?.IsTransactional == true && response.RecordStatus != FormRecordStatus.Confirmed)
            {
                var identity = await ResolveIdentityAsync(definition, document, cancellationToken);
                if (!identity.Ok)
                {
                    return FormResult<FormResponseDto>.ValidationFailed(
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            [definition.IdentitySourceFieldCode ?? "_identidad"] = identity.Error!
                        });
                }
                recordNumber = identity.Number;
                assignRecord = true;
            }
        }

        await using var transaction = await BeginTransactionIfNoneAsync(cancellationToken);

        response.Data = JsonSerializer.Serialize(document, JsonOptions);
        if (submit)
        {
            response.Status = FormResponseStatus.Submitted;
            response.SubmittedAt = DateTimeOffset.UtcNow;
            response.SubmittedByTenantUserId = submittedByTenantUserId;

            // Registro transaccional (ola F3): identidad ya resuelta antes de la transaccion.
            if (assignRecord)
            {
                response.RecordNumber = recordNumber;
                response.RecordStatus = FormRecordStatus.Confirmed;
                response.TransactionDate = DateTimeOffset.UtcNow;
            }

            // TODO(RQ08xRQ11): completar el paso de flujo al enviar (binding diferido). Aqui ECOREX cerraba
            // cada FormFlowLink Pending y completaba el paso via IWorkflowEngine dentro de esta transaccion.
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FormResult<FormResponseDto>.Conflict(ConflictMessage);
        }
        catch (DbUpdateException) when (submit)
        {
            // Choca el indice unico de record_number (clave natural duplicada por tenant+definicion).
            return FormResult<FormResponseDto>.ValidationFailed(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["_identidad"] = "Ya existe un registro con esa clave (numero duplicado)."
                });
        }
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        // TODO(RQ08): avisar a la bandeja en vivo /m/{code} tras confirmar un registro (broadcaster diferido).

        return FormResult<FormResponseDto>.Ok(ToDto(response));
    }

    /// <summary>
    /// Resuelve la identidad de un registro transaccional al confirmar (ola F3, doc 01 D3): consecutivo
    /// (una TenantSequence por formulario, prefijo = codigo del form) o clave natural (valor de un campo,
    /// unicidad garantizada por indice). None = sin numero.
    /// </summary>
    private async Task<(bool Ok, string? Number, string? Error)> ResolveIdentityAsync(
        FormDefinition definition, IReadOnlyDictionary<string, FormFieldValue> document, CancellationToken cancellationToken)
    {
        switch (definition.IdentityMode)
        {
            case FormIdentityMode.None:
                return (true, null, null);

            case FormIdentityMode.NaturalKey:
                if (string.IsNullOrWhiteSpace(definition.IdentitySourceFieldCode))
                {
                    return (false, null, "El formulario no tiene campo de identidad configurado.");
                }
                document.TryGetValue(definition.IdentitySourceFieldCode, out var keyField);
                if (string.IsNullOrWhiteSpace(keyField?.Value))
                {
                    return (false, null, "El campo de identidad es obligatorio para confirmar.");
                }
                return (true, keyField!.Value, null);

            case FormIdentityMode.Sequence:
                // Una secuencia por formulario (doc 03 B): un codigo corto derivado del id ("FD"+id, unico
                // por tenant); el prefijo legible del numero es el codigo del formulario (ej. FRM-021-000001).
                var code = "FD" + definition.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await _sequences.EnsureSequenceAsync(code, cancellationToken);
                var number = await _sequences.NextAsync(code, $"{definition.Code}-", 6, cancellationToken);
                return (true, number, null);

            default:
                return (true, null, null);
        }
    }

    public async Task<IReadOnlyList<FormRecordListItemDto>> ListRecordsAsync(long definitionId, CancellationToken cancellationToken = default)
    {
        // Bandeja del formulario-modulo (ola F4): los registros enviados (no borradores), recientes primero.
        var rows = await _db.FormResponses.AsNoTracking()
            .Where(r => r.DefinitionId == definitionId && r.Status == FormResponseStatus.Submitted)
            .OrderByDescending(r => r.TransactionDate ?? r.SubmittedAt)
            .Select(r => new { r.Id, r.RecordNumber, r.RecordStatus, r.TransactionDate, r.SubmittedAt, r.Reference, r.Data })
            .ToListAsync(cancellationToken);

        return rows.Select(r =>
        {
            var fields = ParseDocument(r.Data).ToDictionary(kv => kv.Key, kv => kv.Value.Value, StringComparer.Ordinal);
            return new FormRecordListItemDto(
                r.Id, r.RecordNumber, r.RecordStatus, r.TransactionDate, r.SubmittedAt, r.Reference,
                fields);
        }).ToList();
    }

    public async Task<byte[]?> ExportRecordsXlsxAsync(long definitionId, CancellationToken cancellationToken = default)
    {
        var definition = await _db.FormDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == definitionId && d.IsModule, cancellationToken);
        if (definition is null) { return null; }

        // Columnas de datos configuradas (field codes) + su etiqueta desde las preguntas.
        var columns = ParseCodeList(definition.ListColumnsJson);
        var labels = await _db.FormQuestions.AsNoTracking()
            .Where(q => q.DefinitionId == definitionId)
            .ToDictionaryAsync(q => q.FieldCode, q => q.Label, StringComparer.Ordinal, cancellationToken);

        var records = await ListRecordsAsync(definitionId, cancellationToken);

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add(definition.Code);
        // Encabezado: metadatos fijos + columnas de datos (vista aplanada para BI).
        var headers = new List<string> { "Numero", "Fecha", "Estado", "Referencia" };
        headers.AddRange(columns.Select(c => labels.TryGetValue(c, out var l) ? l : c));
        for (var i = 0; i < headers.Count; i++) { ws.Cell(1, i + 1).Value = headers[i]; }
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var r in records)
        {
            ws.Cell(row, 1).Value = r.RecordNumber ?? "";
            ws.Cell(row, 2).Value = (r.TransactionDate ?? r.SubmittedAt)?.ToString("yyyy-MM-dd HH:mm") ?? "";
            ws.Cell(row, 3).Value = r.RecordStatus.ToString();
            ws.Cell(row, 4).Value = r.Reference ?? "";
            for (var c = 0; c < columns.Count; c++)
            {
                ws.Cell(row, 5 + c).Value = r.Fields.TryGetValue(columns[c], out var v) ? v ?? "" : "";
            }
            row++;
        }
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ---- Maestro-detalle (ola F5, doc 01 D7) ----

    public async Task<IReadOnlyList<FormRecordListItemDto>> ListChildrenAsync(
        long parentResponseId, string parentFieldCode, CancellationToken cancellationToken = default)
    {
        var children = await _db.FormRecordLinks.AsNoTracking()
            .Where(l => l.ParentResponseId == parentResponseId && l.ParentFieldCode == parentFieldCode)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.CreatedAt)
            .Join(_db.FormResponses.AsNoTracking(), l => l.ChildResponseId, r => r.Id, (l, r) => r)
            .ToListAsync(cancellationToken);

        return children.Select(r =>
        {
            var fields = ParseDocument(r.Data).ToDictionary(kv => kv.Key, kv => kv.Value.Value, StringComparer.Ordinal);
            return new FormRecordListItemDto(r.Id, r.RecordNumber, r.RecordStatus, r.TransactionDate, r.SubmittedAt, r.Reference, fields);
        }).ToList();
    }

    public async Task<FormResult<long>> AddChildAsync(
        long parentResponseId, string parentFieldCode, long childDefinitionId, CancellationToken cancellationToken = default)
    {
        if (_tenant.TenantId is not long tenantId)
        {
            return FormResult<long>.Invalid("No hay tenant activo.");
        }
        var parentExists = await _db.FormResponses.AsNoTracking().AnyAsync(r => r.Id == parentResponseId, cancellationToken);
        if (!parentExists) { return FormResult<long>.NotFound("Registro padre no encontrado."); }
        var childDefExists = await _db.FormDefinitions.AsNoTracking().AnyAsync(d => d.Id == childDefinitionId, cancellationToken);
        if (!childDefExists) { return FormResult<long>.NotFound("Definicion hija no encontrada."); }

        var child = new FormResponse { TenantId = tenantId, DefinitionId = childDefinitionId, Data = "{}" };
        _db.FormResponses.Add(child);
        var order = await _db.FormRecordLinks
            .Where(l => l.ParentResponseId == parentResponseId && l.ParentFieldCode == parentFieldCode)
            .CountAsync(cancellationToken);
        _db.FormRecordLinks.Add(new FormRecordLink
        {
            TenantId = tenantId,
            ParentResponseId = parentResponseId,
            ParentFieldCode = parentFieldCode,
            ChildResponse = child,
            SortOrder = order,
        });
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<long>.Ok(child.Id);
    }

    public async Task<FormResult<bool>> UnlinkChildAsync(
        long parentResponseId, string parentFieldCode, long childResponseId, CancellationToken cancellationToken = default)
    {
        var link = await _db.FormRecordLinks
            .FirstOrDefaultAsync(l => l.ParentResponseId == parentResponseId
                && l.ParentFieldCode == parentFieldCode && l.ChildResponseId == childResponseId, cancellationToken);
        if (link is null) { return FormResult<bool>.NotFound("Enlace no encontrado."); }
        _db.FormRecordLinks.Remove(link);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    /// <summary>Deserializa un arreglo JSON de field codes (columnas/filtros de la bandeja). Vacio si invalido.</summary>
    private static IReadOnlyList<string> ParseCodeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return Array.Empty<string>(); }
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    /// <summary>
    /// Anula un registro transaccional confirmado (ola F3, doc 01 D2): RecordStatus=Voided + motivo +
    /// auditoria. NO borra ni libera el numero (queda el hueco, trazable). Idempotente.
    /// </summary>
    public async Task<FormResult<FormResponseDto>> VoidAsync(
        long responseId, string reason, long? byTenantUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _db.FormResponses.FirstOrDefaultAsync(r => r.Id == responseId, cancellationToken);
        if (response is null)
        {
            return FormResult<FormResponseDto>.NotFound("Respuesta no encontrada.");
        }
        if (response.RecordStatus != FormRecordStatus.Confirmed)
        {
            return FormResult<FormResponseDto>.Invalid("Solo se puede anular un registro confirmado.");
        }
        response.RecordStatus = FormRecordStatus.Voided;
        response.VoidedAt = DateTimeOffset.UtcNow;
        response.VoidedByTenantUserId = byTenantUserId;
        response.VoidReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FormResult<FormResponseDto>.Conflict(ConflictMessage);
        }
        return FormResult<FormResponseDto>.Ok(ToDto(response));
    }

    public async Task<FormResult<bool>> DeleteRecordAsync(long responseId, CancellationToken cancellationToken = default)
    {
        var response = await _db.FormResponses.FirstOrDefaultAsync(r => r.Id == responseId, cancellationToken);
        if (response is null)
        {
            return FormResult<bool>.NotFound("Registro no encontrado.");
        }

        var tx = _db.HasActiveTransaction ? null : await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            // Enlaces maestro-detalle (FormRecordLink) apuntan al registro con FK Restrict: la BD no deja
            // borrar el registro mientras existan, asi que se retiran primero. El registro puede ser padre
            // o hijo de otro.
            var enlaces = await _db.FormRecordLinks
                .Where(l => l.ParentResponseId == responseId || l.ChildResponseId == responseId)
                .ToListAsync(cancellationToken);
            if (enlaces.Count > 0) { _db.FormRecordLinks.RemoveRange(enlaces); }

            // TODO(RQ07): desligar notas de tercero que citen este registro cuando exista el Catalogo de
            // Terceros (en ECOREX aqui se ponia TerceroNota.FormResponseId = null antes de borrar).

            // FormFlowLink cae por cascada de BD. El registro se borra de verdad y su numero se libera.
            _db.FormResponses.Remove(response);
            await _db.SaveChangesAsync(cancellationToken);

            if (tx is not null) { await tx.CommitAsync(cancellationToken); }
            return FormResult<bool>.Ok(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (tx is not null) { await tx.RollbackAsync(cancellationToken); }
            return FormResult<bool>.Conflict(ConflictMessage);
        }
        catch
        {
            if (tx is not null) { await tx.RollbackAsync(cancellationToken); }
            throw;
        }
        finally
        {
            if (tx is not null) { await tx.DisposeAsync(); }
        }
    }

    // ---- Helpers ----

    private static FormResponseDto ToDto(FormResponse response)
        => new(response.Id, response.DefinitionId, response.Reference, response.Status,
            ParseDocument(response.Data), response.SubmittedAt, response.SubmittedByTenantUserId,
            response.Version,
            response.RecordNumber, response.RecordStatus, response.TransactionDate);

    /// <summary>Deserializa el documento { fieldCode: { value, type } }; vacio si es invalido.</summary>
    public static IReadOnlyDictionary<string, FormFieldValue> ParseDocument(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return new Dictionary<string, FormFieldValue>(StringComparer.Ordinal);
        }
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, FormFieldValue>>(data, JsonOptions)
                ?? new Dictionary<string, FormFieldValue>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, FormFieldValue>(StringComparer.Ordinal);
        }
    }

    /// <summary>Se une a la transaccion del llamador si ya hay una abierta (null = unida).</summary>
    private async Task<IDbContextTransaction?> BeginTransactionIfNoneAsync(CancellationToken cancellationToken)
        => _db.HasActiveTransaction ? null : await _db.BeginTransactionAsync(cancellationToken);
}
