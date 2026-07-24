using Tronox.Domain.Enums;

namespace Tronox.Application.Tenancy;

/// <summary>
/// Gestion de usuarios dentro del tenant activo (modulo 1.2). Todas las operaciones quedan
/// acotadas al tenant del contexto (filtro global de consulta + estampado en alta).
/// </summary>
public interface ITenantUserService
{
    Task<IReadOnlyList<TenantUserDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Devuelve null si no hay tenant activo o si el usuario ya es miembro del tenant.</summary>
    Task<TenantUserDto?> InviteAsync(InviteTenantUserRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<TenantUserDto?> ChangeRoleAsync(long tenantUserId, TenantRole role, long actorUserId, CancellationToken cancellationToken = default);

    Task<TenantUserDto?> SetStatusAsync(long tenantUserId, PlatformUserStatus status, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// El admin del tenant fija una clave nueva para un usuario del tenant (hashea con PBKDF2,
    /// actualiza PlatformUser.PasswordHash y, si estaba Invited, lo pasa a Active). Audita.
    /// Devuelve null si el usuario no existe en el tenant; lanza ArgumentException si la clave
    /// es vacia o tiene menos de 6 caracteres. NUNCA registra la clave en claro.
    /// </summary>
    Task<TenantUserDto?> ResetPasswordAsync(long tenantUserId, string newPassword, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Edita el DisplayName del PlatformUser vinculado a un usuario del tenant (opcional; null o
    /// vacio lo deja sin nombre). Audita. Devuelve null si el usuario no existe en el tenant.
    /// </summary>
    Task<TenantUserDto?> UpdateProfileAsync(long tenantUserId, string? displayName, long actorUserId, CancellationToken cancellationToken = default);

    // ------------------------------------------------------------------------------------
    // RQ01 - RF06 (Gestion de Usuarios / Funcionarios). Es el MISMO TenantUser de arriba con
    // sus datos personales y organizacionales; no hay un modelo paralelo de personas.
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// Funcionarios del tenant con cargo, DEPENDENCIA DERIVADA (ADR-003, Addendum), sede y roles.
    /// </summary>
    Task<IReadOnlyList<FuncionarioDto>> ListFuncionariosAsync(CancellationToken cancellationToken = default);

    Task<FuncionarioDto?> GetFuncionarioAsync(long tenantUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Alta o edicion de un funcionario. Valida los datos personales (FuncionarioRules), la
    /// UNICIDAD del documento y del correo dentro del tenant, y que cargo y sede existan.
    ///
    /// El alta NUNCA nace Activa: queda Invitada hasta que <see cref="SetFuncionarioEstadoAsync"/>
    /// compruebe que tiene dependencia, cargo y rol (criterio 2 de 5.6.3).
    /// </summary>
    Task<TenancyResult<FuncionarioDto>> SaveFuncionarioAsync(
        SaveFuncionarioRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el estado de la cuenta (5.6.2) con motivo y auditoria. Pasar a ACTIVO exige
    /// dependencia + cargo + al menos un rol. Inactivar CONSERVA documentos y auditoria
    /// (criterio 4): nunca hay eliminacion real.
    /// </summary>
    Task<TenancyResult<FuncionarioDto>> SetFuncionarioEstadoAsync(
        long tenantUserId, PlatformUserStatus estado, string? motivo, long actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda la RUTA de la imagen de firma manuscrita (invariante 9: nunca los bytes en base de
    /// datos). Pasar null la quita.
    /// </summary>
    Task<TenancyResult<FuncionarioDto>> SetFirmaImagenAsync(
        long tenantUserId, string? rutaSegura, long actorUserId, CancellationToken cancellationToken = default);
}
