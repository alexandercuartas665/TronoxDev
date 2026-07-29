namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de un elemento topografico del archivo fisico (RQ02 - RF06).
/// No hay borrado fisico de un elemento con contenido: se INACTIVA. Un elemento Lleno o Inactivo
/// no admite asignar expedientes (RF06 3.6.6-4). El paso a Lleno es automatico cuando el elemento
/// que controla capacidad alcanza su maximo (RF06 3.6.6-5).
/// </summary>
public enum TopografiaEstado
{
    Disponible = 0,
    Lleno = 1,
    Inactivo = 2
}
