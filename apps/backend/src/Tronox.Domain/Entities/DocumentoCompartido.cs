using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Comparticion interna de un documento (RF07), calca EXP_DOCUMENTOS_COMPARTIDOS del legacy: acceso
/// granular de un documento a un usuario del tenant, con permiso "Ver" siempre incluido mas los
/// opcionales "Editar metadatos" y "Descargar". Compartir con un ROL expande a sus usuarios en el
/// momento (snapshot: <see cref="OrigenRolId"/> guarda de que rol salio; altas posteriores al rol NO se
/// propagan). Revocar NUNCA borra: marca <see cref="Activo"/>=false con fecha/actor (soft-delete).
/// El beneficiario es un PlatformUser (mismo espacio de id que ITenantContext.UserId). TENANT-SCOPED.
/// </summary>
public class DocumentoCompartido : TenantEntity
{
    public long DocumentoId { get; set; }
    public Documento? Documento { get; set; }

    /// <summary>Beneficiario del acceso: PlatformUserId (mismo id que el usuario logueado).</summary>
    public long BeneficiarioPlatformUserId { get; set; }

    /// <summary>"Ver" es la base de toda comparticion (calca el legacy: se incluye siempre).</summary>
    public bool PuedeVer { get; set; } = true;
    public bool PuedeEditarMetadatos { get; set; }
    public bool PuedeDescargar { get; set; }

    /// <summary>Rol del que se expandio esta comparticion (snapshot). Null = comparticion directa a usuario.</summary>
    public long? OrigenRolId { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>Quien revoco (PlatformUserId) y cuando. Null mientras esta activa.</summary>
    public long? RevocadoPor { get; set; }
    public DateTime? FechaRevocado { get; set; }
}
