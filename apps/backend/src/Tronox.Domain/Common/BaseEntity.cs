namespace Tronox.Domain.Common;

/// <summary>
/// Raiz de todas las entidades. Id es BIGINT de identidad: lo genera la base de datos
/// al insertar (ValueGeneratedOnAdd), no la aplicacion. Antes de SaveChanges vale 0.
/// Los campos de auditoria los gestiona el interceptor de SaveChanges, no el codigo de negocio.
/// </summary>
public abstract class BaseEntity
{
    public long Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
}
