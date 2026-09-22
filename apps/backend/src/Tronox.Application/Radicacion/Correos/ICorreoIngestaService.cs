namespace Tronox.Application.Radicacion.Correos;

/// <summary>
/// Ingesta de correos de un buzon (RQ16): captura por IMAP, clasifica con IA y, segun el modo del buzon,
/// radica automaticamente o deja el correo pendiente de revision. Dedup por Message-ID (no reprocesa).
/// </summary>
public interface ICorreoIngestaService
{
    Task<CorreoIngestaResumen> ProcesarBuzonAsync(long buzonId, CancellationToken ct = default);
    Task<ImapTestResult> ProbarConexionAsync(long buzonId, CancellationToken ct = default);
}

/// <summary>Resumen de una corrida de ingesta (alimenta el "Ultima corrida" de la UI).</summary>
public sealed record CorreoIngestaResumen(
    bool Ok, int Leidos, int Pqr, int Descartados, int Duplicados, int Errores,
    int Adjuntos, int Tokens, int Radicados, string? Error)
{
    public static CorreoIngestaResumen Fail(string error) => new(false, 0, 0, 0, 0, 0, 0, 0, 0, error);
    public string Texto => $"{Leidos} leidos - {Pqr} PQR - {Descartados} descartados - {Radicados} radicados - "
        + $"{Duplicados} ya vistos - {Errores} errores - {Adjuntos} adjuntos - {Tokens} tokens";
}
