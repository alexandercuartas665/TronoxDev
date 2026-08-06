namespace Tronox.Domain.Enums;

/// <summary>
/// Direccion del radicado. Espejo del legacy RAD_RADICADOS.TIPO_RADICADO (char E/S/I); en Tronox se
/// persiste como string. Entrada = comunicacion que ingresa; Salida = respuesta/oficio que sale;
/// Interno = memorando/circular entre dependencias.
/// </summary>
public enum RadicadoTipo
{
    Entrada,
    Salida,
    Interno
}
