namespace Tronox.Application.Common.Auth;

/// <summary>Configuracion del JWT propio de TRONOX.tareas (seccion "Jwt").</summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Tronox";
    public string Audience { get; set; } = "Tronox";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
}
