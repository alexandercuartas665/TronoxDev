using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Asiento de cierre / reapertura de un expediente (RQ03 - RF08, legacy EXP_EXPEDIENTES_FIRMAS).
/// APPEND-ONLY: cada cierre y cada reapertura agregan una fila; nunca se modifica ni borra (RNF-04).
/// Al cerrar se calcula un hash SHA-256 del indice del expediente (integridad); la firma criptografica
/// real la aporta RQ05 despues (por eso <see cref="FirmaDigitalId"/> queda null hasta entonces).
/// TENANT-SCOPED.
/// </summary>
public class ExpedienteCierre : TenantEntity
{
    public long ExpedienteId { get; set; }
    public Expediente? Expediente { get; set; }

    /// <summary>Numero consecutivo del cierre para ese expediente (1, 2, ...).</summary>
    public int NumeroCierre { get; set; }

    /// <summary>Hash SHA-256 (hex) del indice serializado al momento del cierre. Integridad.</summary>
    public string HashSha256 { get; set; } = null!;

    /// <summary>Id de la firma digital (RQ05). Null mientras el modulo de firma no exista.</summary>
    public string? FirmaDigitalId { get; set; }

    /// <summary>Null = asiento de CIERRE; con valor = asiento de REAPERTURA con su justificacion.</summary>
    public string? JustificacionReapertura { get; set; }
}
