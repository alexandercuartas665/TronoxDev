namespace Tronox.Domain.Enums;

/// <summary>
/// Accion de la matriz de permisos (RQ01 - RF05). SEIS acciones, no cuatro: la spec exige
/// Ver, Crear, Editar, Eliminar, EXPORTAR e IMPRIMIR, porque en un sistema de gestion
/// documental sacar informacion del sistema (exportar / imprimir) es una operacion sensible
/// que se concede y se audita por separado de "ver".
///
/// Vive en Domain (y no en Application) porque la entidad RolPermiso la persiste como columna:
/// una fila por (modulo, accion). Se guarda como TEXTO (convencion del proyecto), asi que los
/// nombres de los miembros son parte del contrato de datos y del de las policies
/// "Perm:{modulo}:{accion}": NO se renombran ni se reordenan.
/// </summary>
public enum PermissionAction
{
    View = 0,
    Create,
    Edit,
    Delete,
    Export,
    Print
}
