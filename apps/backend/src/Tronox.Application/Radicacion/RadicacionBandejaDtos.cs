using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

// ===================== Bandeja (rad_bandeja) =====================

/// <summary>Filtro de la bandeja (todos server-side, fiel a BuildWhere del legacy).</summary>
public sealed record BandejaFiltro(
    string Tab = "todos",
    string? Buscar = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    RadicadoTipo? Direccion = null,
    long? TipoComunicacionId = null,
    RadicadoEstado? Estado = null,
    RadicadoCanal? Canal = null,
    long? DependenciaId = null,
    string? Sla = null,          // VIGENTE / PROXIMO / VENCIDO
    string? Funcionario = null,
    int Top = 50);

/// <summary>Fila de la grilla de radicados.</summary>
public sealed record BandejaItemDto(
    long Reg, string Numero, RadicadoTipo Tipo, string Fecha,
    string? TipoNombre, string? TipoColor, string Canal, string Remitente, string? Asunto,
    string DependenciaNombre, string? FuncionarioNombre, string Estado, int? Dias,
    bool EsPqrsd, bool EsTutela, string? RespondeA, int NumSalidas);

public sealed record BandejaResultDto(IReadOnlyList<BandejaItemDto> Items, int Total);

/// <summary>Contadores de los 6 tabs (RF11-3).</summary>
public sealed record BandejaContadoresDto(int Todos, int Pqrsd, int Tutelas, int SinDistribuir, int Prox, int Venc);

// ===================== Detalle (rad_detalle) =====================

public sealed record RadicadoDetalleDto(
    RadicadoInfoDto Info,
    IReadOnlyList<RadicadoDocDto> Docs,
    IReadOnlyList<RadicadoTrazaDto> Traza,
    IReadOnlyList<RadicadoTareaDto> Tareas,
    IReadOnlyList<RadicadoComDto> Comunicaciones,
    int TareasActivas,
    RadicadoVinculoDto? Padre,
    IReadOnlyList<RadicadoSalidaDto> Salidas);

public sealed record RadicadoInfoDto(
    long Reg, string Numero, RadicadoTipo Tipo, string? TipoNombre, string? TipoColor,
    bool EsPqrsd, bool EsTutela, string Estado, string Canal, string? Asunto, string? Descripcion,
    string Fecha, bool Anonimo, string? Remitente, string? RemTipoDoc, string? RemDocumento,
    string? RemEmail, string? RemTelefono, string? Nivel, string? DepNombre, string? DepOrigenNombre,
    string? FuncOrigen, string? Funcionario, string Prioridad, int? Folios, int? Anexos, string? Soporte,
    string? Relacionado, string? Operador, string? Vence, string? FechaDist, int? Dias, int? DiasTermino, string? TipoDia);

public sealed record RadicadoDocDto(long Reg, string Nombre, string? Ext, long Kb, string? Fecha, bool Previsualizable);
public sealed record RadicadoTrazaDto(string Fecha, string? Usuario, string Accion, string? Detalle);
public sealed record RadicadoTareaDto(string DepNombre, string? Funcionario, string Estado, bool Activa,
    string Prioridad, string? Instrucciones, string Fecha, string? DistribuidoPor, string? Observacion);
public sealed record RadicadoComDto(string Fecha, string? Usuario, string? Canal, string? Destino, string? Detalle, string? Estado);
public sealed record RadicadoVinculoDto(long Reg, string Numero, string Estado, RadicadoTipo Tipo);
public sealed record RadicadoSalidaDto(long Reg, string Numero, string? Fecha, bool Definitiva, string? EstadoEnvio, string? CanalEnvio);

// ===================== Distribucion (rad_tramites action=distribuir) =====================

public sealed record DistribuirRequest(
    long RadicadoId, long DependenciaId, long? FuncionarioId,
    string? Instrucciones, RadicadoPrioridad Prioridad, string? Justificacion);

/// <summary>Resultado tipado de distribuir (Ok/estado o error controlado, sin fuga de excepciones).</summary>
public sealed record DistribuirResult(bool Ok, string? Error = null, RadicadoEstado? Estado = null)
{
    public static DistribuirResult Fail(string error) => new(false, error);
    public static DistribuirResult Success(RadicadoEstado estado) => new(true, null, estado);
}

/// <summary>Opcion de un combo (dependencias/funcionarios/tipos) para los filtros y el modal.</summary>
public sealed record OpcionDto(long Id, string Nombre);
