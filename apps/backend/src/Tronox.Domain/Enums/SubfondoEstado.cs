namespace Tronox.Domain.Enums;

/// <summary>
/// Estado del subfondo documental (RQ01 - RF02 seccion 5.2.2). Se declara aparte de
/// FondoEstado aunque hoy comparta los mismos valores: son ciclos de vida distintos y el
/// del subfondo puede divergir sin arrastrar al del fondo.
/// </summary>
public enum SubfondoEstado
{
    Activo = 0,
    Inactivo = 1,
    Cerrado = 2
}
