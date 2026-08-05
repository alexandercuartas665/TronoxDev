using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Definicion de un formulario dinamico (RQ08, port del backbone ECOREX / ADR-0015). El arbol
/// contenedores -> preguntas cuelga por DefinitionId; las respuestas se guardan como documento JSON
/// por respuesta (<see cref="FormResponse"/>), no como filas EAV (ver ADR de TRONOX). TENANT-SCOPED,
/// concurrencia optimista portable (<see cref="Version"/>).
///
/// La version DE NEGOCIO del formulario es <see cref="Revision"/>; <see cref="Version"/> (long) es el
/// token de concurrencia de IVersioned que incrementa el interceptor. No comparten nombre a proposito.
///
/// Primer slice: se portan el catalogo, el diseñador y la captura. Se difieren transaccionalidad,
/// formulario-como-modulo, publicacion por token, logica condicional, lookups y maestro-detalle.
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

    /// <summary>Arbol de contenedores del formulario (segmentos, filas, tabs...).</summary>
    public ICollection<FormContainer> Containers { get; set; } = [];

    /// <summary>Preguntas (campos) del formulario.</summary>
    public ICollection<FormQuestion> Questions { get; set; } = [];
}
