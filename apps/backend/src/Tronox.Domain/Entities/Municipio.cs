using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Municipio / ciudad del catalogo DIVIPOLA. Catalogo GLOBAL de plataforma, sin tenant_id
/// (ver <see cref="Pais"/>).
///
/// <see cref="CodigoDivipola"/> es el codigo COMPLETO de 5 digitos (2 del departamento + 3 del
/// municipio): "11001" Bogota D.C., "05001" Medellin. Es exactamente el valor que RF01 exige en
/// Entidad.CodigoDivipola y el que alimenta la mitad territorial del codigo de fondo AGN, por eso
/// se guarda como texto con ceros a la izquierda y NO como entero.
/// </summary>
public class Municipio : BaseEntity
{
    public long DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }

    /// <summary>Codigo DIVIPOLA completo de 5 digitos. Unico.</summary>
    public string CodigoDivipola { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    /// <summary>Capital del departamento: la UI la ofrece primero.</summary>
    public bool EsCapital { get; set; }

    public bool Activo { get; set; } = true;
}
