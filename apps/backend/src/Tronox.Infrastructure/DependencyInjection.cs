using Tronox.Application.Common;
using Tronox.Application.Common.Auth;
using Tronox.Infrastructure.Auth;
using Tronox.Infrastructure.Persistence;
using Tronox.Infrastructure.Persistence.Interceptors;
using Tronox.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tronox.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("TRONOX_DB_CONNECTION");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Cadena de conexion 'Default' no configurada (usa ConnectionStrings:Default o TRONOX_DB_CONNECTION).");
        }

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditableTenantInterceptor>();

        // PostgreSQL es el UNICO motor de TRONOX (las 17 specs lo fijan). Se descarta el DAL dual
        // del backbone y su matriz de tests: un solo proveedor, una sola cadena de migraciones.
        services.AddDbContext<TronoxDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention()
                   .AddInterceptors(sp.GetRequiredService<AuditableTenantInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<TronoxDbContext>());

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        // Llaves de Data Protection compartidas en la base de datos + nombre de aplicacion comun,
        // para que cualquier app (Api, Web, Workers) descifre los secretos cifrados por otra.
        services.AddDataProtection()
            .SetApplicationName("Tronox")
            .PersistKeysToDbContext<TronoxDbContext>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        // Correo saliente via SMTP configurable por tenant, con la clave cifrada (RQ01 RF01-P.2).
        services.AddScoped<Application.Common.IEmailSender, Email.SmtpEmailSender>();

        // Gateway de IA multi-proveedor (base de RQ16).
        services.AddHttpClient<Tronox.Application.Tenancy.IAiProviderClient, Ai.AiProviderClient>();
        services.AddHttpClient<Tronox.Application.Auth.IGoogleOAuthClient, Auth.GoogleOAuthClient>();

        // Aprovisionamiento del menu canonico por tenant (RF09 5.9.4). Cuelga del ALTA de tenant,
        // no de un seeder de demo: ningun cliente puede nacer sin menu (ver ADR-001).
        services.AddScoped<Tronox.Application.MenuConfig.IMenuProvisioningService, MenuProvisioningService>();

        // Comprobantes PDF (QuestPDF). Licencia Community: gratis para empresas con ingresos < USD 1M/ano.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.AddScoped<Application.Common.IReceiptPdfRenderer, Pdf.QuestPdfReceiptRenderer>();

        return services;
    }
}
