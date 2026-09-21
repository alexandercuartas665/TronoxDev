using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Enums;

namespace Tronox.Application.Firmas;

/// <summary>
/// Verificacion publica de firma (RQ05 - RF04). Consulta cross-tenant (IgnoreQueryFilters) por id de
/// documento y devuelve solo datos probatorios. Nunca toca el binario. Superficie publica: no recibe
/// actor ni tenant.
/// </summary>
public sealed class VerificacionFirmaService : IVerificacionFirmaService
{
    private readonly IApplicationDbContext _db;

    public VerificacionFirmaService(IApplicationDbContext db) => _db = db;

    public async Task<VerificacionResultado?> VerificarAsync(long docId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documentos.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.Id == docId && !d.EsVersionHistorica)
            .Select(d => new { d.Id, d.Nombre, d.EstadoFirma, d.HashSha256, d.TenantId })
            .FirstOrDefaultAsync(cancellationToken);
        if (doc is null) { return null; }

        var entidad = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Id == doc.TenantId).Select(t => t.Name).FirstOrDefaultAsync(cancellationToken);

        var firmantes = await _db.Firmas.AsNoTracking().IgnoreQueryFilters()
            .Where(f => f.DocumentoId == docId && f.Estado == EstadoFirma.Firmado)
            .OrderBy(f => f.TimestampFirma)
            .Select(f => new VerificacionFirmanteDto(f.NombreFirmante, f.TimestampFirma, f.TipoFirma.ToString()))
            .ToListAsync(cancellationToken);

        return new VerificacionResultado(
            DocumentoId: doc.Id,
            Entidad: entidad ?? "",
            DocumentoNombre: doc.Nombre,
            Firmado: doc.EstadoFirma == EstadoFirmaDocumento.Firmado,
            EstadoFirma: doc.EstadoFirma.ToString(),
            HashRegistrado: doc.HashSha256,
            Firmantes: firmantes);
    }
}
