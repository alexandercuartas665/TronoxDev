using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Parametros de seguridad de la entidad (legacy GEN_ENTIDADES_PARAM_SEGURIDAD). Control de acceso +
/// politica de contrasenas. Singleton por tenant. Embebido en la pantalla de Datos de la Entidad.
/// TENANT-SCOPED. (Las contrasenas siguen con bcrypt/argon2, invariante 5; esto solo son parametros.)
/// </summary>
public class ParametrosSeguridad : TenantEntity
{
    public int IntentosFallidosMax { get; set; } = 5;
    public int TiempoInactividadSesionMin { get; set; } = 30;
    public int VigenciaContrasenaDias { get; set; } = 90;
    public int HistorialContrasenas { get; set; } = 5;
    public int LongitudMinimaContrasena { get; set; } = 8;
    public int? TiempoAvisoVencimientoDias { get; set; } = 7;
    public bool ComplejidadContrasena { get; set; } = true;
}
