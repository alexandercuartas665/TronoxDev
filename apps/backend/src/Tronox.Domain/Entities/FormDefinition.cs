using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Definicion de un formulario dinamico (RQ08, port del backbone ECOREX / ADR-0015). El arbol
/// contenedores -> preguntas cuelga por DefinitionId; las respuestas se guardan como documento JSON
/// por respuesta (<see cref="FormResponse"/>), no como filas EAV (ADR-011). TENANT-SCOPED,
/// concurrencia optimista portable (<see cref="Version"/>).
///
/// La version DE NEGOCIO del formulario es <see cref="Revision"/>; <see cref="Version"/> (long) es el
/// token de concurrencia de IVersioned que incrementa el interceptor. No comparten nombre a proposito.
/// </summary>
public class FormDefinition : TenantEntity, IVersioned
{
    /// <summary>Codigo legible unico por tenant (ej. "FRM-001").</summary>
    public string Code { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// Version de negocio: arranca en 1 y se incrementa al guardar cambios estructurales
    /// (contenedores/preguntas) sobre una definicion Active.
    /// </summary>
    public int Revision { get; set; } = 1;

    public FormStatus Status { get; set; } = FormStatus.Draft;

    /// <summary>Soft-archive: fuera de las listas por defecto, conserva historia (invariante 8).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Token de concurrencia optimista portable (lo incrementa el interceptor).</summary>
    public long Version { get; set; }

    // ---- Transaccionalidad (Formularios avanzados, ola F3) ----

    /// <summary>Si es true, cada envio confirmado es un REGISTRO (hecho) con identidad, estado y fecha.</summary>
    public bool IsTransactional { get; set; }

    /// <summary>Como produce la identidad el registro (ninguna / clave natural / consecutivo).</summary>
    public FormIdentityMode IdentityMode { get; set; } = FormIdentityMode.None;

    /// <summary>NaturalKey: campo del formulario cuyo valor es el numero/clave del registro.</summary>
    public string? IdentitySourceFieldCode { get; set; }

    /// <summary>NaturalKey: codigos de campo que forman la clave unica por tenant (arreglo JSON).</summary>
    public string? UniqueKeyFieldsJson { get; set; }

    /// <summary>Sequence: referencia logica a la secuencia del tenant que se consume al confirmar.</summary>
    public long? SequenceId { get; set; }

    // ---- Formulario como MODULO del sistema (ola F4) ----

    /// <summary>Si es true, el formulario es un modulo con nodo de menu propio y bandeja en /m/{code}.</summary>
    public bool IsModule { get; set; }

    /// <summary>Nodo de menu generado al promover a modulo (el usuario elige DONDE colgarlo). Null si no es modulo.</summary>
    public long? ModuleMenuNodeId { get; set; }

    /// <summary>Icono del modulo en el menu (clave de icono Bootstrap Icons).</summary>
    public string? ModuleIcon { get; set; }

    /// <summary>Columnas de la bandeja del modulo (arreglo JSON de field codes). Null = por defecto.</summary>
    public string? ListColumnsJson { get; set; }

    /// <summary>Campos de filtro de la bandeja (arreglo JSON de field codes).</summary>
    public string? FilterFieldsJson { get; set; }

    /// <summary>
    /// Ancho de la tarjeta al llenar el formulario (Normal/Ancho/Completo). Util para formularios con
    /// tablas anchas. Default Normal.
    /// </summary>
    public FormCardLayout CardLayout { get; set; } = FormCardLayout.Normal;

    /// <summary>Arbol de contenedores del formulario (segmentos, filas, tabs...).</summary>
    public ICollection<FormContainer> Containers { get; set; } = new List<FormContainer>();

    /// <summary>Preguntas (campos) del formulario.</summary>
    public ICollection<FormQuestion> Questions { get; set; } = new List<FormQuestion>();
}
