using Microsoft.EntityFrameworkCore;
using Tronox.Application.Archivistica;
using Tronox.Infrastructure.Persistence;

namespace Tronox.Integration.Tests;

/// <summary>
/// Utilidades compartidas por los tests de roles (RQ01 - RF05).
///
/// Todo rol necesita un nivel_acceso_maximo (FK OBLIGATORIO a niveles_clasificacion), asi que
/// cualquier test que cree un rol tiene que sembrar antes los niveles del tenant. Se hace con el
/// aprovisionamiento REAL (ClasificacionProvisioningService) para no inventar aqui una definicion
/// paralela de los niveles que pudiera derivar de la canonica.
/// </summary>
public static class RolesTestHelpers
{
    /// <summary>Siembra los 4 niveles del tenant (idempotente) y devuelve el id del codigo pedido.</summary>
    public static async Task<long> SeedNivelesYObtenerIdAsync(
        TronoxDbContext ctx, long tenantId, string codigo = "01")
    {
        await new ClasificacionProvisioningService(ctx).EnsureNivelesClasificacionAsync(tenantId);

        return await ctx.NivelesClasificacion
            .IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId && n.Codigo == codigo)
            .Select(n => n.Id)
            .FirstAsync();
    }

    /// <summary>Id del nivel ya sembrado (no siembra). Falla si el tenant no tiene niveles.</summary>
    public static Task<long> NivelIdAsync(TronoxDbContext ctx, long tenantId, string codigo) =>
        ctx.NivelesClasificacion
            .IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId && n.Codigo == codigo)
            .Select(n => n.Id)
            .FirstAsync();

    /// <summary>Codigos de los 4 niveles canonicos, por comodidad y para no repetir literales.</summary>
    public const string NivelPublico = "01";
    public const string NivelInterno = "02";
    public const string NivelReservado = "03";
    public const string NivelClasificado = "04";
}
