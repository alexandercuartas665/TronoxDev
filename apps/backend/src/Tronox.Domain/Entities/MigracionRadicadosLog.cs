using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Registro de trazabilidad de una corrida de migracion de radicados historicos (RQ09 RF01-6):
/// usuario, fecha, cantidad y reporte de exitosos/errores. El proceso de importacion asincrono real
/// (parseo del Excel + alta) es una integracion posterior; aqui vive el log/plantilla. TENANT-SCOPED.
/// </summary>
public class MigracionRadicadosLog : TenantEntity
{
    public DateTimeOffset FechaMigracion { get; set; }

    /// <summary>Usuario que ejecuto la migracion (TenantUser).</summary>
    public long? EjecutadaPorTenantUserId { get; set; }

    public string? ArchivoNombre { get; set; }

    public int CantidadTotal { get; set; }
    public int CantidadExitosos { get; set; }
    public int CantidadErrores { get; set; }

    /// <summary>Estado destino de los radicados importados: "Respondido" o "Archivado" (nunca activo).</summary>
    public string EstadoDestino { get; set; } = "Archivado";

    /// <summary>Estado de la corrida: "Procesando" / "Completado" / "Error".</summary>
    public string Estado { get; set; } = "Procesando";

    /// <summary>Reporte de errores por fila (JSON).</summary>
    public string? ReporteJson { get; set; }
}
