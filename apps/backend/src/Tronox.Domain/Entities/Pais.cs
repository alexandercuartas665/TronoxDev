using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Pais del catalogo territorial (ISO 3166-1). Catalogo GLOBAL de plataforma, igual que
/// <see cref="ModuleDefinition"/>: NO es tenant-scoped y NO recibe query filter (DAT-01 aplica
/// a datos del tenant, no a una tabla de referencia nacional que seria identica en los N
/// tenants). Se siembra por migracion; ningun tenant lo edita.
///
/// Resuelve el pendiente P-02 de RQ01 en su rama "se precarga en BD" (no API DANE en vivo).
/// </summary>
public class Pais : BaseEntity
{
    /// <summary>Codigo ISO 3166-1 alfa-2 (ej. "CO"). Unico.</summary>
    public string CodigoIso2 { get; set; } = null!;

    /// <summary>Codigo ISO 3166-1 alfa-3 (ej. "COL").</summary>
    public string CodigoIso3 { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public bool Activo { get; set; } = true;
}
