namespace Tronox.Domain.Enums;

/// <summary>Estado de una cuenta de usuario de plataforma (Notas dev sec.1.5).</summary>
public enum PlatformUserStatus
{
    Active,
    Invited,
    Blocked,
    Suspended,
    /// <summary>
    /// Cuenta desactivada por el administrador (RQ01 - RF06 seccion 5.6.2). No puede iniciar
    /// sesion, pero SUS DATOS SE CONSERVAN: documentos, expedientes y pistas de auditoria quedan
    /// intactos (criterio 4 de 5.6.3 e invariante 8: nunca hay eliminacion real).
    ///
    /// Es distinto de Suspended (medida temporal del administrador o del Super Admin) y de
    /// Blocked (lo pone el SISTEMA tras intentos fallidos).
    /// </summary>
    Inactive,
    /// <summary>
    /// Cuenta creada por auto-registro pero todavia no activada (el usuario debe ingresar el
    /// codigo enviado por correo). No puede iniciar sesion hasta cambiar a Active.
    /// </summary>
    PendingActivation
}
