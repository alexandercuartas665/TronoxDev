namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de una version de la Tabla de Retencion Documental (RQ02 - RF01 3.1.2).
///
/// Transiciones validas:
///   EnConstruccion -> Vigente     (accion manual del administrador; voltea la Vigente anterior a Historico)
///   Vigente        -> Historico   (AUTOMATICO al activar otra version; nunca accion directa del usuario)
///   EnConstruccion -> Inactivo    (accion manual: descartar sin reemplazar)
///
/// Regla critica (RF01 3.1.4-2): solo puede existir UNA version Vigente por tenant a la vez.
/// Historico e Inactivo son terminales y de solo consulta. Nunca hay borrado fisico (invariante 8).
/// </summary>
public enum TrdVersionEstado
{
    /// <summary>TRD en proceso de configuracion. Editable libremente.</summary>
    EnConstruccion = 0,

    /// <summary>TRD aprobada y activa. Unica por tenant.</summary>
    Vigente = 1,

    /// <summary>Version anterior reemplazada automaticamente al activar una nueva. Solo consulta.</summary>
    Historico = 2,

    /// <summary>Version descartada sin reemplazar. Solo consulta.</summary>
    Inactivo = 3
}
