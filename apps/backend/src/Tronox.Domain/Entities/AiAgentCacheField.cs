using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Definicion de un dato que el agente debe ir capturando durante la conversacion (RQ16, datos cache).
/// Entidad TENANT-SCOPED. Ej.: "identificacion", "nombre", "celular". El tenant lo configura desde el
/// agente y el motor de inferencia luego intenta llenarlo a partir del texto entrante.
/// </summary>
public class AiAgentCacheField : TenantEntity
{
    public long AgentId { get; set; }
    public AiAgent? Agent { get; set; }

    /// <summary>Clave del dato (slug derivado del nombre). Unica por agente.</summary>
    public string FieldKey { get; set; } = null!;

    /// <summary>Nombre visible del dato (ej. "Identificacion").</summary>
    public string Label { get; set; } = null!;

    /// <summary>De que trata el dato; le sirve al motor para extraerlo del texto entrante.</summary>
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Si esta en true, el motor de inferencia puede sobrescribir el valor durante la conversacion. Si
    /// esta en false, una vez capturado queda fijo y no se actualiza (ej. tipo_cliente, idContacto).
    /// </summary>
    public bool IsUpdatable { get; set; } = true;
}
