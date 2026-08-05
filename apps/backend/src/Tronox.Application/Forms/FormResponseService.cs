using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Forms;

/// <summary>
/// Ciclo de vida de las respuestas de formularios dinamicos (RQ08, port ECOREX / ADR-0015). El
/// documento de datos se guarda como JSON { fieldCode: { value, type } }. Al enviar (submit) se
/// re-valida TODO por tipo en el servidor (fuente de verdad); los errores viajan por fieldCode.
/// </summary>
public sealed class FormResponseService : IFormResponseService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public FormResponseService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<FormResult<FormResponseDto>> GetOrCreateDraftAsync(long definitionId, string? reference, long actorUserId, CancellationToken cancellationToken = default)
    {
        var def = await _db.FormDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (def is null) { return FormResult<FormResponseDto>.NotFound("El formulario no existe."); }
        if (def.Status != FormStatus.Active) { return FormResult<FormResponseDto>.Invalid("El formulario no esta publicado."); }

        FormResponse? resp = null;
        if (!string.IsNullOrWhiteSpace(reference))
        {
            resp = await _db.FormResponses.FirstOrDefaultAsync(
                r => r.DefinitionId == definitionId && r.Reference == reference && r.Status == FormResponseStatus.Draft, cancellationToken);
        }
        if (resp is null)
        {
            resp = new FormResponse
            {
                TenantId = _tenantContext.TenantId!.Value, DefinitionId = definitionId,
                Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
                Status = FormResponseStatus.Draft, Data = "{}"
            };
            _db.FormResponses.Add(resp);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return FormResult<FormResponseDto>.Ok(ToDto(resp));
    }

    public async Task<FormResponseDto?> GetAsync(long responseId, CancellationToken cancellationToken = default)
    {
        var r = await _db.FormResponses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == responseId, cancellationToken);
        return r is null ? null : ToDto(r);
    }

    public async Task<FormResult<FormResponseDto>> SaveAsync(
        long responseId, IReadOnlyDictionary<string, FormFieldValue> data, bool submit,
        long actorUserId, IReadOnlyCollection<string>? hiddenFieldCodes = null, CancellationToken cancellationToken = default)
    {
        var r = await _db.FormResponses.FirstOrDefaultAsync(x => x.Id == responseId, cancellationToken);
        if (r is null) { return FormResult<FormResponseDto>.NotFound("La respuesta no existe."); }
        if (r.Status == FormResponseStatus.Submitted) { return FormResult<FormResponseDto>.Invalid("La respuesta ya fue enviada."); }

        if (submit)
        {
            var errors = await ValidateAllAsync(r.DefinitionId, data, hiddenFieldCodes, cancellationToken);
            if (errors.Count > 0) { return FormResult<FormResponseDto>.ValidationFailed(errors); }
        }

        r.Data = JsonSerializer.Serialize(data, JsonOptions);
        if (submit)
        {
            r.Status = FormResponseStatus.Submitted;
            r.SubmittedAt = DateTimeOffset.UtcNow;
            r.SubmittedByTenantUserId = actorUserId;
            _audit.Write(actorUserId, "form.responder", nameof(FormResponse), r, null, new { r.DefinitionId, r.Reference }, r.TenantId);
        }
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return FormResult<FormResponseDto>.Conflict("La respuesta fue modificada. Recarga."); }
        return FormResult<FormResponseDto>.Ok(ToDto(r));
    }

    public async Task<IReadOnlyList<FormResponseListItemDto>> ListResponsesAsync(long definitionId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.FormResponses.AsNoTracking()
            .Where(r => r.DefinitionId == definitionId && r.Status == FormResponseStatus.Submitted)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(r => new FormResponseListItemDto(
            r.Id, r.Reference, r.Status, r.SubmittedAt, r.CreatedAt, FlattenData(r.Data))).ToList();
    }

    // ---- Helpers ----

    private async Task<Dictionary<string, string>> ValidateAllAsync(
        long definitionId, IReadOnlyDictionary<string, FormFieldValue> data,
        IReadOnlyCollection<string>? hiddenFieldCodes, CancellationToken cancellationToken)
    {
        var hidden = hiddenFieldCodes is null ? new HashSet<string>() : new HashSet<string>(hiddenFieldCodes, StringComparer.Ordinal);
        var questions = await _db.FormQuestions.AsNoTracking()
            .Where(q => q.DefinitionId == definitionId).ToListAsync(cancellationToken);
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var q in questions)
        {
            if (q.IsHidden || hidden.Contains(q.FieldCode)) { continue; }
            data.TryGetValue(q.FieldCode, out var fv);
            var options = FormFieldValidator.ParseOptions(q.OptionsJson);
            var rules = FormFieldValidator.ParseRules(q.ValidationJson);
            var err = FormFieldValidator.Validate(q.ControlType, q.Required, fv?.Value, options, rules);
            if (err is not null) { errors[q.FieldCode] = err; }
        }
        return errors;
    }

    private static FormResponseDto ToDto(FormResponse r)
        => new(r.Id, r.DefinitionId, r.Reference, r.Status, ParseData(r.Data),
            r.SubmittedAt, r.SubmittedByTenantUserId, r.Version);

    private static IReadOnlyDictionary<string, FormFieldValue> ParseData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return new Dictionary<string, FormFieldValue>(); }
        try { return JsonSerializer.Deserialize<Dictionary<string, FormFieldValue>>(json, JsonOptions) ?? new(); }
        catch (JsonException) { return new Dictionary<string, FormFieldValue>(); }
    }

    private static IReadOnlyDictionary<string, string?> FlattenData(string? json)
        => ParseData(json).ToDictionary(kv => kv.Key, kv => kv.Value.Value);
}
