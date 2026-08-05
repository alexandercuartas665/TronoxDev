using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Configuracion del modulo de Radicacion por tenant (RQ09 RF01-1 consecutivos + RF01-3 alertas SLA).
/// Singleton: una fila por tenant. La sigla del esquema de radicacion viene de <see cref="Entidad"/>
/// (RQ01); el consecutivo real se emite con TenantSequence (SELECT FOR UPDATE), aqui se guardan solo
/// los parametros. TENANT-SCOPED.
/// </summary>
public class RadicacionConfig : TenantEntity
{
    // ---- RF01-1 Consecutivos ----

    /// <summary>Valor inicial del consecutivo de Entrada (editable solo antes del primer radicado).</summary>
    public int ConsecutivoEntradaInicio { get; set; } = 1;
    public int ConsecutivoSalidaInicio { get; set; } = 1;
    public int ConsecutivoInternoInicio { get; set; } = 1;

    /// <summary>El 1 de enero reinicia los tres consecutivos (default true).</summary>
    public bool ReinicioAnual { get; set; } = true;

    /// <summary>Cantidad de digitos del consecutivo con ceros a la izquierda (esquema, default 6).</summary>
    public int DigitosConsecutivo { get; set; } = 6;

    /// <summary>Separador del numero de radicado (esquema, default "-").</summary>
    public string Separador { get; set; } = "-";

    // ---- RF01-3 Parametros de alertas SLA ----

    /// <summary>Primera alerta al consumir el X% del termino (default 50).</summary>
    public int Alerta1Porcentaje { get; set; } = 50;

    /// <summary>Segunda alerta - zona critica (default 80).</summary>
    public int Alerta2Porcentaje { get; set; } = 80;

    /// <summary>Alerta especial para tutelas, en horas (default 24).</summary>
    public int AlertaTutelaHoras { get; set; } = 24;

    /// <summary>Notifica al responsable configurado al vencer (default true).</summary>
    public bool NotificarJefeAlVencer { get; set; } = true;

    /// <summary>Notifica al rol/direccion configurado al vencer (default true).</summary>
    public bool NotificarDireccionAlVencer { get; set; } = true;
}
