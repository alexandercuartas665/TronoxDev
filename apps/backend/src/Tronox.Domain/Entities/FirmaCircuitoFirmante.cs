using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Firmante ordenado dentro de un circuito (RQ05 - RF07), calca FIR_CIRCUITO_FIRMANTES. Cuando esta
/// activo (Pendiente) tiene una <see cref="Firma"/> asociada que aparece en su bandeja "Mis Firmas".
/// TENANT-SCOPED.
/// </summary>
public class FirmaCircuitoFirmante : TenantEntity
{
    public long CircuitoId { get; set; }
    public FirmaCircuito? Circuito { get; set; }

    public int Orden { get; set; }

    /// <summary>Firmante (PlatformUserId).</summary>
    public long FirmantePlatformUserId { get; set; }
    public string NombreFirmante { get; set; } = string.Empty;
    public string? CargoFirmante { get; set; }

    public TipoFirma TipoFirma { get; set; } = TipoFirma.Electronica;
    public EstadoCircuitoFirmante Estado { get; set; } = EstadoCircuitoFirmante.EnEspera;

    /// <summary>Solicitud de firma (Firma) creada cuando el firmante queda activo. Null mientras En_Espera.</summary>
    public long? FirmaId { get; set; }

    public DateTimeOffset? TimestampFirma { get; set; }
}
