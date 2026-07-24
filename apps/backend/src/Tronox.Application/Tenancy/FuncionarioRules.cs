using Tronox.Domain.Enums;

namespace Tronox.Application.Tenancy;

/// <summary>
/// Validacion PURA de un funcionario (RQ01 - RF06). Sin EF: se puede testear sin base de datos,
/// igual que OrgStructureRules y EntidadRules. Devuelve el mensaje de error, o null si es valido.
///
/// Lo que NO se valida aqui porque necesita base de datos (vive en TenantUserService):
/// unicidad de documento y de correo dentro del tenant, existencia del cargo, de la sede y de
/// los roles, y la dependencia DERIVADA del cargo (que se resuelve caminando el arbol).
/// </summary>
public static class FuncionarioRules
{
    public const int MaxNumeroDocumento = 20;
    public const int MaxNombres = 100;
    public const int MaxApellidos = 100;
    public const int MaxCorreo = 150;
    public const int MaxTelefono = 20;

    /// <summary>
    /// Datos personales y de contacto (5.6.1). Se validan SIEMPRE, se vaya a activar o no: un
    /// registro incompleto no sirve ni como metadato documental.
    /// </summary>
    public static string? ValidateDatos(
        TipoDocumento? tipoDocumento,
        string? numeroDocumento,
        string? nombres,
        string? apellidos,
        string? correoElectronico,
        string? telefono)
    {
        if (tipoDocumento is null)
        {
            return "El tipo de documento es obligatorio.";
        }
        if (string.IsNullOrWhiteSpace(numeroDocumento))
        {
            return "El numero de documento es obligatorio.";
        }
        if (numeroDocumento.Trim().Length > MaxNumeroDocumento)
        {
            return $"El numero de documento no puede superar {MaxNumeroDocumento} caracteres.";
        }
        if (string.IsNullOrWhiteSpace(nombres))
        {
            return "Los nombres son obligatorios.";
        }
        if (nombres.Trim().Length > MaxNombres)
        {
            return $"Los nombres no pueden superar {MaxNombres} caracteres.";
        }
        if (string.IsNullOrWhiteSpace(apellidos))
        {
            return "Los apellidos son obligatorios.";
        }
        if (apellidos.Trim().Length > MaxApellidos)
        {
            return $"Los apellidos no pueden superar {MaxApellidos} caracteres.";
        }
        if (!EsCorreoValido(correoElectronico))
        {
            return "El correo electronico no es valido.";
        }
        if (correoElectronico!.Trim().Length > MaxCorreo)
        {
            return $"El correo electronico no puede superar {MaxCorreo} caracteres.";
        }
        if (telefono is { } t && t.Trim().Length > MaxTelefono)
        {
            return $"El telefono no puede superar {MaxTelefono} caracteres.";
        }
        return null;
    }

    /// <summary>
    /// Criterio 2 de 5.6.3: para quedar ACTIVO, un funcionario necesita DEPENDENCIA, CARGO y al
    /// menos un ROL.
    ///
    /// La dependencia NO se captura: se DERIVA subiendo por el cargo hasta el primer nodo
    /// Dependencia (ADR-003, Addendum). Por eso el parametro es la dependencia YA resuelta: un
    /// cargo colgado de la raiz resuelve a null y entonces el usuario NO puede activarse, que es
    /// justo el fail-closed del punto 3 del Addendum (sin area documental no hay visibilidad, y
    /// no se le concede "todo" por no tenerla).
    /// </summary>
    public static string? ValidatePuedeActivar(
        long? cargoOrgUnitId, long? dependenciaDerivadaId, int rolesVigentes)
    {
        if (cargoOrgUnitId is null)
        {
            return "Para activar al funcionario asignale un cargo del catalogo.";
        }
        if (dependenciaDerivadaId is null)
        {
            return "El cargo asignado no cuelga de ninguna dependencia, asi que el funcionario no "
                 + "tendria area documental. Reubica el cargo bajo una dependencia antes de activarlo.";
        }
        if (rolesVigentes <= 0)
        {
            return "Para activar al funcionario asignale al menos un rol de permisos (RF05).";
        }
        return null;
    }

    /// <summary>
    /// Estados que dejan iniciar sesion. Solo Activo; el resto conserva los datos pero no accede
    /// (5.6.2). Se expone como regla pura para que la UI y el login no diverjan.
    /// </summary>
    public static bool PuedeIniciarSesion(PlatformUserStatus estado)
        => estado == PlatformUserStatus.Active;

    /// <summary>Normaliza el correo a minusculas y sin espacios (es el LOGIN, criterio 1).</summary>
    public static string NormalizarCorreo(string correo) => correo.Trim().ToLowerInvariant();

    /// <summary>
    /// Validacion de correo deliberadamente SIMPLE (hay una arroba con texto util a cada lado y un
    /// punto en el dominio). Las expresiones regulares "completas" de RFC 5322 rechazan
    /// direcciones validas y no evitan ni una sola direccion inexistente: quien confirma que un
    /// correo existe es el envio, no el formato.
    /// </summary>
    private static bool EsCorreoValido(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo)) { return false; }
        var value = correo.Trim();
        var at = value.IndexOf('@');
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1) { return false; }
        var dominio = value[(at + 1)..];
        var punto = dominio.IndexOf('.');
        return punto > 0 && punto < dominio.Length - 1 && !value.Contains(' ');
    }
}
