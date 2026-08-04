using Tronox.Domain.Enums;

namespace Tronox.Application.Expedientes;

/// <summary>
/// Reglas puras de expedientes (RQ03): sin EF, testeables sin base de datos. Codigo estructurado
/// (RF04), fecha de apertura, herencia de clasificacion "solo elevar" (RF10) y obligatoriedad de
/// metadatos (DAT-04).
/// </summary>
public static class ExpedienteRules
{
    /// <summary>Digitos del consecutivo del codigo (RF04, configurable en RQ01; default 6).</summary>
    public const int ConsecutivoPadding = 6;

    public const string MensajeNoBajarClasificacion =
        "El nivel de clasificacion solo se puede elevar respecto al heredado de la TRD, nunca bajar (RF10).";

    /// <summary>Codigo del consecutivo por anio para el emisor de secuencias (scope tenant+anio).</summary>
    public static string SequenceCode(int anio) => $"EXP-{anio}";

    /// <summary>
    /// Codigo estructurado [Dependencia]-[Serie]-[Anio]-[Consecutivo] (RF04). El consecutivo llega ya
    /// formateado con padding por el emisor de secuencias.
    /// </summary>
    public static string ComponerCodigo(string codigoDependencia, string codigoSerie, int anio, string consecutivoPadded)
        => $"{codigoDependencia}-{codigoSerie}-{anio}-{consecutivoPadded}";

    public static string? ValidateNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) { return "El nombre del expediente es obligatorio (RF03)."; }
        return nombre.Trim().Length > 200 ? "El nombre no puede superar 200 caracteres." : null;
    }

    /// <summary>La fecha de apertura es obligatoria y no puede ser futura (RF03).</summary>
    public static string? ValidateFechaApertura(DateOnly fechaApertura, DateOnly hoy)
        => fechaApertura > hoy ? "La fecha de apertura no puede ser futura (RF03)." : null;

    /// <summary>Herencia RF10: el nivel elegido debe ser >= al heredado de la asignacion de TRD.</summary>
    public static bool PuedeElevar(int nivelHeredadoOrden, int nivelElegidoOrden)
        => nivelElegidoOrden >= nivelHeredadoOrden;

    /// <summary>
    /// Valida que todo metadato obligatorio de la serie tenga valor (DAT-04). Devuelve el nombre del
    /// primer faltante en un mensaje, o null si todo esta completo.
    /// </summary>
    public static string? ValidateMetadatosObligatorios(
        IEnumerable<(long TrdMetadatoId, string Nombre, bool Obligatorio)> definiciones,
        IReadOnlyDictionary<long, string?> valores)
    {
        foreach (var def in definiciones)
        {
            if (!def.Obligatorio) { continue; }
            valores.TryGetValue(def.TrdMetadatoId, out var valor);
            if (string.IsNullOrWhiteSpace(valor))
            {
                return $"El metadato '{def.Nombre}' es obligatorio.";
            }
        }
        return null;
    }
}
