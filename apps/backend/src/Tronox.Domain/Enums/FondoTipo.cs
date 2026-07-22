namespace Tronox.Domain.Enums;

/// <summary>
/// Naturaleza archivistica del fondo documental (RQ01 - RF02).
/// </summary>
public enum FondoTipo
{
    /// <summary>Fondo vivo de la entidad productora actual.</summary>
    Activo = 0,

    /// <summary>
    /// Fondo acumulado: proviene de una entidad liquidada o fusionada. En este tipo
    /// EntidadOrigen es OBLIGATORIA (nombre de la entidad de la que proviene el acervo).
    /// </summary>
    Acumulado = 1,

    /// <summary>Fondo transferido a otra entidad o al archivo general.</summary>
    Transferido = 2
}
