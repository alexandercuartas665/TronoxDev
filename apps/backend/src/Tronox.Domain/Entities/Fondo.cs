using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Fondo documental (RQ01 - RF02). TENANT-SCOPED. Un tenant puede tener MULTIPLES fondos.
///
/// Nunca se elimina: un fondo con dependencias, series o expedientes asociados se Inactiva o
/// se Cierra (invariante 8). Un fondo Cerrado es de SOLO LECTURA: no admite nada nuevo colgando
/// de el, pero se consulta y se exporta sin limite.
/// </summary>
public class Fondo : TenantEntity
{
    /// <summary>Codigo del fondo. UNICO POR TENANT, no global: dos entidades distintas
    /// pueden usar el mismo codigo sin colisionar.</summary>
    public string CodigoFondo { get; set; } = null!;

    public string NombreFondo { get; set; } = null!;

    public string? Descripcion { get; set; }

    /// <summary>
    /// Sede a la que pertenece el fondo. SEMANTICA EXPLICITA DE LA SPEC:
    /// NULL = fondo TRANSVERSAL a toda la entidad (no pertenece a ninguna sede en particular);
    /// con valor = fondo propio de esa sede. No se interpreta null como "sin asignar".
    /// FK NO ACTION: inactivar o borrar una sede jamas arrastra sus fondos.
    /// </summary>
    public long? SedeId { get; set; }
    public Sede? Sede { get; set; }

    public FondoTipo TipoFondo { get; set; } = FondoTipo.Activo;

    public FondoEstado Estado { get; set; } = FondoEstado.Activo;

    public DateOnly FechaApertura { get; set; }

    /// <summary>
    /// Fecha de cierre. OBLIGATORIA si Estado = Cerrado, y debe ser posterior a FechaApertura.
    /// </summary>
    public DateOnly? FechaCierre { get; set; }

    /// <summary>
    /// Entidad liquidada o fusionada de la que proviene el acervo. OBLIGATORIA si
    /// TipoFondo = Acumulado; sin sentido (y por tanto no exigida) en los demas tipos.
    /// </summary>
    public string? EntidadOrigen { get; set; }
}
