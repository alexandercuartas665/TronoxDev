using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Metadato de una asignacion de TRD (RQ02 - RF04 paso 6). TENANT-SCOPED. Define un campo que el
/// usuario debera diligenciar al crear un expediente bajo esta serie en esta dependencia.
///
/// Contexto = Expediente en RF04 (metadatos del expediente). Los metadatos de Documento (contexto
/// Documento) los produce RF05 sobre las tipologias; se modelaran cuando exista RF05.
///
/// Para tipo de dato Lista, referencia una ListaMaestra del Administrador de Listas (RF03) por FK
/// EXPLICITA (ListaMaestraId), en vez del prefijo numerico en VALOR_DEFAULT del legacy.
/// </summary>
public class TrdMetadato : TenantEntity
{
    public long TrdAsignacionId { get; set; }
    public TrdAsignacion? TrdAsignacion { get; set; }

    public string Nombre { get; set; } = null!;

    public TipoDatoMetadato TipoDato { get; set; } = TipoDatoMetadato.TextoCorto;

    public bool Obligatorio { get; set; }

    /// <summary>Posicion del metadato en el formulario del expediente.</summary>
    public int Orden { get; set; }

    /// <summary>Lista que alimenta el desplegable cuando TipoDato = Lista (RF03). Null en el resto.</summary>
    public long? ListaMaestraId { get; set; }
    public ListaMaestra? ListaMaestra { get; set; }

    /// <summary>Momento en que se solicita: Expediente (RF04) o Documento (RF05).</summary>
    public ContextoMetadato Contexto { get; set; } = ContextoMetadato.Expediente;

    public bool IsArchived { get; set; }
}
