namespace Tronox.Application.Forms;

/// <summary>
/// Ciclo de vida de las respuestas de formularios dinamicos (RQ08, port ECOREX / ADR-0015): borrador
/// con autosave, envio con VALIDACION SERVIDOR completa por tipo (errores por fieldCode), registro
/// transaccional al confirmar (identidad por clave natural o consecutivo) y maestro-detalle (subform).
///
/// Port fiel de ECOREX con Guid->long. DIFERIDO en TRONOX: el completado del paso de flujo BPMN al
/// enviar (binding RQ08 x RQ11) y la auto-resolucion de columnas por lookup (fuentes de RQ07).
/// </summary>
public interface IFormResponseService
{
    /// <summary>
    /// Borrador para (definicion, referencia): si existe uno Draft lo devuelve; si no, lo crea. Con
    /// reference null SIEMPRE crea un borrador nuevo (respuesta suelta). La definicion debe estar Active.
    /// </summary>
    Task<FormResult<FormResponseDto>> GetOrCreateDraftAsync(long definitionId, string? reference, CancellationToken cancellationToken = default);

    Task<FormResponseDto?> GetAsync(long responseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ancla una respuesta YA ENVIADA a una referencia (arranque FORM-FIRST): el usuario llena el
    /// formulario, el servidor lo valida y SOLO entonces nace el caso; recien ahi hay numero que anclar.
    /// Idempotente y no destructivo: si la respuesta ya tiene referencia, no la pisa.
    /// </summary>
    Task<FormResult<FormResponseDto>> SetReferenceAsync(long responseId, string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda el documento de datos. Con submit=false (autosave) solo persiste; con submit=true valida
    /// TODO por tipo (required, min/max length, pattern, rango numerico, opcion valida, fecha valida) y
    /// devuelve ValidationFailed con errores por fieldCode si algo falla. <paramref name="hiddenFieldCodes"/>
    /// son los campos ocultos por condiciones en runtime (el cliente los envia): no se exige requerido en
    /// ellos. Al confirmar un formulario transaccional resuelve la identidad (clave natural / consecutivo).
    /// </summary>
    Task<FormResult<FormResponseDto>> SaveAsync(
        long responseId, IReadOnlyDictionary<string, FormFieldValue> data, bool submit,
        long? submittedByTenantUserId = null,
        IReadOnlyCollection<string>? hiddenFieldCodes = null,
        CancellationToken cancellationToken = default);

    /// <summary>Anula un registro transaccional confirmado (ola F3): Voided + motivo + auditoria; no libera el numero.</summary>
    Task<FormResult<FormResponseDto>> VoidAsync(long responseId, string reason, long? byTenantUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// BORRA de verdad un registro (a diferencia de <see cref="VoidAsync"/>, que solo lo anula). Uso:
    /// quitar registros de prueba o cargados por error. Limpia en una transaccion los enlaces
    /// maestro-detalle que lo referencian y libera su numero. Irreversible: la UI debe confirmar.
    /// </summary>
    Task<FormResult<bool>> DeleteRecordAsync(long responseId, CancellationToken cancellationToken = default);

    /// <summary>Registros (respuestas enviadas) de una definicion, para la bandeja del formulario-modulo (ola F4).</summary>
    Task<IReadOnlyList<FormRecordListItemDto>> ListRecordsAsync(long definitionId, CancellationToken cancellationToken = default);

    /// <summary>Exporta los registros de la bandeja a Excel (.xlsx) con las columnas configuradas (ola F4). Null si no es modulo.</summary>
    Task<byte[]?> ExportRecordsXlsxAsync(long definitionId, CancellationToken cancellationToken = default);

    // ---- Maestro-detalle (ola F5, doc 01 D7) ----

    /// <summary>Registros hijos enlazados a un campo Subform del padre.</summary>
    Task<IReadOnlyList<FormRecordListItemDto>> ListChildrenAsync(long parentResponseId, string parentFieldCode, CancellationToken cancellationToken = default);

    /// <summary>Crea un registro hijo (borrador) de la definicion dada y lo enlaza al padre. Devuelve el id del hijo.</summary>
    Task<FormResult<long>> AddChildAsync(long parentResponseId, string parentFieldCode, long childDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>Quita el enlace de un hijo (el registro hijo se conserva, se desengancha del padre).</summary>
    Task<FormResult<bool>> UnlinkChildAsync(long parentResponseId, string parentFieldCode, long childResponseId, CancellationToken cancellationToken = default);
}
