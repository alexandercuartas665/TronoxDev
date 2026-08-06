using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion de "Correos por Revisar" (rad_correos). LINQ parametrizado; radicar-desde-correo usa el
/// RadicadorService (consecutivo + SLA) y cierra el termino en la vinculacion (RF04-5). Los adjuntos se
/// referencian desde object storage (invariante 9). Fechas en UTC. Quirks del legacy NO replicados
/// (SQL concatenado, fail-open, sin transaccion, BLOB, MAX(REG)).
/// </summary>
public sealed class RadicacionCorreosService : IRadicacionCorreosService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IRadicadorService _radicador;

    public RadicacionCorreosService(IApplicationDbContext db, ITenantContext tenant, IRadicadorService radicador)
    {
        _db = db;
        _tenant = tenant;
        _radicador = radicador;
    }

    public async Task<CorreosListaDto> ListarAsync(string tab, CancellationToken ct = default)
    {
        var estado = string.Equals(tab, "descartados", StringComparison.OrdinalIgnoreCase)
            ? CorreoRevisionEstado.Descartado : CorreoRevisionEstado.Pendiente;
        var ahora = DateTime.UtcNow;

        var raw = await _db.CorreosRecibidos.AsNoTracking().Where(c => c.Estado == estado)
            .OrderByDescending(c => c.FechaRecepcion)
            .Select(c => new
            {
                c.Id, c.Remitente, c.RemitenteEmail, c.Asunto, c.FechaRecepcion, c.NumAdjuntos,
                c.Confianza, c.Modo, c.RadicaEn, c.DuplicadoNumero, c.RadicadoRef,
                TipoNombre = _db.TiposComunicacion.Where(t => t.Id == c.TipoDetectadoId).Select(t => t.Nombre).FirstOrDefault(),
                TipoColor = _db.TiposComunicacion.Where(t => t.Id == c.TipoDetectadoId).Select(t => t.Color).FirstOrDefault()
            }).ToListAsync(ct);

        var lista = raw.Select(c => new CorreoItemDto(
            c.Id, string.IsNullOrWhiteSpace(c.Remitente) ? "(Sin nombre)" : c.Remitente!, c.RemitenteEmail,
            c.Asunto, c.FechaRecepcion?.ToString("HH:mm") ?? "", c.NumAdjuntos, c.TipoNombre, c.TipoColor,
            c.Confianza, c.Modo.ToString(),
            c.RadicaEn is null ? null : (int)(c.RadicaEn.Value - ahora).TotalSeconds,
            c.DuplicadoNumero, c.RadicadoRef)).ToList();

        var pend = await _db.CorreosRecibidos.CountAsync(c => c.Estado == CorreoRevisionEstado.Pendiente, ct);
        var desc = await _db.CorreosRecibidos.CountAsync(c => c.Estado == CorreoRevisionEstado.Descartado, ct);
        return new CorreosListaDto(lista, pend, desc);
    }

    public async Task<CorreoDetalleDto?> DetalleAsync(long reg, CancellationToken ct = default)
    {
        var c = await _db.CorreosRecibidos.AsNoTracking().Where(x => x.Id == reg)
            .Select(x => new
            {
                x.Id, x.BuzonEmail, x.Remitente, x.RemitenteEmail, x.Asunto, x.CuerpoTratado, x.FechaRecepcion,
                x.TipoDetectadoId, x.Confianza, x.Modo, x.Estado, x.DuplicadoNumero, x.RadicadoRef, x.RadicadoNumero,
                TipoNombre = _db.TiposComunicacion.Where(t => t.Id == x.TipoDetectadoId).Select(t => t.Nombre).FirstOrDefault(),
                TipoColor = _db.TiposComunicacion.Where(t => t.Id == x.TipoDetectadoId).Select(t => t.Color).FirstOrDefault()
            }).FirstOrDefaultAsync(ct);
        if (c is null) { return null; }

        var adjs = await _db.CorreosRecibidosAdjuntos.AsNoTracking().Where(a => a.CorreoRecibidoId == reg)
            .Select(a => new CorreoAdjuntoDto(a.Id, a.Nombre, a.TamanoBytes / 1024, a.Extension, a.EsCuerpoHtml, a.EsHilo))
            .ToListAsync(ct);

        string? causal = null;
        if (c.Estado == CorreoRevisionEstado.Descartado)
        {
            causal = await _db.CorreosDescartados.AsNoTracking()
                .Where(d => d.CorreoRecibidoId == reg && !d.Recuperado)
                .OrderByDescending(d => d.Fecha).Select(d => d.Causal).FirstOrDefaultAsync(ct);
        }

        return new CorreoDetalleDto(
            c.Id, c.BuzonEmail, string.IsNullOrWhiteSpace(c.Remitente) ? "(Sin nombre)" : c.Remitente!, c.RemitenteEmail,
            c.Asunto, c.CuerpoTratado, c.FechaRecepcion?.ToString("dd/MM/yyyy HH:mm") ?? "", c.TipoDetectadoId,
            c.TipoNombre, c.TipoColor, c.Confianza, c.Modo.ToString(), c.Estado.ToString(),
            c.DuplicadoNumero, c.RadicadoRef, c.RadicadoNumero, causal, adjs);
    }

    public async Task<CorreoResult> RadicarAsync(long reg, long? tipoOverrideId, bool vincular, CancellationToken ct = default)
    {
        var correo = await _db.CorreosRecibidos.FirstOrDefaultAsync(c => c.Id == reg, ct);
        if (correo is null) { return CorreoResult.Fail("Correo no encontrado."); }
        if (correo.Estado != CorreoRevisionEstado.Pendiente) { return CorreoResult.Fail("El correo no esta pendiente."); }

        // ----- Rama A: vincular como respuesta (RF04-5) -----
        if (vincular)
        {
            if (string.IsNullOrWhiteSpace(correo.RadicadoRef)) { return CorreoResult.Fail("El correo no referencia un radicado existente."); }
            var padre = await _db.Radicados.FirstOrDefaultAsync(r => r.NumeroRadicado == correo.RadicadoRef, ct);
            if (padre is null) { return CorreoResult.Fail("El radicado referenciado no existe."); }
            padre.Estado = RadicadoEstado.Respondido;
            _db.RadicadosTrazabilidad.Add(new RadicadoTrazabilidad
            {
                TenantId = padre.TenantId, RadicadoId = padre.Id, Accion = "RESPONDIDO", Fecha = DateTime.UtcNow,
                UsuarioId = _tenant.UserId,
                Detalle = $"Respuesta recibida por correo de {correo.RemitenteEmail} - termino cerrado (RF04-5)."
            });
            correo.Estado = CorreoRevisionEstado.Radicado;
            correo.RadicadoId = padre.Id;
            correo.RadicadoNumero = correo.RadicadoRef;
            await _db.SaveChangesAsync(ct);
            return CorreoResult.Success(correo.RadicadoRef);
        }

        // ----- Rama B: radicar como nuevo -----
        var tipoId = tipoOverrideId ?? correo.TipoDetectadoId;
        if (tipoId is null) { return CorreoResult.Fail("Seleccione el tipo de comunicacion."); }

        var adjuntos = await _db.CorreosRecibidosAdjuntos.AsNoTracking().Where(a => a.CorreoRecibidoId == reg)
            .Select(a => new RadicarAdjunto(a.Nombre, a.Extension, a.MimeType, a.TamanoBytes, a.StorageBucket, a.StorageKey, a.Sha256))
            .ToListAsync(ct);

        var res = await _radicador.RadicarAsync(new RadicarNuevoRequest(
            Tipo: RadicadoTipo.Entrada,
            TipoComunicacionId: tipoId.Value,
            Asunto: correo.Asunto,
            Descripcion: correo.CuerpoTratado,
            Canal: RadicadoCanal.Correo,
            Anonimo: false,
            RemitenteNombre: correo.Remitente,
            RemitenteEmail: correo.RemitenteEmail,
            RadicadoRelacionadoId: null,
            Soporte: "Electronico",
            Adjuntos: adjuntos), ct);
        if (!res.Ok) { return CorreoResult.Fail(res.Error ?? "No se pudo radicar."); }

        correo.Estado = CorreoRevisionEstado.Radicado;
        correo.RadicadoId = res.RadicadoId;
        correo.RadicadoNumero = res.Numero;
        await _db.SaveChangesAsync(ct);
        return CorreoResult.Success(res.Numero);
    }

    public async Task<CorreoResult> EditarAsync(EditarCorreoRequest req, CancellationToken ct = default)
    {
        var c = await _db.CorreosRecibidos.FirstOrDefaultAsync(x => x.Id == req.Reg, ct);
        if (c is null) { return CorreoResult.Fail("Correo no encontrado."); }
        if (c.Estado != CorreoRevisionEstado.Pendiente) { return CorreoResult.Fail("Solo se editan correos pendientes."); }
        c.TipoDetectadoId = req.TipoDetectadoId;
        c.Confianza = 100; // confirmado por el operador
        c.Asunto = req.Asunto;
        c.CuerpoTratado = req.Descripcion;
        c.Remitente = req.Nombre;
        c.RemitenteEmail = req.Email;
        await _db.SaveChangesAsync(ct);
        return CorreoResult.Success();
    }

    public async Task<CorreoResult> DescartarAsync(long reg, string causal, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(causal)) { return CorreoResult.Fail("La causal de descarte es obligatoria."); }
        var c = await _db.CorreosRecibidos.FirstOrDefaultAsync(x => x.Id == reg, ct);
        if (c is null) { return CorreoResult.Fail("Correo no encontrado."); }
        c.Estado = CorreoRevisionEstado.Descartado;
        c.RadicaEn = null;
        _db.CorreosDescartados.Add(new CorreoDescartado
        {
            TenantId = c.TenantId, CorreoRecibidoId = c.Id, UsuarioId = _tenant.UserId,
            Causal = causal.Trim(), Fecha = DateTime.UtcNow, Recuperado = false
        });
        await _db.SaveChangesAsync(ct);
        return CorreoResult.Success();
    }

    public async Task<CorreoResult> RecuperarAsync(long reg, CancellationToken ct = default)
    {
        var c = await _db.CorreosRecibidos.FirstOrDefaultAsync(x => x.Id == reg, ct);
        if (c is null) { return CorreoResult.Fail("Correo no encontrado."); }
        c.Estado = CorreoRevisionEstado.Pendiente;
        var log = await _db.CorreosDescartados
            .Where(d => d.CorreoRecibidoId == reg && !d.Recuperado).OrderByDescending(d => d.Fecha).FirstOrDefaultAsync(ct);
        if (log is not null)
        {
            log.Recuperado = true;
            log.FechaRecupera = DateTime.UtcNow;
            log.UsuarioRecuperaId = _tenant.UserId;
        }
        await _db.SaveChangesAsync(ct);
        return CorreoResult.Success();
    }

    public async Task<IReadOnlyList<OpcionDto>> TiposEntradaAsync(CancellationToken ct = default)
        => await _db.TiposComunicacion.AsNoTracking()
            .Where(t => t.Activo && t.Direccion == RadicacionDireccion.Entrada)
            .OrderBy(t => t.Nombre).Select(t => new OpcionDto(t.Id, t.Nombre)).ToListAsync(ct);

    public async Task<CorreoResult> SimularAsync(CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) { return CorreoResult.Fail("Sesion no valida."); }

        var buzon = await _db.BuzonesCorreo.AsNoTracking().FirstOrDefaultAsync(b => b.Activo, ct);
        var tipo = await _db.TiposComunicacion.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Activo && t.Direccion == RadicacionDireccion.Entrada && t.EsPqrsd, ct);

        var i = (await _db.CorreosRecibidos.CountAsync(ct)) + 1;
        var plantillas = new[]
        {
            ("Solicitud de certificado laboral", "Buen dia, solicito de manera respetuosa un certificado laboral. Quedo atento. Gracias."),
            ("Queja por demora en tramite", "Presento queja por la demora en la respuesta a mi solicitud radicada el mes pasado."),
            ("Peticion de informacion publica", "Solicito copia de los contratos suscritos en la vigencia actual, conforme a la Ley 1712."),
            ("Derecho de peticion", "En ejercicio del derecho de peticion solicito informacion sobre el estado de mi proceso."),
        };
        var (asunto, cuerpo) = plantillas[i % plantillas.Length];
        var nombre = new[] { "Juan Perez", "Maria Gomez", "Carlos Ruiz", "Ana Torres" }[i % 4];
        var email = $"{nombre.Split(' ')[0].ToLower()}{i}@ciudadano.com";

        _db.CorreosRecibidos.Add(new CorreoRecibido
        {
            TenantId = tenantId.Value,
            BuzonCorreoId = buzon?.Id,
            BuzonEmail = buzon?.DireccionEmail ?? "ventanilla@entidad.gov.co",
            Estado = CorreoRevisionEstado.Pendiente,
            Remitente = nombre,
            RemitenteEmail = email,
            Asunto = asunto,
            CuerpoTratado = cuerpo,
            MessageId = $"sim-{Guid.NewGuid():N}@simulador",
            TipoDetectadoId = tipo?.Id,
            Confianza = 78,
            Modo = buzon?.ModoRadicacion ?? BuzonModoRadicacion.Manual,
            FechaRecepcion = DateTime.UtcNow,
            NumAdjuntos = 0
        });
        await _db.SaveChangesAsync(ct);
        return CorreoResult.Success();
    }
}
