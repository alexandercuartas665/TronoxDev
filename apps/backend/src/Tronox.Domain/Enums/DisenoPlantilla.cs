namespace Tronox.Domain.Enums;

/// <summary>Formato de papel de la hoja de la plantilla (RQ04 - RF09).</summary>
public enum FormatoPapel
{
    Carta = 0,
    Oficio = 1,
    A4 = 2
}

/// <summary>Orientacion de la hoja.</summary>
public enum OrientacionPapel
{
    Vertical = 0,
    Horizontal = 1
}

/// <summary>Margenes de la hoja.</summary>
public enum MargenesPapel
{
    Normal = 0,
    Estrecho = 1,
    Amplio = 2
}
