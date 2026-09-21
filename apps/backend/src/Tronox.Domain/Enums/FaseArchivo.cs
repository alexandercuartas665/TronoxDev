namespace Tronox.Domain.Enums;

/// <summary>
/// Eje 2 del ciclo de vida del expediente (RQ03 - RF13): fase del ciclo vital del archivo. Avance
/// UNIDIRECCIONAL Gestion -&gt; Central -&gt; Historico via actas de transferencia (RF14). No reversible.
/// Central/Historico solo con el submodulo de Transferencias activo (diferido a un slice posterior).
/// </summary>
public enum FaseArchivo
{
    /// <summary>Archivo de gestion: expediente en produccion en su dependencia. Fase por defecto.</summary>
    Gestion = 0,

    /// <summary>Archivo central: transferido tras cumplir el tiempo de gestion.</summary>
    Central = 1,

    /// <summary>Archivo historico: conservacion permanente (disposicion CT).</summary>
    Historico = 2
}
