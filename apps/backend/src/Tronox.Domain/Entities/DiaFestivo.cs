using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Dia no habil (festivo) del calendario de la entidad (RQ01 - calendario habil). Se usa para el
/// calculo de terminos/vencimientos SLA de radicacion (dias habiles). Se siembra por tenant con los
/// festivos de Colombia (Ley Emiliani + Pascua) y admite dias no habiles propios de la entidad.
/// Unico por (tenant, fecha). TENANT-SCOPED.
/// </summary>
public class DiaFestivo : TenantEntity
{
    public DateOnly Fecha { get; set; }
    public string Nombre { get; set; } = null!;

    /// <summary>true si lo sembro el sistema (festivo nacional); false si lo agrego la entidad.</summary>
    public bool EsNacional { get; set; }
}
