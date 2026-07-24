using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Departamento (o entidad territorial de primer nivel) del catalogo DIVIPOLA. Catalogo GLOBAL
/// de plataforma, sin tenant_id (ver <see cref="Pais"/>).
///
/// El <see cref="CodigoDane"/> son los DOS primeros digitos del codigo DIVIPOLA de 5 posiciones:
/// "11" Bogota D.C., "05" Antioquia, "76" Valle del Cauca. Los municipios cuelgan de aqui, y el
/// selector de la UI se filtra por <see cref="PaisId"/> (selectores encadenados, criterio 5 de RF01).
/// </summary>
public class Departamento : BaseEntity
{
    public long PaisId { get; set; }
    public Pais? Pais { get; set; }

    /// <summary>Codigo DANE de 2 digitos, con cero a la izquierda ("05", no "5").</summary>
    public string CodigoDane { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public bool Activo { get; set; } = true;
}
