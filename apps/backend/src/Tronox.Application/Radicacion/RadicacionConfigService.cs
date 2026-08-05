using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Configuracion del modulo de Radicacion (RQ09 RF01): consecutivos + alertas SLA (singleton por
/// tenant), notificaciones por evento y bitacora de migracion historica. La primera lectura crea la
/// config con sus valores por defecto y siembra (idempotente) los 13 tipos de comunicacion base y una
/// fila de notificacion por evento. Tenant-scoped: el filtro global de EF acota por tenant.
/// </summary>
public sealed class RadicacionConfigService : IRadicacionConfigService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public RadicacionConfigService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<RadicacionConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay tenant activo.");

        var cfg = await _db.RadicacionConfigs.FirstOrDefaultAsync(cancellationToken);
        if (cfg is null)
        {
            cfg = await CreateConfigAndSeedAsync(tenantId, cancellationToken);
        }

        var sigla = (await _db.Entidades.AsNoTracking().FirstOrDefaultAsync(cancellationToken))?.Sigla;
        return Map(cfg, sigla);
    }

    private async Task<RadicacionConfig> CreateConfigAndSeedAsync(long tenantId, CancellationToken cancellationToken)
    {
        // Unidad de trabajo unica: crear la config y sembrar tipos base + eventos de notificacion.
        // Se une a la transaccion del llamador si ya hay una abierta (patron DAT del proyecto).
        var ownsTransaction = !_db.HasActiveTransaction;
        var transaction = ownsTransaction ? await _db.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var cfg = new RadicacionConfig { TenantId = tenantId };
            _db.RadicacionConfigs.Add(cfg);

            // Idempotente: solo sembrar si el tenant aun no tiene tipos/notificaciones.
            if (!await _db.TiposComunicacion.AnyAsync(cancellationToken))
            {
                foreach (var tipo in BuildTiposBase(tenantId))
                {
                    _db.TiposComunicacion.Add(tipo);
                }
            }

            if (!await _db.NotificacionesRadicacion.AnyAsync(cancellationToken))
            {
                foreach (RadicacionEventoNotificacion evento in Enum.GetValues<RadicacionEventoNotificacion>())
                {
                    _db.NotificacionesRadicacion.Add(new NotificacionRadicacionConfig
                    {
                        TenantId = tenantId,
                        Evento = evento,
                        Activo = true
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return cfg;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<RadicacionResult<RadicacionConfigDto>> SaveConfigAsync(SaveRadicacionConfigRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is null)
        {
            return RadicacionResult<RadicacionConfigDto>.Invalid("No hay tenant activo.");
        }

        if (request.Alerta1Porcentaje < 1 || request.Alerta1Porcentaje > 100 ||
            request.Alerta2Porcentaje < 1 || request.Alerta2Porcentaje > 100)
        {
            return RadicacionResult<RadicacionConfigDto>.Invalid("Los porcentajes de alerta deben estar entre 1 y 100.");
        }
        if (request.Alerta1Porcentaje >= request.Alerta2Porcentaje)
        {
            return RadicacionResult<RadicacionConfigDto>.Invalid("La primera alerta debe ser menor que la segunda.");
        }
        if (request.AlertaTutelaHoras < 0)
        {
            return RadicacionResult<RadicacionConfigDto>.Invalid("Las horas de alerta de tutela no pueden ser negativas.");
        }
        if (request.DigitosConsecutivo < 1 || request.DigitosConsecutivo > 12)
        {
            return RadicacionResult<RadicacionConfigDto>.Invalid("Los digitos del consecutivo deben estar entre 1 y 12.");
        }
        if (request.ConsecutivoEntradaInicio < 0 || request.ConsecutivoSalidaInicio < 0 || request.ConsecutivoInternoInicio < 0)
        {
            return RadicacionResult<RadicacionConfigDto>.Invalid("Los consecutivos de inicio no pueden ser negativos.");
        }

        var cfg = await _db.RadicacionConfigs.FirstOrDefaultAsync(cancellationToken);
        if (cfg is null)
        {
            cfg = await CreateConfigAndSeedAsync(_tenantContext.TenantId.Value, cancellationToken);
        }

        // TODO(RQ09): el consecutivo de inicio solo es editable antes del primer radicado; como la
        // radicacion aun no opera, por ahora se permite editar sin restriccion.
        cfg.ConsecutivoEntradaInicio = request.ConsecutivoEntradaInicio;
        cfg.ConsecutivoSalidaInicio = request.ConsecutivoSalidaInicio;
        cfg.ConsecutivoInternoInicio = request.ConsecutivoInternoInicio;
        cfg.ReinicioAnual = request.ReinicioAnual;
        cfg.DigitosConsecutivo = request.DigitosConsecutivo;
        cfg.Separador = string.IsNullOrEmpty(request.Separador) ? "-" : request.Separador;
        cfg.Alerta1Porcentaje = request.Alerta1Porcentaje;
        cfg.Alerta2Porcentaje = request.Alerta2Porcentaje;
        cfg.AlertaTutelaHoras = request.AlertaTutelaHoras;
        cfg.NotificarJefeAlVencer = request.NotificarJefeAlVencer;
        cfg.NotificarDireccionAlVencer = request.NotificarDireccionAlVencer;

        _audit.Write(actorUserId, "radicacion.config.update", nameof(RadicacionConfig), cfg,
            previousValue: null,
            newValue: new { cfg.DigitosConsecutivo, cfg.Alerta1Porcentaje, cfg.Alerta2Porcentaje, cfg.AlertaTutelaHoras });

        await _db.SaveChangesAsync(cancellationToken);

        var sigla = (await _db.Entidades.AsNoTracking().FirstOrDefaultAsync(cancellationToken))?.Sigla;
        return RadicacionResult<RadicacionConfigDto>.Ok(Map(cfg, sigla));
    }

    public async Task<IReadOnlyList<NotificacionRadicacionDto>> ListNotificacionesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.NotificacionesRadicacion.AsNoTracking()
            .OrderBy(n => n.Evento)
            .ToListAsync(cancellationToken);
        return rows.Select(MapNotificacion).ToList();
    }

    public async Task<RadicacionResult<NotificacionRadicacionDto>> SaveNotificacionAsync(long id, SaveNotificacionRadicacionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var row = await _db.NotificacionesRadicacion.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (row is null)
        {
            return RadicacionResult<NotificacionRadicacionDto>.NotFound("La notificacion no existe.");
        }

        // Los eventos obligatorios por ley se mantienen siempre activos.
        row.Activo = EsObligatorioPorLey(row.Evento) || request.Activo;
        row.DestinatariosRolesJson = SerializeIds(request.RolesIds);
        row.DestinatariosUsuariosJson = SerializeIds(request.UsuariosIds);
        row.PlantillaAsunto = request.PlantillaAsunto?.Trim();
        row.PlantillaCuerpo = request.PlantillaCuerpo;

        _audit.Write(actorUserId, "radicacion.notificacion.update", nameof(NotificacionRadicacionConfig), row,
            previousValue: null, newValue: new { row.Evento, row.Activo });

        await _db.SaveChangesAsync(cancellationToken);
        return RadicacionResult<NotificacionRadicacionDto>.Ok(MapNotificacion(row));
    }

    public async Task<IReadOnlyList<MigracionRadicadosDto>> ListMigracionesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.MigracionesRadicados.AsNoTracking()
            .OrderByDescending(m => m.FechaMigracion)
            .ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(m => new MigracionRadicadosDto(
            m.Id, m.FechaMigracion, m.ArchivoNombre,
            m.CantidadTotal, m.CantidadExitosos, m.CantidadErrores,
            m.EstadoDestino, m.Estado)).ToList();
    }

    // ---- Mapeo ----

    private static RadicacionConfigDto Map(RadicacionConfig c, string? siglaEntidad) => new(
        c.ConsecutivoEntradaInicio, c.ConsecutivoSalidaInicio, c.ConsecutivoInternoInicio,
        c.ReinicioAnual, c.DigitosConsecutivo, c.Separador,
        c.Alerta1Porcentaje, c.Alerta2Porcentaje, c.AlertaTutelaHoras,
        c.NotificarJefeAlVencer, c.NotificarDireccionAlVencer,
        // TODO(RQ09): leer ultimo consecutivo asignado del emisor de secuencias. 0 mientras la
        // radicacion aun no opera.
        0, 0, 0,
        siglaEntidad);

    private static NotificacionRadicacionDto MapNotificacion(NotificacionRadicacionConfig n) => new(
        n.Id, n.Evento, EventoNombre(n.Evento), n.Activo, EsObligatorioPorLey(n.Evento),
        DeserializeIds(n.DestinatariosRolesJson), DeserializeIds(n.DestinatariosUsuariosJson),
        n.PlantillaAsunto, n.PlantillaCuerpo);

    private static bool EsObligatorioPorLey(RadicacionEventoNotificacion evento) =>
        evento is RadicacionEventoNotificacion.ProrrogaNotificadaCiudadano
            or RadicacionEventoNotificacion.IncompetenciaDeclaradaCiudadano
            or RadicacionEventoNotificacion.TutelaRecibida;

    private static string EventoNombre(RadicacionEventoNotificacion evento) => evento switch
    {
        RadicacionEventoNotificacion.RadicacionExitosaCiudadano => "Radicacion exitosa (ciudadano)",
        RadicacionEventoNotificacion.AsignacionDependencia => "Asignacion a dependencia",
        RadicacionEventoNotificacion.DistribucionFuncionario => "Distribucion a funcionario",
        RadicacionEventoNotificacion.AlertaSla50 => "Alerta SLA al 50%",
        RadicacionEventoNotificacion.AlertaSla80 => "Alerta SLA al 80%",
        RadicacionEventoNotificacion.TutelaRecibida => "Tutela recibida",
        RadicacionEventoNotificacion.RadicadoVencido => "Radicado vencido",
        RadicacionEventoNotificacion.ProrrogaNotificadaCiudadano => "Prorroga notificada (ciudadano)",
        RadicacionEventoNotificacion.IncompetenciaDeclaradaCiudadano => "Incompetencia declarada (ciudadano)",
        RadicacionEventoNotificacion.RespuestaEmitidaCiudadano => "Respuesta emitida (ciudadano)",
        _ => evento.ToString()
    };

    private static string? SerializeIds(IReadOnlyList<long>? ids)
    {
        if (ids is null || ids.Count == 0)
        {
            return null;
        }
        return JsonSerializer.Serialize(ids);
    }

    private static IReadOnlyList<long> DeserializeIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<long>();
        }
        try
        {
            return JsonSerializer.Deserialize<long[]>(json) ?? Array.Empty<long>();
        }
        catch (JsonException)
        {
            return Array.Empty<long>();
        }
    }

    // ---- Semilla de los 13 tipos de comunicacion base (RF01-2) ----

    private static IEnumerable<TipoComunicacion> BuildTiposBase(long tenantId)
    {
        // Los tipos con termino usan inicio de termino al siguiente dia habil.
        TipoComunicacion Tipo(
            string codigo, string nombre, RadicacionDireccion direccion,
            bool esPqrsd, bool esTutela, bool esRecurso, bool requiereRespuesta,
            int? diasRespuesta, RadicacionTipoDia? tipoDia, bool prorrogable,
            bool permiteAnonimo, bool habilitadoWeb) => new()
            {
                TenantId = tenantId,
                Codigo = codigo,
                Nombre = nombre,
                Direccion = direccion,
                EsPqrsd = esPqrsd,
                EsTutela = esTutela,
                EsRecurso = esRecurso,
                RequiereRespuesta = requiereRespuesta,
                DiasRespuesta = diasRespuesta,
                TipoDia = tipoDia,
                InicioTermino = requiereRespuesta ? RadicacionInicioTermino.SiguienteDiaHabil : null,
                Prorrogable = prorrogable,
                DiasProrroga = null,
                PermiteAnonimo = permiteAnonimo,
                HabilitadoWeb = habilitadoWeb,
                NivelReservaDefaultId = null,
                Activo = true,
                EsBase = true
            };

        yield return Tipo("PETICION", "Peticion", RadicacionDireccion.Entrada, true, false, false, true, 15, RadicacionTipoDia.Habiles, true, true, true);
        yield return Tipo("QUEJA", "Queja", RadicacionDireccion.Entrada, true, false, false, true, 15, RadicacionTipoDia.Habiles, true, true, true);
        yield return Tipo("RECLAMO", "Reclamo", RadicacionDireccion.Entrada, true, false, false, true, 15, RadicacionTipoDia.Habiles, true, true, true);
        yield return Tipo("SUGERENCIA", "Sugerencia", RadicacionDireccion.Entrada, true, false, false, false, null, null, false, true, true);
        yield return Tipo("FELICITACION", "Felicitacion", RadicacionDireccion.Entrada, true, false, false, false, null, null, false, true, true);
        yield return Tipo("DENUNCIA", "Denuncia", RadicacionDireccion.Entrada, true, false, false, true, 15, RadicacionTipoDia.Habiles, true, true, true);
        yield return Tipo("DERECHO_PETICION", "Derecho de Peticion", RadicacionDireccion.Entrada, true, false, false, true, 15, RadicacionTipoDia.Habiles, true, true, true);
        yield return Tipo("INFO_PUBLICA", "Solicitud Informacion Publica", RadicacionDireccion.Entrada, false, false, false, true, 10, RadicacionTipoDia.Habiles, true, false, true);
        yield return Tipo("TUTELA", "Tutela", RadicacionDireccion.Entrada, false, true, false, true, 10, RadicacionTipoDia.Calendario, false, false, false);
        yield return Tipo("RECURSO_REPO", "Recurso de Reposicion", RadicacionDireccion.Entrada, false, false, true, true, 10, RadicacionTipoDia.Habiles, false, false, false);
        yield return Tipo("RECURSO_APEL", "Recurso de Apelacion", RadicacionDireccion.Entrada, false, false, true, true, 10, RadicacionTipoDia.Habiles, false, false, false);
        yield return Tipo("OFICIO_EXT", "Oficio Externo", RadicacionDireccion.Entrada, false, false, false, true, 10, RadicacionTipoDia.Habiles, true, false, false);
        yield return Tipo("OFICIO_INT", "Memorando / Circular Interna", RadicacionDireccion.Interno, false, false, false, true, 10, RadicacionTipoDia.Habiles, true, false, false);
    }
}
