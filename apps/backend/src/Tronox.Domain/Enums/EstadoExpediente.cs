namespace Tronox.Domain.Enums;

/// <summary>
/// Eje 1 del ciclo de vida del expediente (RQ03 - RF13): estado de tramite. Ortogonal a la fase de
/// archivo (<see cref="FaseArchivo"/>). Bidireccional Abierto &lt;-&gt; Cerrado (RF08), con permiso y
/// justificacion en la reapertura.
/// </summary>
public enum EstadoExpediente
{
    /// <summary>Acepta incorporacion de documentos. Estado por defecto al crear.</summary>
    Abierto = 0,

    /// <summary>Sellado: indice electronico firmado con hash. No acepta nuevos documentos.</summary>
    Cerrado = 1
}
