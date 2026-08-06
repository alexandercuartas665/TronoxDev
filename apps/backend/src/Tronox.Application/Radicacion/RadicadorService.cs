using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Tenancy;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion del orquestador de radicacion. Consecutivo con ISequenceService (concurrencia segura,
/// scope tenant/tipo/anio -> reinicio anual natural por el codigo). Vencimiento con el calendario habil.
/// Numero de radicado: Sigla + Cod + Anio + consecutivo, unidos por el separador de la config.
/// </summary>
public sealed class RadicadorService : IRadicadorService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISequenceService _sequences;
    private readonly ICalendarioHabilService _calendario;

    public RadicadorService(IApplicationDbContext db, ITenantContext tenant, ISequenceService sequences, ICalendarioHabilService calendario)
    {
        _db = db;
        _tenant = tenant;
        _sequences = sequences;
        _calendario = calendario;
    }

    public async Task<RadicarResult> RadicarAsync(RadicarNuevoRequest req, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) { return RadicarResult.Fail("Sesion no valida."); }

        var tipo = await _db.TiposComunicacion.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TipoComunicacionId, ct);
        if (tipo is null) { return RadicarResult.Fail("El tipo de comunicacion no existe."); }

        var cfg = await _db.RadicacionConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        var digitos = cfg?.DigitosConsecutivo ?? 6;
        var separador = cfg?.Separador ?? "-";
        var sigla = (await _db.Entidades.AsNoTracking().Select(e => e.Sigla).FirstOrDefaultAsync(ct)) ?? "RAD";

        // ---- Vencimiento SLA (calendario habil) ----
        DateTime? vencimiento = null;
        if (tipo.RequiereRespuesta && tipo.DiasRespuesta is int dias && dias > 0)
        {
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var inicio = tipo.InicioTermino == RadicacionInicioTermino.SiguienteDiaHabil
                ? await _calendario.ProximoHabilAsync(hoy.AddDays(1), ct)
                : hoy;
            var fVenc = tipo.TipoDia == RadicacionTipoDia.Calendario
                ? inicio.AddDays(dias)
                : await _calendario.SumarDiasHabilesAsync(inicio, dias, ct);
            vencimiento = DateTime.SpecifyKind(fVenc.ToDateTime(new TimeOnly(23, 59, 0)), DateTimeKind.Utc);
        }

        // ---- Consecutivo (SELECT FOR UPDATE, reinicio anual por codigo con el anio) ----
        // El codigo de secuencia debe caber en varchar(10): "RADE2026" (tipo+anio), no "RAD-Entrada-2026".
        var anio = DateTime.UtcNow.Year;
        var cod = req.Tipo switch { RadicadoTipo.Entrada => "E", RadicadoTipo.Salida => "S", _ => "I" };
        var code = $"RAD{cod}{anio}";
        await _sequences.EnsureSequenceAsync(code, ct);
        var consec = await _sequences.NextAsync(code, "", digitos, ct);
        var numero = string.Join(separador, sigla, cod, anio.ToString(), consec);

        var radicado = new Radicado
        {
            TenantId = tenantId.Value,
            NumeroRadicado = numero,
            Tipo = req.Tipo,
            Estado = RadicadoEstado.Radicado,
            Canal = req.Canal,
            Prioridad = req.Prioridad,
            TipoComunicacionId = req.TipoComunicacionId,
            Asunto = req.Asunto,
            Descripcion = req.Descripcion,
            Anonimo = req.Anonimo,
            RemitenteNombre = req.Anonimo ? null : req.RemitenteNombre,
            RemitenteEmail = req.RemitenteEmail,
            RemitenteTipoDoc = req.RemitenteTipoDoc,
            RemitenteDocumento = req.RemitenteDocumento,
            RemitenteTelefono = req.RemitenteTelefono,
            NivelReservaId = req.NivelReservaId ?? tipo.NivelReservaDefaultId,
            RadicadoRelacionadoId = req.RadicadoRelacionadoId,
            Soporte = req.Soporte,
            FechaRadicacion = DateTime.UtcNow,
            FechaVencimiento = vencimiento,
            UsuarioRadicaId = _tenant.UserId
        };

        if (req.Adjuntos is { Count: > 0 })
        {
            foreach (var a in req.Adjuntos)
            {
                radicado.Archivos.Add(new RadicadoArchivo
                {
                    TenantId = tenantId.Value,
                    Nombre = a.Nombre,
                    Extension = a.Extension,
                    MimeType = a.MimeType,
                    TamanoBytes = a.TamanoBytes,
                    StorageBucket = a.StorageBucket,
                    StorageKey = a.StorageKey,
                    Sha256 = a.Sha256,
                    FechaCarga = DateTime.UtcNow
                });
            }
        }

        radicado.Trazas.Add(new RadicadoTrazabilidad
        {
            TenantId = tenantId.Value,
            Accion = "RADICADO",
            Fecha = DateTime.UtcNow,
            UsuarioId = _tenant.UserId,
            Detalle = $"Radicado {numero} creado (canal {req.Canal})."
        });

        _db.Radicados.Add(radicado);
        await _db.SaveChangesAsync(ct);
        return RadicarResult.Success(radicado.Id, numero);
    }
}
