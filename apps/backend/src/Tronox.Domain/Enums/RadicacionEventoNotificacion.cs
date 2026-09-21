namespace Tronox.Domain.Enums;

/// <summary>
/// Eventos del modulo de radicacion que pueden disparar una notificacion (RQ09 RF01-5). Algunos son
/// obligatorios por ley (prorroga, incompetencia -> siempre activos) y otros configurables.
/// </summary>
public enum RadicacionEventoNotificacion
{
    RadicacionExitosaCiudadano = 0,
    AsignacionDependencia,
    DistribucionFuncionario,
    AlertaSla50,
    AlertaSla80,
    TutelaRecibida,
    RadicadoVencido,
    ProrrogaNotificadaCiudadano,
    IncompetenciaDeclaradaCiudadano,
    RespuestaEmitidaCiudadano
}
