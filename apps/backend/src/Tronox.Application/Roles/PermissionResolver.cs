using Tronox.Domain.Enums;

namespace Tronox.Application.Roles;

/// <summary>
/// Set de permisos de un modulo (una celda de la matriz resuelta a booleans, 6 acciones).
/// </summary>
public sealed record ModuleAccess(
    bool View, bool Create, bool Edit, bool Delete, bool Export, bool Print)
{
    /// <summary>Sin ningun permiso. Es el valor por defecto de todo modulo no concedido.</summary>
    public static readonly ModuleAccess None = new(false, false, false, false, false, false);

    /// <summary>Las 6 acciones concedidas. NO es un estado global: solo describe UN modulo.</summary>
    public static readonly ModuleAccess All = new(true, true, true, true, true, true);

    public bool Can(PermissionAction action) => action switch
    {
        PermissionAction.View => View,
        PermissionAction.Create => Create,
        PermissionAction.Edit => Edit,
        PermissionAction.Delete => Delete,
        PermissionAction.Export => Export,
        PermissionAction.Print => Print,
        _ => false
    };
}

/// <summary>
/// Permisos efectivos resueltos de un usuario del tenant (RQ01 - RF05).
///
/// FAIL-CLOSED (invariante 10). Esta clase NO tiene ninguna puerta trasera:
/// <list type="bullet">
/// <item>NO existe "Unrestricted" ni "AllowAll". El backbone (ECOREX) los tenia y por eso un
/// usuario sin rol, o una resolucion que fallaba, terminaban con acceso TOTAL. En una consola
/// interna eso era aceptable; en TRONOX, que maneja niveles Reservado y Clasificado, es una fuga
/// de informacion reservada.</item>
/// <item>Un modulo que no esta en el mapa resuelve a <see cref="ModuleAccess.None"/>.</item>
/// <item>Un usuario sin roles vigentes resuelve a <see cref="None"/>: SIN PERMISOS.</item>
/// </list>
/// Si el Super Administrador debe verlo todo, es porque SU ROL tiene la matriz completa
/// (la siembra del alta del tenant se la da), no porque el codigo lo deje pasar.
///
/// Estructura inmutable y sin dependencias de EF (logica pura, testeable sin base de datos).
/// </summary>
public sealed class EffectivePermissions
{
    private readonly IReadOnlyDictionary<string, ModuleAccess> _byModule;

    /// <summary>
    /// Orden del nivel de clasificacion MAXIMO que alcanza el usuario, o null si no tiene ningun
    /// rol vigente. Mas alto = mas restrictivo = mayor alcance de lectura (1 Publico .. 4
    /// Clasificado). null significa SIN alcance documental, no "todos".
    /// </summary>
    public int? NivelAccesoMaximoOrden { get; }

    /// <summary>Ids de los roles VIGENTES que se unieron para formar este set (vacio si ninguno).</summary>
    public IReadOnlyList<long> RolIds { get; }

    /// <summary>true si el usuario no tiene ningun rol vigente: SIN permisos.</summary>
    public bool IsEmpty => RolIds.Count == 0;

    private EffectivePermissions(
        IReadOnlyList<long> rolIds,
        int? nivelAccesoMaximoOrden,
        IReadOnlyDictionary<string, ModuleAccess> byModule)
    {
        RolIds = rolIds;
        NivelAccesoMaximoOrden = nivelAccesoMaximoOrden;
        _byModule = byModule;
    }

    /// <summary>
    /// SIN PERMISOS. Es el resultado de: usuario sin roles, roles vencidos, roles inactivos,
    /// usuario no resoluble, y de CUALQUIER fallo en la resolucion. Nunca concede nada.
    /// </summary>
    public static EffectivePermissions None { get; } =
        new([], null, new Dictionary<string, ModuleAccess>(StringComparer.Ordinal));

    /// <summary>Construye el set a partir de los modulos ya unidos (uso interno y de tests).</summary>
    public static EffectivePermissions Create(
        IReadOnlyList<long> rolIds,
        int? nivelAccesoMaximoOrden,
        IReadOnlyDictionary<string, ModuleAccess> byModule)
        => new(rolIds, nivelAccesoMaximoOrden, byModule);

    /// <summary>
    /// Set de UN rol (atajo para tests y para el caso de un solo rol). Sin vigencia ni estado:
    /// para la resolucion real usa <see cref="PermissionResolver.Resolve"/>.
    /// </summary>
    public static EffectivePermissions FromPermissions(
        long rolId, IEnumerable<ModulePermissionDto> permisos, int? nivelOrden = null)
    {
        var map = new Dictionary<string, ModuleAccess>(StringComparer.Ordinal);
        foreach (var p in permisos)
        {
            if (string.IsNullOrWhiteSpace(p.ModuleKey)) { continue; }
            map[p.ModuleKey] = new ModuleAccess(
                p.CanView, p.CanCreate, p.CanEdit, p.CanDelete, p.CanExport, p.CanPrint);
        }
        return new EffectivePermissions([rolId], nivelOrden, map);
    }

    /// <summary>Set del modulo. Modulo ausente -> <see cref="ModuleAccess.None"/> (fail-closed).</summary>
    public ModuleAccess For(string? moduleKey)
    {
        if (string.IsNullOrEmpty(moduleKey)) { return ModuleAccess.None; }
        return _byModule.TryGetValue(moduleKey, out var access) ? access : ModuleAccess.None;
    }

    /// <summary>
    /// Puerta unica del enforcement y de la UI. Sin excepciones ni atajos: si el modulo no esta
    /// en la matriz del usuario, la respuesta es NO.
    /// </summary>
    public bool Can(string? moduleKey, PermissionAction action) => For(moduleKey).Can(action);

