using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Lista de opciones reutilizable (RQ02 - RF03). TENANT-SCOPED. Es la FUENTE DE VALORES de un
/// metadato de tipo Lista en series, subseries o tipos documentales (RF04/RF05).
///
/// Regla de diseno: NO es un banco de metadatos global; su unico proposito es proveer las opciones
/// de un desplegable. No se elimina una lista en uso por metadatos activos (RF03 3.3.4-3): se
/// inactiva. Equivale a la tabla legacy de doc_adminlistas.aspx.
/// </summary>
public class ListaMaestra : TenantEntity
{
    /// <summary>Nombre de la lista. Ej: "Tipo de Vinculacion". UNICO POR TENANT (RF03 3.3.4-1).</summary>
    public string NombreLista { get; set; } = null!;

    public string? Descripcion { get; set; }

    public ListaEstado Estado { get; set; } = ListaEstado.Activo;

    public ICollection<ListaOpcion> Opciones { get; set; } = [];
}
