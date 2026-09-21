namespace Tronox.Application.Radicacion;

/// <summary>Item de la lista de correos por revisar.</summary>
public sealed record CorreoItemDto(
    long Reg, string Nombre, string? Email, string? Asunto, string Hora,
    int Adjuntos, string? TipoNombre, string? TipoColor, int Confianza, string Modo,
    int? Segundos, string? DuplicadoNumero, string? RadicadoRef);

public sealed record CorreosListaDto(IReadOnlyList<CorreoItemDto> Lista, int Pendientes, int Descartados);

/// <summary>Detalle de un correo (panel de lectura).</summary>
public sealed record CorreoDetalleDto(
    long Reg, string? Buzon, string Nombre, string? Email, string? Asunto, string? Cuerpo,
    string Recibido, long? TipoDetectadoId, string? TipoNombre, string? TipoColor, int Confianza,
    string Modo, string Estado, string? DuplicadoNumero, string? RadicadoRef, string? RadicadoNumero,
    string? Causal, IReadOnlyList<CorreoAdjuntoDto> Adjuntos);

public sealed record CorreoAdjuntoDto(long Reg, string Nombre, long Kb, string? Ext, bool EsCuerpoHtml, bool EsHilo);

public sealed record EditarCorreoRequest(long Reg, long? TipoDetectadoId, string? Asunto, string? Descripcion, string? Nombre, string? Email);

public sealed record CorreoResult(bool Ok, string? Error = null, string? Numero = null)
{
    public static CorreoResult Fail(string error) => new(false, error);
    public static CorreoResult Success(string? numero = null) => new(true, null, numero);
}