    /// <summary>
    /// true si el usuario alcanza a leer documentos del nivel indicado. Sin nivel resuelto
    /// (ningun rol vigente) la respuesta es NO, nunca "por si acaso, si".
    /// </summary>
    public bool AlcanzaNivel(int nivelOrden)
        => NivelAccesoMaximoOrden is int max && nivelOrden <= max;

    /// <summary>Modulos con al menos un permiso concedido.</summary>
    public IReadOnlyCollection<string> ModuleKeys => (IReadOnlyCollection<string>)_byModule.Keys;
}

/// <summary>
/// Un rol asignado a un usuario, tal y como lo necesita la resolucion: su matriz, su nivel y su
/// vigencia. Es un DTO plano y sin EF para que <see cref="PermissionResolver.Resolve"/> sea
/// logica PURA y testeable sin base de datos.
/// </summary>
public sealed record RolGrant(
    long RolId,
    RolEstado Estado,
    int NivelAccesoMaximoOrden,
    DateTimeOffset? VigenteDesde,
    DateTimeOffset? VigenteHasta,
    IReadOnlyList<ModulePermissionDto> Permisos)
{
    /// <summary>
    /// La asignacion cuenta en la resolucion. Tres condiciones, todas necesarias:
    /// el rol esta Activo, ya empezo su vigencia, y no ha expirado.
    ///
    /// VigenteHasta es EXCLUSIVO: en el instante exacto de expiracion el rol ya NO cuenta. Asi un
    /// encargo temporal queda revocado AUTOMATICAMENTE al vencer, sin que nadie tenga que
    /// acordarse de ir a borrar la fila.
    /// </summary>
    public bool EstaVigente(DateTimeOffset asOf)
        => Estado == RolEstado.Activo
        && (VigenteDesde is not DateTimeOffset desde || desde <= asOf)
        && (VigenteHasta is not DateTimeOffset hasta || asOf < hasta);
}

/// <summary>
/// Logica PURA de resolucion de permisos (sin EF, testeable en Application.Tests). El servicio la
/// usa para no repetir reglas y para poder probarlas en aislamiento.
/// </summary>
public static class PermissionResolver
{
    /// <summary>
    /// Filtra las filas de permiso que deben persistirse: solo las que conceden algo
    /// (SavePermisos borra e reinserta). Deduplica por ModuleKey (gana la ultima). Descarta las
    /// que no correspondan a un modulo del catalogo si <paramref name="validModuleKeys"/> se
    /// provee (null = no valida contra catalogo).
    /// </summary>
    public static IReadOnlyList<ModulePermissionDto> FilterPersistable(
        IEnumerable<ModulePermissionDto> permisos,
        ISet<string>? validModuleKeys = null)
    {
        var byKey = new Dictionary<string, ModulePermissionDto>(StringComparer.Ordinal);
        foreach (var p in permisos)
        {
            if (p is null || string.IsNullOrWhiteSpace(p.ModuleKey)) { continue; }
            if (!p.HasAny) { continue; }
            if (validModuleKeys is not null && !validModuleKeys.Contains(p.ModuleKey)) { continue; }
            byKey[p.ModuleKey] = p;
        }
        return byKey.Values.ToList();
    }

    /// <summary>
    /// Resuelve los permisos efectivos de un usuario a partir de TODOS sus roles asignados.
    ///
    /// Reglas (RF05):
    /// 1. Solo cuentan los roles VIGENTES (activos y dentro de su ventana temporal).
    /// 2. Los permisos se UNEN (OR): basta que UN rol vigente conceda (modulo, accion).
    /// 3. El nivel de acceso es el MAS ALTO (mayor NivelOrden) entre los roles vigentes.
    /// 4. FAIL-CLOSED: sin roles, o sin roles vigentes, el resultado es
    ///    <see cref="EffectivePermissions.None"/> - SIN PERMISOS, jamas acceso total.
    /// </summary>
    public static EffectivePermissions Resolve(
        IEnumerable<RolGrant>? grants,
        DateTimeOffset asOf)
    {
        if (grants is null)
        {
            return EffectivePermissions.None;
        }

        var rolIds = new List<long>();
        int? nivelMaximo = null;
        var union = new Dictionary<string, ModuleAccess>(StringComparer.Ordinal);

        foreach (var grant in grants)
        {
            if (grant is null || !grant.EstaVigente(asOf))
            {
                continue;
            }

            rolIds.Add(grant.RolId);
            // Mas alto = mas restrictivo = mayor alcance de lectura.
            if (nivelMaximo is not int actual || grant.NivelAccesoMaximoOrden > actual)
            {
                nivelMaximo = grant.NivelAccesoMaximoOrden;
            }

            foreach (var p in grant.Permisos ?? [])
            {
                if (p is null || string.IsNullOrWhiteSpace(p.ModuleKey)) { continue; }
                union[p.ModuleKey] = union.TryGetValue(p.ModuleKey, out var previo)
                    ? Union(previo, p)
                    : new ModuleAccess(p.CanView, p.CanCreate, p.CanEdit, p.CanDelete, p.CanExport, p.CanPrint);
            }
        }

        // Sin ningun rol vigente no hay nada que conceder.
        if (rolIds.Count == 0)
        {
            return EffectivePermissions.None;
        }

        return EffectivePermissions.Create(rolIds, nivelMaximo, union);
    }

    /// <summary>OR accion por accion: lo que conceda cualquiera de los dos queda concedido.</summary>
    private static ModuleAccess Union(ModuleAccess a, ModulePermissionDto b) => new(
        a.View || b.CanView,
        a.Create || b.CanCreate,
        a.Edit || b.CanEdit,
        a.Delete || b.CanDelete,
        a.Export || b.CanExport,
        a.Print || b.CanPrint);
}
