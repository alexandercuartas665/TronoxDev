namespace Tronox.Application.Radicacion;

/// <summary>
/// Contrato del dashboard del Panel de Control (RQ09 RF12-1). Espejo del JSON que el legacy
/// rad_panel_op.ashx?action=dashboard devuelve al front. Los KPIs son "actuales/hoy" (ignoran el
/// rango, fiel al legacy); las series y la actividad respetan el rango (salvo actividad = ultimos 6).
/// </summary>
public sealed record RadicacionDashboardDto(
    KpiRadicacionDto Kpi,
    IReadOnlyList<SerieItemDto> PorDia,
    IReadOnlyList<SerieItemDto> PorTipo,
    IReadOnlyList<SerieItemDto> PorCanal,
    IReadOnlyList<SerieItemDto> PorEstado,
    IReadOnlyList<SerieItemDto> PorDependencia,
    IReadOnlyList<SerieItemDto> Promedio,
    SlaDto Sla,
    IReadOnlyList<ActividadRadicadoDto> Actividad,
    string Desde,
    string Hasta);

/// <summary>Seis indicadores del legacy (renderKpis). Todos "actuales", no filtran por rango.</summary>
public sealed record KpiRadicacionDto(
    int Hoy, int HoyEntrada, int HoySalida, int HoyInterno,
    int SinDistribuir, int Proximos, int Vencidos, int Tutelas,
    int Correos, string CorreosDetalle);

/// <summary>Punto de una serie: K = etiqueta, V = valor, C = color HEX opcional (por tipo).</summary>
public sealed record SerieItemDto(string K, double V, string? C = null);

/// <summary>Cumplimiento SLA: a tiempo vs vencidos (gauge del panel).</summary>
public sealed record SlaDto(int ATiempo, int Vencidos);

/// <summary>Fila de "Actividad reciente" (ultimos 6 radicados, sin filtrar por rango).</summary>
public sealed record ActividadRadicadoDto(
    long Reg, string Numero, string Fecha, string TipoNombre, string? TipoColor,
    string Canal, string Remitente, string Estado, int? Dias);
