namespace Tronox.Application.Forms;

/// <summary>
/// Ciclo de vida de las respuestas de formularios dinamicos (RQ08, port ECOREX / ADR-0015): borrador
/// con autosave, envio con VALIDACION SERVIDOR completa por tipo (errores por fieldCode). Primer
/// slice: sin vinculo a pasos de workflow, ni registros transaccionales, ni maestro-detalle.
/// </summary>
public interface IFormResponseService
{
    /// <summary>
    /// Borrador para (definicion, referencia): si existe uno Draft lo devuelve; si no, lo crea. Con
    /// reference null SIEMPRE crea uno nuevo (respuesta suelta). La definicion debe estar Active.
    /// </summary>
    Task<FormResult<FormResponseDto>> GetOrCreateDraftAsync(long definitionId, string? reference, long actorUserId, CancellationToken cancellationToken = default);

    Task<FormResponseDto?> GetAsync(long responseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda el documento de datos. Con submit=false (autosave) solo persiste; con submit=true valida
    /// TODO por tipo y devuelve ValidationFailed con errores por fieldCode si algo falla.
    /// </summary>
    Task<FormResult<FormResponseDto>> SaveAsync(
        long responseId, IReadOnlyDictionary<string, FormFieldValue> data, bool submit,
        long actorUserId, IReadOnlyCollection<string>? hiddenFieldCodes = null,
        CancellationToken cancellationToken = default);

    /// <summary>Respuestas enviadas de una definicion (bandeja de respuestas).</summary>
    Task<IReadOnlyList<FormResponseListItemDto>> ListResponsesAsync(long definitionId, CancellationToken cancellationToken = default);
}
