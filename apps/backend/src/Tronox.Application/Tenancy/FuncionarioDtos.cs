using Tronox.Application.Roles;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tenancy;

/// <summary>
/// Funcionario del tenant (RQ01 - RF06). Es la vista COMPLETA del mismo <c>TenantUser</c> que ya
/// usaba el backbone: no hay un modelo paralelo de personas.
///
/// La DEPENDENCIA no es un campo del usuario: viene DERIVADA de su cargo subiendo el arbol
/// (ADR-003, Addendum). Se incluye en el DTO porque la pantalla la muestra, no porque se almacene.
/// </summary>
public sealed record FuncionarioDto(
    long Id,
    long PlatformUserId,
    string Email,
    TenantRole TenantRole,
    PlatformUserStatus Status,
    TipoDocumento? TipoDocumento,
    string? NumeroDocumento,
    string? Nombres,
    string? Apellidos,
    string? Telefono,
    long? CargoOrgUnitId,
    string? CargoNombre,
    NivelJerarquico? CargoNivel,
    long? DependenciaId,
    string? DependenciaNombre,
    long? SedeId,
    string? SedeNombre,
    DateOnly? FechaVinculacion,
    string? FirmaImagenPath,
    long? MenuViewId,
    IReadOnlyList<RolAsignacionDto> Roles)
{
    public string NombreCompleto =>
        string.IsNullOrWhiteSpace(Nombres) && string.IsNullOrWhiteSpace(Apellidos)
            ? Email
            : $"{Nombres} {Apellidos}".Trim();

    /// <summary>
    /// Un funcionario es ACTIVABLE cuando tiene cargo, dependencia derivada y al menos un rol
    /// (criterio 2 de 5.6.3). La pantalla lo usa para explicar por que el boton esta bloqueado
    /// ANTES de que el usuario lo intente.
    /// </summary>
    public string? MotivoNoActivable =>
        FuncionarioRules.ValidatePuedeActivar(CargoOrgUnitId, DependenciaId, Roles.Count);
}

/// <summary>
/// Alta/edicion de un funcionario (RF06). Id null = alta.
///
/// Los ROLES no viajan aqui: se asignan por <c>IRolService.SetUserRolesAsync</c>, que es el unico
/// camino que audita la asignacion como pide el criterio 6 de 5.6.3. El servicio solo los CUENTA
/// para decidir si el funcionario puede quedar activo.
/// </summary>
public sealed record SaveFuncionarioRequest(
    long? Id,
    TipoDocumento? TipoDocumento,
    string? NumeroDocumento,
    string? Nombres,
    string? Apellidos,
    string CorreoElectronico,
    string? Telefono,
    long? CargoOrgUnitId,
    long? SedeId = null,
    DateOnly? FechaVinculacion = null,
    TenantRole TenantRole = TenantRole.Advisor,
    /// <summary>Clave inicial opcional. Sin clave el funcionario queda Invitado.</summary>
    string? Password = null);

/// <summary>
/// Resultado tipado de los servicios de usuarios del tenant (mismo patron que OrgResult):
/// sin excepciones crudas hacia la presentacion.
/// </summary>
public enum TenancyServiceStatus
{
    Ok = 0,
    NotFound,
    Invalid,
    Conflict
}

public sealed record TenancyResult<T>(TenancyServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == TenancyServiceStatus.Ok;

    public static TenancyResult<T> Ok(T value) => new(TenancyServiceStatus.Ok, value, null);
    public static TenancyResult<T> NotFound(string? error = null) => new(TenancyServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static TenancyResult<T> Invalid(string error) => new(TenancyServiceStatus.Invalid, default, error);
    public static TenancyResult<T> Conflict(string error) => new(TenancyServiceStatus.Conflict, default, error);

    /// <summary>Reetiqueta un resultado de FALLO a otro tipo de valor, conservando estado y mensaje.</summary>
    public TenancyResult<TOther> To<TOther>() => Status == TenancyServiceStatus.Ok
        ? throw new InvalidOperationException("Solo se reetiquetan resultados de fallo.")
        : new TenancyResult<TOther>(Status, default, Error);
}
