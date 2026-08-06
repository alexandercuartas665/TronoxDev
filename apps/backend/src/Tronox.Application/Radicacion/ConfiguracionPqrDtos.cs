namespace Tronox.Application.Radicacion;

/// <summary>Prioridad editable (props mutables para @bind en la grilla del editor).</summary>
public sealed class PrioridadDto
{
    public long Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Icono { get; set; }
    public string? Color { get; set; }
    public int? SlaSugerido { get; set; }
    public bool Activo { get; set; } = true;
    public bool EsBase { get; set; }
    public int Orden { get; set; }
}

/// <summary>Config del portal ciudadano (props mutables para @bind en el formulario).</summary>
public sealed class PortalConfigDto
{
    public string? NombreEntidad { get; set; }
    public string? Subtitulo { get; set; }
    public string? Nit { get; set; }
    public string? Color { get; set; } = "#405189";
    public int MaxAdjuntoMb { get; set; } = 20;
    public string? Banner { get; set; }
    public bool PermitirAnonimo { get; set; } = true;
    public bool ExigirCaptcha { get; set; } = true;
    public string? CanalesAtencion { get; set; }
    public string? AvisoPrivacidad { get; set; }
    public string? Faq { get; set; }
    public string? Slug { get; set; }
}

public sealed record TipoWebDto(long Id, string Nombre, string? Color, bool HabilitadoWeb, int? OrdenPortal, string? DescripcionCiudadano);
