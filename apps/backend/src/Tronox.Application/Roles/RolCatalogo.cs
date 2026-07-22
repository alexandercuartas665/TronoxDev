namespace Tronox.Application.Roles;

/// <summary>
/// Definicion CANONICA de los roles PREDETERMINADOS de TRONOX (RQ01 - RF05).
///
/// Vive en Application y es LOGICA PURA (sin EF), igual que NivelClasificacionCatalogo y por la
/// misma razon: la usan tanto el aprovisionamiento del alta del tenant como los tests, de modo
/// que las dos definiciones no puedan derivar.
///
/// La identidad de un rol predeterminado es su <see cref="RolSemilla.CodigoSistema"/>, NO su
/// nombre: el nombre puede cambiar (ver "Lider de Dependencia") y la siembra debe seguir siendo
/// idempotente despues de que el tenant lo renombre.
/// </summary>
public static class RolCatalogo
{
    /// <summary>
    /// Un rol predeterminado. <paramref name="NivelCodigo"/> apunta al Codigo del
    /// NivelClasificacion ("01".."04"), no a su id: los ids son por tenant.
    /// </summary>
    /// <param name="MatrizCompleta">
    /// true = al sembrarlo recibe las 6 acciones sobre TODOS los modulos del menu del tenant.
    /// Es lo que sustituye al viejo bypass "AllowAll" del backbone: el Super Administrador lo ve
    /// todo porque su MATRIZ lo dice, no porque el codigo de permisos lo deje pasar.
    /// </param>
    /// <param name="AllowRename">true = rol de sistema RENOMBRABLE (excepcion, hoy solo DAT-05).</param>
    public sealed record RolSemilla(
        string CodigoSistema,
        string Nombre,
        string NivelCodigo,
        string Descripcion,
        bool MatrizCompleta = false,
        bool AllowRename = false);

    public const string CodigoSuperAdministrador = "SUPER_ADMIN";
    public const string CodigoAdministrador = "ADMIN";
    public const string CodigoAdministradorArchivo = "ADMIN_ARCHIVO";
    public const string CodigoRadicador = "RADICADOR";
    public const string CodigoArchivista = "ARCHIVISTA";
    public const string CodigoConsultaGeneral = "CONSULTA_GENERAL";

    /// <summary>
    /// Rol TECNICO de la plataforma (DAT-05): identifica al responsable jerarquico de una
    /// dependencia en los workflows de RQ11. NO es eliminable (el motor de workflows lo
    /// referencia) pero SI es renombrable, porque cada entidad lo llama distinto (Jefe de Area,
    /// Coordinador, Secretario General...).
    /// </summary>
    public const string CodigoLiderDependencia = "LIDER_DEPENDENCIA";

    public static readonly IReadOnlyList<RolSemilla> Roles =
    [
        new(CodigoSuperAdministrador, "Super Administrador", "04",
            "Control total de la configuracion y la operacion del sistema en el tenant.",
            MatrizCompleta: true),

        new(CodigoAdministrador, "Administrador", "04",
            "Administra usuarios, roles y parametrizacion general del tenant.",
            MatrizCompleta: true),

        new(CodigoAdministradorArchivo, "Administrador de Archivo", "03",
            "Responsable de la gestion archivistica: TRD, fondos, series y transferencias."),

        new(CodigoRadicador, "Radicador", "02",
            "Radica comunicaciones oficiales de entrada, salida e internas."),

        new(CodigoArchivista, "Archivista", "03",
            "Organiza, describe y conserva los expedientes y las unidades documentales."),

        new(CodigoConsultaGeneral, "Consulta General", "01",
            "Consulta la informacion publica del sistema, sin capacidad de modificacion."),

        new(CodigoLiderDependencia, "Lider de Dependencia", "02",
            "Rol tecnico (DAT-05): responsable jerarquico de una dependencia en los flujos de trabajo.",
            AllowRename: true)
    ];

    /// <summary>Los codigos de sistema, para validaciones y tests.</summary>
    public static IReadOnlySet<string> Codigos { get; } =
        Roles.Select(r => r.CodigoSistema).ToHashSet(StringComparer.Ordinal);
}

/// <summary>
/// Aprovisionamiento de los ROLES PREDETERMINADOS de un tenant (RQ01 - RF05).
///
/// Cuelga del camino de ALTA DEL TENANT, igual que IMenuProvisioningService y
/// IClasificacionProvisioningService, y por la misma razon (trampa heredada del backbone: lo que
/// se siembra desde un seeder de demo deja sin datos base a los clientes creados desde el panel).
///
/// ORDEN OBLIGATORIO en el alta: menu -> niveles de clasificacion -> roles. Los roles necesitan
/// los niveles (nivel_acceso_maximo es un FK obligatorio) y la matriz completa del Super
/// Administrador necesita el menu (el catalogo de modulos se deriva de el).
///
/// La implementacion vive en Infrastructure.
/// </summary>
public interface IRolProvisioningService
{
    /// <summary>
    /// Siembra los roles predeterminados que falten. IDEMPOTENTE y resistente al renombrado: la
    /// identidad es el CodigoSistema, asi que un rol ya sembrado no se duplica ni se revierte a
    /// su nombre original aunque el tenant lo haya renombrado.
    /// </summary>
    Task EnsureRolesPredeterminadosAsync(long tenantId, CancellationToken cancellationToken = default);
}
