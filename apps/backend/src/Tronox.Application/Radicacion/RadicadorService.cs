using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Documentos;
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
    private readonly IObjectStorage _storage;

    public RadicadorService(IApplicationDbContext db, ITenantContext tenant, ISequenceService sequences,
        ICalendarioHabilService calendario, IObjectStorage storage)
    {
        _db = db;
        _tenant = tenant;
        _sequences = sequences;
        _calendario = calendario;
        _storage = storage;
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
        var incluirAnio = cfg?.IncluirAnio ?? true;
        // Sigla del esquema: la del modulo (rad_config) si esta definida, si no la de la Entidad (RQ01).
        var sigla = (string.IsNullOrWhiteSpace(cfg?.SiglaRadicacion)
                        ? (await _db.Entidades.AsNoTracking().Select(e => e.Sigla).FirstOrDefaultAsync(ct))
                        : cfg!.SiglaRadicacion)
                    ?? "RAD";

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
        // Numero de radicado: Sigla + Cod [+ Anio] + consecutivo, unidos por el separador de la config.
        var partes = incluirAnio
            ? new[] { sigla, cod, anio.ToString(), consec }
            : new[] { sigla, cod, consec };
        var numero = string.Join(separador, partes);

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
            RemitenteMunicipio = req.Anonimo ? null : req.RemitenteMunicipio,
            NivelReservaId = req.NivelReservaId ?? tipo.NivelReservaDefaultId,
            RadicadoRelacionadoId = req.RadicadoRelacionadoId,
            Soporte = req.Soporte,
            Folios = req.Folios,
            NumAnexos = req.NumAnexos,
            FechaDocumento = req.FechaDocumento,
            Observaciones = req.Observaciones,
            DependenciaOrigenId = req.DependenciaOrigenId,
            FuncionarioOrigenId = req.FuncionarioOrigenId,
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

    public async Task<RadicarResult> RadicarConArchivosAsync(RadicarNuevoRequest request,
        IReadOnlyList<AdjuntoBytes> archivos, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) { return RadicarResult.Fail("Sesion no valida."); }
        if (archivos is not { Count: > 0 }) { return await RadicarAsync(request, ct); }

        // Sube cada documento electronico a object storage (invariante 9: nunca BLOB en BD). La key es
        // opaca y tenant-scoped, igual que en Documentos: "{tenant}/{guid}.{ext}".
        var adjuntos = new List<RadicarAdjunto>(archivos.Count);
        var totalFolios = 0;
        foreach (var a in archivos)
        {
            if (a.Contenido is not { Length: > 0 }) { continue; }
            var ext = System.IO.Path.GetExtension(a.Nombre).TrimStart('.').ToLowerInvariant();
            var contentType = a.MimeType ?? DocumentoRules.ContentType(a.Nombre);
            var hash = DocumentoRules.HashSha256(a.Contenido);
            var folios = ext == "pdf" ? ContarPaginasPdf(a.Contenido) : 1;
            totalFolios += folios;
            var key = $"{tenantId.Value}/{Guid.NewGuid():N}" + (string.IsNullOrEmpty(ext) ? "" : $".{ext}");
            using (var ms = new MemoryStream(a.Contenido, writable: false))
            {
                await _storage.PutAsync(key, ms, contentType, ct);
            }
            adjuntos.Add(new RadicarAdjunto(a.Nombre, string.IsNullOrEmpty(ext) ? null : ext, contentType,
                a.Contenido.LongLength, null, key, hash));
        }

        // Reutiliza el radicar base con los adjuntos ya subidos; folios totales si el request no los trae.
        var conAdjuntos = request with { Adjuntos = adjuntos, Folios = request.Folios ?? totalFolios };
        return await RadicarAsync(conAdjuntos, ct);
    }

    /// <summary>Cuenta paginas de un PDF por conteo de objetos /Type /Page (sin dependencia de PDF libs,
    /// calca ContarPaginasPdf de Documentos). Fallback 1.</summary>
    private static int ContarPaginasPdf(byte[] contenido)
    {
        try
        {
            var txt = System.Text.Encoding.Latin1.GetString(contenido);
            var count = System.Text.RegularExpressions.Regex.Matches(txt, @"/Type\s*/Page[^s]").Count;
            return count > 0 ? count : 1;
        }
        catch { return 1; }
    }
}
