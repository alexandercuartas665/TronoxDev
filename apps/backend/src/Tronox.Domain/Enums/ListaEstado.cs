namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de una lista de opciones o de una de sus opciones (RQ02 - RF03).
/// No hay borrado fisico de una lista en uso (RF03 3.3.4-3): se INACTIVA. Inactivar una opcion no
/// afecta los valores ya guardados en expedientes/documentos (RF03 3.3.4-4): se conserva el valor
/// historico, la opcion solo deja de ofrecerse en el desplegable.
/// </summary>
public enum ListaEstado
{
    Activo = 0,
    Inactivo = 1
}
