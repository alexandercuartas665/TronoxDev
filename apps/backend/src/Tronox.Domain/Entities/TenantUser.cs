using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Vinculo entre un PlatformUser y un tenant, con rol interno. Entidad TENANT-SCOPED:
/// recibe filtro global de consulta por TenantId.
/// </summary>
public class TenantUser : TenantEntity
{
    public long PlatformUserId { get; set; }
    public PlatformUser? PlatformUser { get; set; }

    public string Email { get; set; } = null!;
    public TenantRole TenantRole { get; set; } = TenantRole.Advisor;
    public PlatformUserStatus Status { get; set; } = PlatformUserStatus.Active;

    /// <summary>Codigo o documento del asesor/vendedor dentro de este tenant (maestro de vendedores 000124). Opcional.</summary>
    public string? DocumentCode { get; set; }

    /// <summary>Telefono de contacto del asesor/vendedor. Opcional.</summary>
    public string? Phone { get; set; }

    /// <summary>Alcance de leads del asesor (los admin/owner/supervisor ven todo por rol).</summary>
    public LeadVisibility LeadVisibility { get; set; } = LeadVisibility.OwnOnly;

    /// <summary>
    /// Vista del menu configurable asignada a este usuario (Ola 1 del menu por perfil).
    /// Null = usa la vista IsDefault del tenant. FK NO ACTION (no arrastra al usuario por cascada).
    /// </summary>
    public long? MenuViewId { get; set; }
    public MenuView? MenuView { get; set; }

    /// <summary>
    /// Roles de permisos asignados a este usuario (RQ01 - RF05, multi-rol).
    ///
    /// OJO: ya NO existe un unico "RolId" en el usuario. RF05 exige VARIOS roles por usuario con
    /// vigencia temporal, asi que la asignacion vive en la tabla puente <see cref="UsuarioRol"/>
    /// (usuarios_roles). Los permisos efectivos son la UNION de los roles vigentes y el nivel de
    /// acceso es el MAS ALTO entre ellos; un usuario SIN ningun rol vigente resuelve a SIN
    /// PERMISOS, nunca a acceso total (invariante 10, FAIL-CLOSED).
    ///
    /// Distinto de <see cref="TenantRole"/>, que modela el poder ORGANICO (Owner/Admin/...) y no
    /// concede por si solo ningun permiso de modulo.
    /// </summary>
    public ICollection<UsuarioRol> Roles { get; set; } = new List<UsuarioRol>();

    /// <summary>
    /// Nodo CARGO del arbol organizacional al que se ancla este usuario (ADR-003, Addendum).
    ///
    /// El usuario apunta a UN SOLO nodo: su Cargo. La DEPENDENCIA NO SE ALMACENA AQUI: se
    /// deriva subiendo por la cadena de padres hasta el primer nodo con clasificador
    /// Dependencia (ver OrgUnitTree.ResolveDependenciaId, logica pura y cacheable).
    ///
    /// FAIL-CLOSED: null, o un Cargo sin ninguna Dependencia por encima, resuelve a SIN
    /// dependencia y por tanto SIN visibilidad documental. Nunca a visibilidad total.
    ///
    /// FK NO ACTION: mover o archivar el organigrama jamas borra usuarios en cascada.
    /// </summary>
    public long? CargoOrgUnitId { get; set; }
    public OrgUnit? CargoOrgUnit { get; set; }

    /// <summary>Token de invitacion para que el asesor complete su registro (clave + foto). Null si ya activo.</summary>
    public string? InvitationToken { get; set; }
    public DateTimeOffset? InvitationExpiresAt { get; set; }
}
