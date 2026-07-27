using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Opcion de una lista reutilizable (RQ02 - RF03 3.3.2). TENANT-SCOPED. Cuelga de una ListaMaestra.
///
/// La CLAVE es el valor interno (ej. "LIBRE_REMOCION"); el VALOR es lo que ve el usuario en el
/// desplegable (ej. "Libre Remocion"). El ORDEN define la posicion en el desplegable y es editable
/// (drag and drop, RF03 3.3.4-5). Inactivar una opcion no borra los valores ya guardados en
/// expedientes/documentos (RF03 3.3.4-4): solo deja de ofrecerse.
/// </summary>
public class ListaOpcion : TenantEntity
{
    /// <summary>Lista a la que pertenece la opcion.</summary>
    public long ListaMaestraId { get; set; }
    public ListaMaestra? ListaMaestra { get; set; }

    /// <summary>Valor interno. Ej: "LIBRE_REMOCION". UNICO DENTRO DE LA LISTA.</summary>
    public string Clave { get; set; } = null!;

    /// <summary>Valor visible al usuario. Ej: "Libre Remocion".</summary>
    public string Valor { get; set; } = null!;

    /// <summary>Posicion en el desplegable (editable por drag and drop).</summary>
    public int Orden { get; set; }

    public ListaEstado Estado { get; set; } = ListaEstado.Activo;
}
