namespace Tronox.Domain.Enums;

/// <summary>
/// Nivel jerarquico de un nodo Cargo del arbol organizacional (RQ01 - RF04, ADR-003).
/// Obligatorio en todo nodo con clasificador Cargo.
///
/// REGLA DE ORO de RF04: el cargo (y por tanto su nivel jerarquico) es METADATO
/// ORGANIZACIONAL, NO un controlador de permisos. Un cargo Directivo no otorga por si
/// mismo ninguna facultad: los permisos vienen UNICAMENTE de la matriz de RF05.
/// </summary>
public enum NivelJerarquico
{
    Directivo,
    Asesor,
    Profesional,
    Tecnico,
    Asistencial
}
