namespace Tronox.Application.Radicacion;

/// <summary>Fila de la bandeja de tramites (una tarea de distribucion).</summary>
public sealed record TareaItemDto(
    long Tarea, long Reg, string Numero, string? TipoNombre, string? TipoColor, string Remitente,
    string? Asunto, string? DepNombre, string? Funcionario, string Prioridad, int? Dias, string RadEstado,
    string Recibida, string Estado, string? Instrucciones);

public sealed record TramitesContadores(int Asig, int Acep, int Prox, int Venc);

public sealed record TramitesResultDto(IReadOnlyList<TareaItemDto> Lista, TramitesContadores Contadores);

/// <summary>Filtro de la bandeja de tramites.</summary>
public sealed record TramitesFiltro(string Tab = "asignadas", string? Q = null, long? DependenciaId = null, string? Prioridad = null);

public sealed record TareaResult(bool Ok, string? Error = null)
{
    public static TareaResult Fail(string e) => new(false, e);
    public static TareaResult Success() => new(true);
}
