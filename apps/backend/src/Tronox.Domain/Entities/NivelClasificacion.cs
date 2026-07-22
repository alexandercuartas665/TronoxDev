using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Nivel de clasificacion documental del tenant (RQ01 - RF01-P.3). TENANT-SCOPED.
///
/// Los 4 niveles canonicos (Publico / Interno / Reservado / Clasificado) se siembran al CREAR
/// el tenant, no desde un seeder de demo: ver IClasificacionProvisioningService. El tenant puede
/// desactivarlos (Activo=false) pero nunca se borran fisicamente (invariante 8).
///
/// Esta tabla es la que referenciara roles.nivel_acceso_maximo (RF05): un rol solo puede ver
/// documentos cuyo NivelOrden sea menor o igual al maximo que tiene concedido. Por eso el orden
/// es un entero comparable y no un simple texto.
/// </summary>
public class NivelClasificacion : TenantEntity
{
    public string Nombre { get; set; } = null!;

    /// <summary>Codigo corto y estable del nivel ("01".."04"). Unico por tenant.</summary>
    public string Codigo { get; set; } = null!;

    public string? Descripcion { get; set; }

    /// <summary>Color HEX de la etiqueta visual, formato #RRGGBB.</summary>
    public string? ColorEtiqueta { get; set; }

    /// <summary>
    /// Orden de restriccion: 1 = MENOR restriccion (Publico), 4 = MAYOR (Clasificado).
    /// Unico por tenant: dos niveles no pueden competir por el mismo escalon.
    /// </summary>
    public int NivelOrden { get; set; }

    public bool Activo { get; set; } = true;
}
