namespace Tronox.Domain.Enums;

/// <summary>Seguridad de transporte del buzon IMAP (RQ09 RF01-4).</summary>
public enum BuzonSeguridad
{
    SslTls = 0,
    StartTls,
    Ninguna
}
