namespace Tronox.Domain.Enums;

/// <summary>
/// Prioridad de atencion del radicado. Espejo del legacy RAD_RADICADOS.PRIORIDAD. Los valores exactos
/// del legacy se reconcilian al portar el modulo de radicar (rad_radicar); por ahora el panel solo lee.
/// </summary>
public enum RadicadoPrioridad
{
    Normal,
    Alta,
    Urgente
}
