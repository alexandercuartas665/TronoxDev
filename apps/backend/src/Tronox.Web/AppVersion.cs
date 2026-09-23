using System.Reflection;

namespace Tronox.Web;

/// <summary>
/// Version del sistema para la UI. <see cref="Current"/> es el semver legible (ej. "0.1.0"), fuente
/// unica en Tronox.Web.csproj (&lt;Version&gt;). <see cref="Build"/> es el identificador exacto del
/// build (ej. "b08678e-20260923"), estampado en la imagen al construir (build arg TRONOX_VERSION -&gt;
/// env var); sirve de tooltip para trazabilidad. Fuera de Docker <see cref="Build"/> es null.
/// </summary>
public static class AppVersion
{
    /// <summary>Semver legible, ej. "0.1.0" (de &lt;Version&gt; del csproj).</summary>
    public static string Current { get; } = ResolveSemver();

    /// <summary>Identificador exacto del build (commit-fecha) o null si no se estampo.</summary>
    public static string? Build { get; } = ResolveBuild();

    /// <summary>"v0.1.0" o "v0.1.0 (b08678e-20260923)" si hay build id.</summary>
    public static string Display => string.IsNullOrWhiteSpace(Build) ? $"v{Current}" : $"v{Current} ({Build})";

    private static string ResolveSemver()
    {
        // La version informativa trae "0.1.0" o "0.1.0+<sha>"; nos quedamos con la parte semver.
        var info = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info!.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }
        var v = typeof(AppVersion).Assembly.GetName().Version;
        return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    private static string? ResolveBuild()
    {
        var env = Environment.GetEnvironmentVariable("TRONOX_VERSION");
        return string.IsNullOrWhiteSpace(env) || env.Trim() == "dev" ? null : env.Trim();
    }
}
