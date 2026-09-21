using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Metadato de una asignacion de TRD. TENANT-SCOPED. Define un campo que el usuario debera
/// diligenciar segun su contexto:
///   - Contexto = Expediente (RF04 paso 6): al CREAR un expediente bajo esta serie en esta
///     dependencia. TrdTipologiaId es null (cuelga directo de la asignacion).
///   - Contexto = Documento (RF05 3.5.3): al CARGAR un documento de una tipologia. TrdTipologiaId
///     apunta a la TrdTipologia (equivale al TRD_TIPOLOGIA_REG del legacy).
///
/// Para tipo de dato Lista, referencia una ListaMaestra del Administrador de Listas (RF03) por FK
/// EXPLICITA (ListaMaestraId), en vez del prefijo numerico en VALOR_DEFAULT del legacy.
/// </summary>
public class TrdMetadato : TenantEntity
{
    public long TrdAsignacionId { get; set; }
    public TrdAsignacion? TrdAsignacion { get; set; }

    /// <summary>
    /// Tipologia a la que pertenece cuando Contexto = Documento (RF05). Null cuando Contexto =
    /// Expediente (RF04), en cuyo caso el metadato cuelga directamente de la asignacion.
    /// </summary>
    public long? TrdTipologiaId { get; set; }
    public TrdTipologia? TrdTipologia { get; set; }

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
