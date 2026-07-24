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

    // ---- Datos personales del funcionario (RQ01 - RF06 seccion 5.6.1) ----
    //
    // Son NULLABLE en base de datos y OBLIGATORIOS por regla de negocio (FuncionarioRules), igual
    // que la ubicacion de Entidad: las filas creadas antes de RF06 (invitaciones del backbone) no
    // pueden quedar imposibles de guardar, pero la pantalla de RF06 los exige.

    /// <summary>Tipo de documento de identidad (CC / CE / Pasaporte / NIT).</summary>
    public TipoDocumento? TipoDocumento { get; set; }

    /// <summary>Numero de documento (20). UNICO POR TENANT (indice unico filtrado).</summary>
    public string? NumeroDocumento { get; set; }

    /// <summary>Nombres del funcionario (100).</summary>
    public string? Nombres { get; set; }

    /// <summary>Apellidos del funcionario (100).</summary>
    public string? Apellidos { get; set; }

    /// <summary>
    /// Sede a la que pertenece el funcionario (RF01 4.1.2). Opcional: una entidad puede no tener
    /// sedes. FK NO ACTION: inactivar una sede jamas borra usuarios.
    /// </summary>
    public long? SedeId { get; set; }
    public Sede? Sede { get; set; }

    /// <summary>Fecha de vinculacion a la entidad. Opcional.</summary>
    public DateOnly? FechaVinculacion { get; set; }

    /// <summary>
    /// RUTA de la imagen de firma manuscrita escaneada (500). Se guarda la ruta, JAMAS los bytes
    /// (invariante 9: binarios fuera de la base de datos). Hoy apunta a wwwroot/uploads; cuando
    /// entre MinIO cambia la ruta, no el modelo.
    /// </summary>
    public string? FirmaImagenPath { get; set; }

    /// <summary>
    /// Reservado para el componente de firma DIGITAL (CAdES/PAdES/XAdES) de RQ05, que llega en un
    /// hito posterior. La nota tecnica de RF06 pide dejarlo previsto desde ahora para no migrar la
    /// tabla cuando exista; hoy siempre es null y ningun codigo lo lee.
    /// </summary>
    public long? FirmaDigitalId { get; set; }

    /// <summary>Token de invitacion para que el asesor complete su registro (clave + foto). Null si ya activo.</summary>
    public string? InvitationToken { get; set; }
    public DateTimeOffset? InvitationExpiresAt { get; set; }

    /// <summary>
    /// Nombre para mostrar derivado de los datos personales de RF06. Si el funcionario todavia no
    /// los tiene (fila anterior a RF06), cae al correo. NO se persiste.
    /// </summary>
    public string NombreCompleto =>
        string.IsNullOrWhiteSpace(Nombres) && string.IsNullOrWhiteSpace(Apellidos)
            ? Email
            : $"{Nombres} {Apellidos}".Trim();
}
