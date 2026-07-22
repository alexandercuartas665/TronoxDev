using Tronox.Domain.Enums;

namespace Tronox.Application.Roles;

/// <summary>
/// Rol de permisos (fila del listado). UserCount = usuarios del tenant con una asignacion
/// VIGENTE de este rol.
/// </summary>
public sealed record RolDto(
    long Id,
    string Name,
    string? Description,
    RolEstado Estado,
    bool IsSystem,
    bool AllowRename,
    string? CodigoSistema,
    long NivelAccesoMaximoId,
    string? NivelAccesoMaximoNombre,
    int NivelAccesoMaximoOrden,
    int UserCount)
{
    /// <summary>Comodidad para la UI: un rol Inactivo no concede nada.</summary>
    public bool IsActive => Estado == RolEstado.Activo;

    /// <summary>
    /// true si el rol puede renombrarse: los de tenant siempre; los de sistema solo si tienen la
    /// excepcion (hoy unicamente "Lider de Dependencia", DAT-05).
    /// </summary>
    public bool CanRename => !IsSystem || AllowRename;

    /// <summary>true si el rol puede eliminarse (los de sistema, jamas).</summary>
    public bool CanDelete => !IsSystem;
}

/// <summary>
/// Permisos de UN modulo dentro de un rol, en forma de fila de la matriz para la UI (6 acciones).
///
/// OJO - esto es una PROYECCION de presentacion, no la forma de la tabla: en base de datos hay
/// una fila por (modulo, accion) (ver RolPermiso), como exige la spec. El servicio explota este
/// DTO en filas al guardar y las reagrupa al leer.
/// </summary>
public sealed record ModulePermissionDto(
    string ModuleKey,
    bool CanView,
    bool CanCreate,
    bool CanEdit,
    bool CanDelete,
    bool CanExport,
    bool CanPrint)
{
    /// <summary>Constructor de conveniencia para las 4 acciones clasicas (exportar/imprimir en false).</summary>
    public ModulePermissionDto(string moduleKey, bool canView, bool canCreate, bool canEdit, bool canDelete)
        : this(moduleKey, canView, canCreate, canEdit, canDelete, false, false)
    {
    }

    /// <summary>true si la fila concede algo (se persiste); false = fila vacia (no se guarda).</summary>
    public bool HasAny => CanView || CanCreate || CanEdit || CanDelete || CanExport || CanPrint;

    public bool Can(PermissionAction action) => action switch
    {
        PermissionAction.View => CanView,
        PermissionAction.Create => CanCreate,
        PermissionAction.Edit => CanEdit,
        PermissionAction.Delete => CanDelete,
        PermissionAction.Export => CanExport,
        PermissionAction.Print => CanPrint,
        _ => false
    };

    /// <summary>Acciones concedidas: exactamente las filas que se persisten para este modulo.</summary>
    public IEnumerable<PermissionAction> GrantedActions()
    {
        foreach (var action in PermissionActions.All)
        {
            if (Can(action)) { yield return action; }
        }
    }

    /// <summary>Reconstruye la fila de UI a partir del conjunto de acciones concedidas (lectura).</summary>
    public static ModulePermissionDto FromActions(string moduleKey, IEnumerable<PermissionAction> actions)
    {
        var set = new HashSet<PermissionAction>(actions);
        return new ModulePermissionDto(
            moduleKey,
            set.Contains(PermissionAction.View),
            set.Contains(PermissionAction.Create),
            set.Contains(PermissionAction.Edit),
            set.Contains(PermissionAction.Delete),
            set.Contains(PermissionAction.Export),
            set.Contains(PermissionAction.Print));
    }
}

/// <summary>Las SEIS acciones de la matriz, en el orden en que se muestran.</summary>
public static class PermissionActions
{
    public static readonly IReadOnlyList<PermissionAction> All =
    [
        PermissionAction.View,
        PermissionAction.Create,
        PermissionAction.Edit,
        PermissionAction.Delete,
        PermissionAction.Export,
        PermissionAction.Print
    ];

    /// <summary>Etiqueta en espanol para la UI (la spec nombra las acciones en espanol).</summary>
    public static string Label(PermissionAction action) => action switch
    {
        PermissionAction.View => "Ver",
        PermissionAction.Create => "Crear",
        PermissionAction.Edit => "Editar",
        PermissionAction.Delete => "Eliminar",
        PermissionAction.Export => "Exportar",
        PermissionAction.Print => "Imprimir",
        _ => action.ToString()
    };
}

/// <summary>Detalle de un rol: sus datos + la matriz por modulo (solo los modulos con algo concedido).</summary>
public sealed record RolDetailDto(
    long Id,
    string Name,
    string? Description,
    RolEstado Estado,
    bool IsSystem,
    bool AllowRename,
    string? CodigoSistema,
    long NivelAccesoMaximoId,
    IReadOnlyList<ModulePermissionDto> Permisos)
{
    public bool IsActive => Estado == RolEstado.Activo;
    public bool CanRename => !IsSystem || AllowRename;
}

/// <summary>Modulo del catalogo sobre el que se definen permisos. Derivado del menu (Route/Name/Section).</summary>
public sealed record ModuloInfo(string Key, string Label, string Grupo);

/// <summary>
/// Asignacion de un rol a un usuario con su vigencia (RF05 multi-rol). VigenteHasta null = sin
/// expiracion; una asignacion con VigenteHasta pasado esta revocada y no cuenta.
/// </summary>
public sealed record RolAsignacionDto(
    long RolId,
    string? RolNombre,
    DateTimeOffset? VigenteDesde,
    DateTimeOffset? VigenteHasta);

/// <summary>Datos para crear/editar un rol (RF05).</summary>
public sealed record SaveRolRequest(
    long? Id,
    string Name,
    string? Description,
    long NivelAccesoMaximoId,
    RolEstado Estado);
