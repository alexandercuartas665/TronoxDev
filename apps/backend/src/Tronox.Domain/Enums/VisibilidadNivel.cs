namespace Tronox.Domain.Enums;

/// <summary>
/// Nivel de visibilidad de radicados de un usuario (RF11-8, RAD_PERMISOS_VISIBILIDAD.NIVEL).
/// Propios = solo los que radico/le asignaron/origino; Dependencia = los de su dependencia (subiendo
/// el arbol OrgUnit) mas los propios; Todos = todos los del tenant. En TRONOX es un tightening ADITIVO
/// sobre el permiso del modulo (que es el gate fail-closed); un error de resolucion cae en Propios,
/// NUNCA en Todos (a diferencia del legacy fail-open).
/// </summary>
public enum VisibilidadNivel
{
    Propios,
    Dependencia,
    Todos
}
