namespace Tronox.Domain.Enums;

/// <summary>
/// Modo de radicacion automatica de los correos capturados por un buzon (RQ09 RF01-4):
/// SemiAutomatico crea pre-radicado y espera X minutos; Automatico radica de inmediato (pausa en
/// duplicado); Manual siempre exige revision del operador.
/// </summary>
public enum BuzonModoRadicacion
{
    SemiAutomatico = 0,
    Automatico,
    Manual
}
