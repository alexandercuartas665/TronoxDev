namespace Tronox.Domain.Enums;

/// <summary>
/// Ancho de la tarjeta del formulario al llenarlo (pagina publica /f, modulo /m y vista previa del
/// disenador). Es CONFIGURACION por formulario, no global: un formulario con una tabla ancha necesita
/// mas ancho que uno de contacto. NO rota a apaisado; solo ensancha la tarjeta. Default = Normal.
/// (RQ08, port ECOREX.)
/// </summary>
public enum FormCardLayout
{
    /// <summary>Ancho actual (~720px), centrado.</summary>
    Normal = 0,
    /// <summary>Tarjeta ancha (~1160px) para formularios con tablas anchas.</summary>
    Ancho,
    /// <summary>Casi todo el ancho de la ventana (min(96vw, 1600px)).</summary>
    Completo
}
