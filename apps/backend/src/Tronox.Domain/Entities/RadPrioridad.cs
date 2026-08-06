using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Prioridad de radicacion configurable (RAD_PRIORIDADES, RQ09 RF01-1). La consumen el asistente de
/// radicacion, la distribucion (RF07-2) y la bandeja de tramites. Las base (Normal/Alta/Urgente) no se
/// eliminan (EsBase): solo editar/inactivar (invariante 8). Unica por (tenant, codigo). TENANT-SCOPED.
/// </summary>
public class RadPrioridad : TenantEntity
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    /// <summary>Emoji/clave de icono (max 10).</summary>
    public string? Icono { get; set; }
    /// <summary>Color HEX #RRGGBB.</summary>
    public string? Color { get; set; }
    /// <summary>SLA sugerido en dias (opcional).</summary>
    public int? SlaSugerido { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary>Base normativa no eliminable.</summary>
    public bool EsBase { get; set; }
    public int Orden { get; set; } = 99;
}
