using Microsoft.EntityFrameworkCore;
using Tronox.Application.Archivistica;
using Tronox.Domain.Entities;

namespace Tronox.Infrastructure.Persistence;

/// <summary>
/// Siembra de los NIVELES DE CLASIFICACION DOCUMENTAL de un tenant (RQ01 - RF01-P.3).
///
/// Misma mecanica que MenuProvisioningService y por la misma razon: cuelga del camino de ALTA
/// DEL TENANT (TenantAdminService y OnboardingService), NO de un seeder de demo. Si se sembrara
/// desde un seeder, los clientes creados desde el panel de plataforma nacerian sin niveles y
/// RF05 no tendria contra que resolver roles.nivel_acceso_maximo.
///
/// La definicion canonica de los 4 niveles vive en Application (NivelClasificacionCatalogo), no
/// aqui, para que el aprovisionamiento y los tests no puedan derivar.
///
/// La operacion es IDEMPOTENTE: si el tenant ya tiene al menos un nivel, no hace nada (no
/// reintroduce niveles que el tenant haya renombrado o desactivado).
/// </summary>
public sealed class ClasificacionProvisioningService : IClasificacionProvisioningService
{
    private readonly TronoxDbContext _db;

    public ClasificacionProvisioningService(TronoxDbContext db) => _db = db;

    public async Task EnsureNivelesClasificacionAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        // Se consulta IGNORANDO el filtro global: el alta corre bajo el contexto de la plataforma,
        // no bajo el del tenant que se esta creando, asi que el filtro no aplicaria aqui.
        var yaTieneNiveles = await _db.NivelesClasificacion
            .IgnoreQueryFilters()
            .AnyAsync(n => n.TenantId == tenantId, cancellationToken);
        if (yaTieneNiveles)
        {
            return;
        }

        foreach (var semilla in NivelClasificacionCatalogo.Niveles)
        {
            _db.NivelesClasificacion.Add(new NivelClasificacion
            {
                TenantId = tenantId,
                Nombre = semilla.Nombre,
                Codigo = semilla.Codigo,
                Descripcion = semilla.Descripcion,
                ColorEtiqueta = semilla.ColorEtiqueta,
                NivelOrden = semilla.NivelOrden,
                Activo = true
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
