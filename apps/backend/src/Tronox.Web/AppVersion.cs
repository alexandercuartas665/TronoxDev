using System.Reflection;

namespace Tronox.Web;

/// <summary>
/// Version de la app que se muestra en la UI (pie del sidebar y login) para saber EXACTAMENTE que
/// build esta desplegado. Se estampa en la imagen al construir (build arg TRONOX_VERSION -> env var,
/// tipicamente "gitShortSha-fecha"). Fuera de Docker cae a la version informativa del ensamblado, y si
/// tampoco hay, a "dev".
/// </summary>
public static class AppVersion
{
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var env = Environment.GetEnvironmentVariable("TRONOX_VERSION");
        if (!string.IsNullOrWhiteSpace(env)) { return env.Trim(); }

        var info = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(info) ? "dev" : info!;
    }
}
