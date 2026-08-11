namespace Tronox.Domain.Enums;

/// <summary>
/// Modo de fijacion del codigo de una version de TRD (RQ02 - RF01), paridad con la columna
/// MODO_CODIGO_SERIE del legacy doc_versionesTRD. Reintroducido por decision del cliente
/// (revierte ADR-006).
/// </summary>
public enum ModoCodigoSerie
{
    /// <summary>El sistema autogenera el codigo de version con formato TRD-&lt;anio&gt;-v&lt;consecutivo&gt;.</summary>
    CalcularCodigo = 0,

    /// <summary>El usuario escribe manualmente el codigo de version.</summary>
    EditarCodigo = 1
}
