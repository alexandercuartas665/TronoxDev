using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Rol de permisos dentro del tenant (RQ01 - RF05). Define, via <see cref="RolPermiso"/>, que
/// puede hacer un usuario sobre cada modulo real del menu (matriz Modulo x Accion), y hasta que
/// NIVEL DE CLASIFICACION documental alcanza a leer. TENANT-SCOPED (filtro global por TenantId).
///
/// Un usuario puede tener VARIOS roles a la vez (tabla puente <see cref="UsuarioRol"/>): sus
/// permisos son la UNION de todos sus roles vigentes y su nivel de acceso es el MAS ALTO de
/// ellos. Ver PermissionResolver (logica pura).
/// </summary>
public class Rol : TenantEntity
{
    /// <summary>Nombre del rol (obligatorio, unico por tenant, 100).</summary>
    public string Name { get; set; } = null!;

    /// <summary>Descripcion libre (300).</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Nivel de clasificacion MAXIMO que este rol alcanza a leer (FK a niveles_clasificacion,
    /// OBLIGATORIO). Es un FK y no un texto porque la comparacion se hace sobre
    /// <see cref="NivelClasificacion.NivelOrden"/>: mas alto = mas restrictivo = mayor alcance.
    /// </summary>
    public long NivelAccesoMaximoId { get; set; }
    public NivelClasificacion? NivelAccesoMaximo { get; set; }

    /// <summary>
    /// Rol PREDETERMINADO de plataforma: no se puede eliminar y no se le puede cambiar el
    /// <see cref="NivelAccesoMaximoId"/>. Lo siembra el alta del tenant, no un seeder de demo.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// Excepcion al bloqueo de renombrado de los roles de sistema. Solo la usa el rol tecnico
    /// "Lider de Dependencia" (DAT-05): identifica al responsable jerarquico en los workflows de
    /// RQ11, asi que NO es eliminable, pero cada entidad le da el nombre que use internamente
    /// (Jefe de Area, Coordinador, Secretario General...), asi que SI es renombrable.
    /// Los roles NO de sistema siempre se pueden renombrar (esta bandera solo aplica a IsSystem).
    /// </summary>
    public bool AllowRename { get; set; }

    /// <summary>
    /// Codigo estable del rol predeterminado (ej. "SUPER_ADMIN", "LIDER_DEPENDENCIA"). Null en
    /// los roles que crea el tenant. Es la IDENTIDAD del rol de sistema para la siembra
    /// idempotente: sobrevive a que el tenant lo renombre, cosa que el Name no hace.
    /// </summary>
    public string? CodigoSistema { get; set; }

    /// <summary>Activo/Inactivo. Un rol Inactivo NO concede permisos (fail-closed).</summary>
    public RolEstado Estado { get; set; } = RolEstado.Activo;
}

/// <summary>
/// Permiso de un rol sobre un modulo, UNA FILA POR (modulo, accion) (RQ01 - RF05).
///
/// La spec es explicita: "No usar bitmask ni columnas JSON: dificultan consultas y auditoria".
/// Por eso esta tabla NO tiene columnas can_view/can_create/...: tiene <see cref="Modulo"/>,
/// <see cref="Accion"/> y <see cref="Permitido"/>, y una fila por combinacion. Asi un "quien
/// puede exportar el modulo X" es un WHERE, no un desempaquetado de bits.
///
/// <see cref="Modulo"/> = Route del MenuNode Item (ej. "admin-usuarios"): la MISMA cadena
/// identifica el destino de navegacion y la llave de permiso, de modo que el catalogo de modulos
/// se DERIVA del menu y no puede desincronizarse de una lista paralela.
///
/// Unico por (TenantId, RolId, Modulo, Accion). TENANT-SCOPED. Solo se persisten las filas
/// concedidas (SavePermisos borra e reinserta dentro de una transaccion).
/// </summary>
public class RolPermiso : TenantEntity
{
    public long RolId { get; set; }
    public Rol? Rol { get; set; }

    /// <summary>Clave del modulo = Route del MenuNode Item (ej. "admin-usuarios").</summary>
    public string Modulo { get; set; } = null!;

    /// <summary>Accion concedida (Ver/Crear/Editar/Eliminar/Exportar/Imprimir). Texto en BD.</summary>
    public PermissionAction Accion { get; set; }

    /// <summary>true = concedido. Solo se persisten filas concedidas; ausencia = sin permiso.</summary>
    public bool Permitido { get; set; }
}

/// <summary>
/// Asignacion de un ROL a un USUARIO del tenant, con VIGENCIA TEMPORAL (RQ01 - RF05, multi-rol).
/// TENANT-SCOPED.
///
/// Reemplaza al antiguo TenantUser.RolId (un solo rol por usuario), que no podia expresar ni la
/// union de varios roles ni la caducidad. Un usuario puede acumular varias filas; sus permisos
/// efectivos son la UNION (OR) de los roles VIGENTES y su nivel de acceso es el MAS ALTO entre
/// ellos.
///
/// Vigencia: <see cref="VigenteDesde"/> null = vigente desde siempre; <see cref="VigenteHasta"/>
/// null = sin expiracion. Una asignacion con VigenteHasta ya pasado queda revocada
/// AUTOMATICAMENTE (no cuenta en la resolucion) sin que nadie tenga que ir a borrarla: asi un
/// encargo temporal no sobrevive por olvido. Ver PermissionResolver.
/// </summary>
public class UsuarioRol : TenantEntity
{
    public long TenantUserId { get; set; }
    public TenantUser? TenantUser { get; set; }

    public long RolId { get; set; }
    public Rol? Rol { get; set; }

    /// <summary>Inicio de vigencia. Null = vigente desde siempre.</summary>
    public DateTimeOffset? VigenteDesde { get; set; }

    /// <summary>Fin de vigencia (exclusivo). Null = sin expiracion.</summary>
    public DateTimeOffset? VigenteHasta { get; set; }
}
