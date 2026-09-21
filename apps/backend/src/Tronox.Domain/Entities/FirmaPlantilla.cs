using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Plantilla de firma reutilizable (RQ05 - TRON-20, legacy FIR_PLANTILLAS). Guarda la configuracion del
/// disenador de "Solicitar Firma / Circuito" (modo + OTP + firmantes) como JSON, para precargarla luego.
/// Baja logica con Activo (invariante 8). TENANT-SCOPED.
/// </summary>
public class FirmaPlantilla : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Modo del circuito: "Secuencial" | "Paralelo".</summary>
    public string Modo { get; set; } = "Secuencial";
    public bool OtpRequerido { get; set; }
    public int TotalFirmantes { get; set; }

    /// <summary>Resumen legible (nombres de los firmantes) para la lista.</summary>
    public string? Resumen { get; set; }

    /// <summary>Config del disenador en JSON: { modo, otp, firmantes:[{uid,nombre,tipo}] }.</summary>
    public string ConfigJson { get; set; } = "{}";

    public bool Activo { get; set; } = true;
}
