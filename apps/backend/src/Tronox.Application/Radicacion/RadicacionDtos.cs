using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

// ---- RF01-1 Consecutivos + RF01-3 Alertas SLA (config singleton del tenant) ----

public sealed record RadicacionConfigDto(
    // Consecutivos (RF01-1)
    int ConsecutivoEntradaInicio, int ConsecutivoSalidaInicio, int ConsecutivoInternoInicio,
    bool ReinicioAnual, int DigitosConsecutivo, string Separador,
    // Alertas SLA (RF01-3)
    int Alerta1Porcentaje, int Alerta2Porcentaje, int AlertaTutelaHoras,
    bool NotificarJefeAlVencer, bool NotificarDireccionAlVencer,
    // Solo lectura: ultimo consecutivo asignado por tipo (del emisor de secuencias). 0 si aun ninguno.
    int ConsecutivoEntradaActual, int ConsecutivoSalidaActual, int ConsecutivoInternoActual,
    // Contexto: sigla del esquema (de la Entidad, RQ01) para la vista previa del numero de radicado.
    string? SiglaEntidad);

public sealed record SaveRadicacionConfigRequest(
    int ConsecutivoEntradaInicio, int ConsecutivoSalidaInicio, int ConsecutivoInternoInicio,
    bool ReinicioAnual, int DigitosConsecutivo, string Separador,
    int Alerta1Porcentaje, int Alerta2Porcentaje, int AlertaTutelaHoras,
    bool NotificarJefeAlVencer, bool NotificarDireccionAlVencer);

// ---- RF01-2 Tipos de comunicacion ----

public sealed record TipoComunicacionDto(
    long Id, string Codigo, string Nombre, RadicacionDireccion Direccion,
    bool EsPqrsd, bool EsTutela, bool EsRecurso, bool RequiereRespuesta,
    int? DiasRespuesta, RadicacionTipoDia? TipoDia, RadicacionInicioTermino? InicioTermino,
    bool Prorrogable, int? DiasProrroga, bool PermiteAnonimo, bool HabilitadoWeb,
    long? NivelReservaDefaultId, string? NivelReservaDefaultNombre,
    string? Icono, string? Color, string? PalabrasClave, int? OrdenPortal,
    string? DescripcionCiudadano, bool Activo, bool EsBase);

public sealed record SaveTipoComunicacionRequest(
    string Codigo, string Nombre, RadicacionDireccion Direccion,
    bool EsPqrsd, bool EsTutela, bool EsRecurso, bool RequiereRespuesta,
    int? DiasRespuesta, RadicacionTipoDia? TipoDia, RadicacionInicioTermino? InicioTermino,
    bool Prorrogable, int? DiasProrroga, bool PermiteAnonimo, bool HabilitadoWeb,
    long? NivelReservaDefaultId, string? Icono, string? Color, string? PalabrasClave,
    int? OrdenPortal, string? DescripcionCiudadano, bool Activo);

// ---- RF01-4 Buzones de correo ----

public sealed record BuzonCorreoDto(
    long Id, string NombreBuzon, string DireccionEmail, BuzonProtocolo Protocolo,
    string? Servidor, int? Puerto, BuzonSeguridad Seguridad, string Usuario, bool TieneClave,
    string Carpeta, BuzonFrecuenciaRevision FrecuenciaRevision, BuzonModoRadicacion ModoRadicacion,
    int? TiempoEsperaMinutos, long? TipoComunicacionDefaultId, string? TipoComunicacionDefaultNombre,
    long? DependenciaDefaultId, string? DependenciaDefaultNombre, bool Activo);

/// <summary>Contrasena null = no cambiar (conserva la cifrada existente).</summary>
public sealed record SaveBuzonCorreoRequest(
    string NombreBuzon, string DireccionEmail, BuzonProtocolo Protocolo,
    string? Servidor, int? Puerto, BuzonSeguridad Seguridad, string Usuario, string? Contrasena,
    string Carpeta, BuzonFrecuenciaRevision FrecuenciaRevision, BuzonModoRadicacion ModoRadicacion,
    int? TiempoEsperaMinutos, long? TipoComunicacionDefaultId, long? DependenciaDefaultId, bool Activo);

// ---- RF01-5 Notificaciones ----

public sealed record NotificacionRadicacionDto(
    long Id, RadicacionEventoNotificacion Evento, string EventoNombre, bool Activo, bool ObligatorioPorLey,
    IReadOnlyList<long> RolesIds, IReadOnlyList<long> UsuariosIds,
    string? PlantillaAsunto, string? PlantillaCuerpo);

public sealed record SaveNotificacionRadicacionRequest(
    bool Activo, IReadOnlyList<long> RolesIds, IReadOnlyList<long> UsuariosIds,
    string? PlantillaAsunto, string? PlantillaCuerpo);

// ---- RF01-6 Migracion de radicados historicos ----

public sealed record MigracionRadicadosDto(
    long Id, DateTimeOffset FechaMigracion, string? ArchivoNombre,
    int CantidadTotal, int CantidadExitosos, int CantidadErrores,
    string EstadoDestino, string Estado);
