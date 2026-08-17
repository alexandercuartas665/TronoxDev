using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Historial de ubicacion fisica de un expediente (RQ03 - RF12, legacy EXP_EXPEDIENTES_UBICACION).
/// APPEND-ONLY: cada asignacion o cambio agrega una fila; la ubicacion ACTUAL es la mas reciente.
/// Referencia un nodo real del arbol de topografia (RQ02 - RF06). TENANT-SCOPED.
/// </summary>
public class ExpedienteUbicacion : TenantEntity
{
    public long ExpedienteId { get; set; }
    public Expediente? Expediente { get; set; }

    /// <summary>Nodo de topografia (Bodega/Estante/... ) donde queda ubicado. FK TopografiaElemento.</summary>
    public long TopografiaElementoId { get; set; }
    public TopografiaElemento? TopografiaElemento { get; set; }

    /// <summary>Fase del ciclo vital en la que se asigno la ubicacion.</summary>
    public FaseArchivo Fase { get; set; } = FaseArchivo.Gestion;

    public string? Observacion { get; set; }
}
