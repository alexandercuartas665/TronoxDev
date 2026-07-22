namespace Tronox.Application.Tests;

/// <summary>
/// Generador de identificadores sinteticos para las pruebas. Los Id del dominio son BIGINT
/// (DAT-01 / ADR-001); esta clase sustituye a los antiguos TestIds.Next() de los tests y solo
/// garantiza que cada valor emitido sea distinto dentro del proceso de pruebas.
/// </summary>
internal static class TestIds
{
    private static long _next = 1000;

    public static long Next() => Interlocked.Increment(ref _next);
}
