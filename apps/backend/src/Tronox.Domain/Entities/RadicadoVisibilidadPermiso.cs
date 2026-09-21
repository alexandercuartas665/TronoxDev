using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Nivel de visibilidad de radicados de un usuario (RF11-8, RAD_PERMISOS_VISIBILIDAD). Configura si un
/// usuario ve solo lo Propio, lo de su Dependencia o Todo. Es un tightening ADITIVO sobre el permiso del
/// modulo (el gate fail-closed real). Sin fila para el usuario -> Todos (dentro del tenant, ya aislado);
/// el resolver NUNCA degrada a Todos ante un ERROR (cae en Propios). Unico por (tenant, usuario).
/// TENANT-SCOPED.
/// </summary>
public class RadicadoVisibilidadPermiso : TenantEntity
{
    /// <summary>Usuario del tenant (TenantUser). NO ACTION.</summary>
    public long TenantUserId { get; set; }

    public VisibilidadNivel Nivel { get; set; } = VisibilidadNivel.Propios;

    public bool Activo { get; set; } = true;
}
