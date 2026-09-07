using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Circuito de firma (RQ05 - RF07), calca FIR_CIRCUITOS. Agrupa varios firmantes sobre el mismo
/// documento en modo Secuencial (uno tras otro por orden) o Paralelo (todos a la vez). El sellado
/// final (todas las cajitas) ocurre cuando todos completan. TENANT-SCOPED.
/// </summary>
public class FirmaCircuito : TenantEntity
{
    public long DocumentoId { get; set; }
    public Documento? Documento { get; set; }

    public ModoCircuito Modo { get; set; } = ModoCircuito.Secuencial;
    public EstadoCircuito Estado { get; set; } = EstadoCircuito.Activo;

    public int TotalFirmantes { get; set; }
    public int FirmantesCompletados { get; set; }

    /// <summary>Mas de un tipo de firma distinto entre los firmantes (Electronica/Digital).</summary>
    public bool TipoFirmaMixto { get; set; }

    public bool OtpRequerido { get; set; }

    /// <summary>Solicitante (PlatformUserId) y su nombre (snapshot).</summary>
    public long SolicitantePlatformUserId { get; set; }
    public string? SolicitanteNombre { get; set; }

    /// <summary>Motivo de cancelacion (cuando un firmante rechaza y se cancela todo el circuito).</summary>
    public string? MotivoCancelacion { get; set; }

    public ICollection<FirmaCircuitoFirmante> Firmantes { get; set; } = new List<FirmaCircuitoFirmante>();
}
