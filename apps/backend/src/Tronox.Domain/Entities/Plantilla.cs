using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Plantilla documental (RQ04 - RF09): documento parametrizado con editor de texto y variables
/// {{...}}, asociado a N tipologias (tipos documentales). Es CONFIGURACION, no produccion: se consume
/// al crear un documento (RF10, diferido). TENANT-SCOPED. Sin borrado fisico: se inactiva (invariante 8).
/// </summary>
public class Plantilla : TenantEntity
{
    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    /// <summary>Contenido del editor (HTML) con variables {{...}}. Se combina al crear el documento.</summary>
    public string? ContenidoHtml { get; set; }

    /// <summary>Tipologia "representante" (la primera asociada), para la galeria sin joins.</summary>
    public long? TrdTipologiaId { get; set; }
    public TrdTipologia? TrdTipologia { get; set; }

    // ---- Diseno de hoja (RF09) ----
    public FormatoPapel FormatoPapel { get; set; } = FormatoPapel.Carta;
    public OrientacionPapel Orientacion { get; set; } = OrientacionPapel.Vertical;
    public MargenesPapel Margenes { get; set; } = MargenesPapel.Normal;

    /// <summary>Clave del preset de encabezado/pie (opcional).</summary>
    public string? Encabezado { get; set; }
    public string? PiePagina { get; set; }

    /// <summary>Conteo informativo de variables distintas detectadas en el contenido.</summary>
    public int VariablesNum { get; set; }

    public PlantillaEstado Estado { get; set; } = PlantillaEstado.Activa;

    /// <summary>Veces que la plantilla se ha usado para crear un documento (RF10). Alimenta el badge.</summary>
    public int UsoContador { get; set; }

    /// <summary>Tipologias asociadas (N:N). Un tipo puede tener N plantillas y viceversa (RF09).</summary>
    public ICollection<PlantillaTipo> Tipos { get; set; } = [];
}
