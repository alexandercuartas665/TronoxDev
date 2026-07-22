using Ecorex.Application.Common;
using Ecorex.Application.Common.Auth;
using Ecorex.Infrastructure.Auth;
using Ecorex.Infrastructure.Persistence;
using Ecorex.Infrastructure.Persistence.Interceptors;
using Ecorex.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecorex.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("ECOREX_DB_CONNECTION");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Cadena de conexion 'Default' no configurada (usa ConnectionStrings:Default o ECOREX_DB_CONNECTION).");
        }

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditableTenantInterceptor>();

        // PostgreSQL es el UNICO motor de TRONOX (las 17 specs lo fijan). Se descarta el DAL dual
        // del backbone y su matriz de tests: un solo proveedor, una sola cadena de migraciones.
        services.AddDbContext<EcorexDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention()
                   .AddInterceptors(sp.GetRequiredService<AuditableTenantInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<EcorexDbContext>());

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        // Llaves de Data Protection compartidas en la base de datos + nombre de aplicacion comun,
        // para que cualquier app (Api, Web, Workers) descifre los secretos cifrados por otra.
        services.AddDataProtection()
            .SetApplicationName("Ecorex")
            .PersistKeysToDbContext<EcorexDbContext>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        // Correo saliente via SMTP configurable por tenant, con la clave cifrada (RQ01 RF01-P.2).
        services.AddScoped<Application.Common.IEmailSender, Email.SmtpEmailSender>();

        // Gateway de IA multi-proveedor (base de RQ16).
        services.AddHttpClient<Ecorex.Application.Tenancy.IAiProviderClient, Ai.AiProviderClient>();
        services.AddHttpClient<Ecorex.Application.Auth.IGoogleOAuthClient, Auth.GoogleOAuthClient>();

        // Aprovisionamiento del menu canonico por tenant (RF09 5.9.4). Cuelga del ALTA de tenant,
        // no de un seeder de demo: ningun cliente puede nacer sin menu (ver ADR-001).
        services.AddScoped<Ecorex.Application.MenuConfig.IMenuProvisioningService, MenuProvisioningService>();

        // Comprobantes PDF (QuestPDF). Licencia Community: gratis para empresas con ingresos < USD 1M/ano.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.AddScoped<Application.Common.IReceiptPdfRenderer, Pdf.QuestPdfReceiptRenderer>();

        return services;
    }
}
