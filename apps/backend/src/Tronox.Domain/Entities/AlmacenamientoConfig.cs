using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Configuracion del almacenamiento de binarios (Azure Blob Storage) POR ENTIDAD (RQ01, ADR-012 sobre
/// ADR-009). Una fila por tenant. La cadena de conexion es un SECRETO: se guarda cifrada (AES-256 via
/// ISecretProtector), nunca en claro (CLAUDE.md seccion 5). Si no hay config activa, el object storage
/// cae al proveedor global (Azurite/env). TENANT-SCOPED.
/// </summary>
public class AlmacenamientoConfig : TenantEntity
{
    /// <summary>Cadena de conexion de Azure Storage, CIFRADA. Null = no configurada.</summary>
    public string? ConnectionStringCifrada { get; set; }

    /// <summary>Contenedor donde viven los binarios de documentos del tenant.</summary>
    public string Contenedor { get; set; } = "tronox-documentos";

    /// <summary>Prefijo opcional (carpeta virtual) que antecede a la key de cada objeto.</summary>
    public string? Prefijo { get; set; }

    /// <summary>Si esta activa, el object storage del tenant usa esta cuenta; si no, cae al global.</summary>
    public bool Activo { get; set; }
}
