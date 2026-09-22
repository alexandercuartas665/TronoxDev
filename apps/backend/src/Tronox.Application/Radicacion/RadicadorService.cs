using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Documentos;
using Tronox.Application.Tenancy;
using Tronox.Application.Terceros;
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
    private readonly IRadicadoEstampador _estampador;
    private readonly ITerceroService _terceros;

    public RadicadorService(IApplicationDbContext db, ITenantContext tenant, ISequenceService sequences,
        ICalendarioHabilService calendario, IObjectStorage storage, IRadicadoEstampador estampador,
        ITerceroService terceros)
    {
        _db = db;
        _tenant = tenant;
        _sequences = sequences;
        _calendario = calendario;
        _storage = storage;
        _estampador = estampador;
        _terceros = terceros;
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

        // DAT-02: el remitente (entrada) / destinatario (salida) es un tercero del catalogo unico RQ07.
        // Si trae documento y no es anonimo, se hace upsert en terceros y se enlaza; nunca se crea una
        // tabla propia de personas externas (invariante 2). La interna no tiene tercero externo.
        long? remitenteTerceroId = null;
        if (!req.Anonimo && req.Tipo != RadicadoTipo.Interno && !string.IsNullOrWhiteSpace(req.RemitenteDocumento))
        {
            var esJuridica = string.Equals(req.RemitenteTipoDoc, "NIT", StringComparison.OrdinalIgnoreCase);
            var subtipo = esJuridica ? TerceroSubtipo.JuridicaPrivada : TerceroSubtipo.PersonaNatural;
            var terc = await _terceros.CrearRapidoAsync(new CrearRapidoRequest(
                subtipo, req.RemitenteTipoDoc ?? "CC", req.RemitenteDocumento!.Trim(),
                Nombre: esJuridica ? null : req.RemitenteNombre?.Trim(),
                Apellidos: null,
                RazonSocial: esJuridica ? req.RemitenteNombre?.Trim() : null,
                Email: req.RemitenteEmail, Telefono: req.RemitenteTelefono, MunicipioId: null,
                Origen: req.Tipo == RadicadoTipo.Salida ? "Salida" : "Radicacion"), ct);
            if (terc.Ok) { remitenteTerceroId = terc.Id; }
        }

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
            RemitenteTerceroId = remitenteTerceroId,
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
            // ---- Bloque de envio: solo en salidas. Nace Pendiente (el envio se registra desde la bandeja). ----
            CanalEnvio = req.Tipo == RadicadoTipo.Salida ? req.CanalEnvio : null,
            EstadoEnvio = req.Tipo == RadicadoTipo.Salida ? "Pendiente" : null,
            EsRespuestaDefinitiva = req.Tipo == RadicadoTipo.Salida && req.EsRespuestaDefinitiva,
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

        // RF05-5: la respuesta DEFINITIVA cierra el termino del radicado de entrada vinculado (Estado -> Respondido).
        // La salida parcial NO cambia el estado de la entrada. Todo en la misma transaccion que la salida.
        if (req.Tipo == RadicadoTipo.Salida && req.EsRespuestaDefinitiva && req.RadicadoRelacionadoId is long entradaId)
        {
            var entrada = await _db.Radicados.FirstOrDefaultAsync(
                r => r.Id == entradaId && r.Tipo == RadicadoTipo.Entrada, ct);
            if (entrada is not null && entrada.Estado != RadicadoEstado.Anulado)
            {
                entrada.Estado = RadicadoEstado.Respondido;
                entrada.Trazas.Add(new RadicadoTrazabilidad
                {
                    TenantId = tenantId.Value,
                    Accion = "RESPONDIDO",
                    Fecha = DateTime.UtcNow,
                    UsuarioId = _tenant.UserId,
                    Detalle = $"Respuesta definitiva con radicado de salida {numero} - termino cerrado (RF05-5)."
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return RadicarResult.Success(radicado.Id, numero);
    }

    public async Task<RadicarResult> AdjuntarAsync(long radicadoId, IReadOnlyList<AdjuntoBytes> archivos,
        CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) { return RadicarResult.Fail("Sesion no valida."); }
        if (archivos is not { Count: > 0 }) { return RadicarResult.Fail("No hay archivos para adjuntar."); }

        var radicado = await _db.Radicados.Include(r => r.Archivos)
            .FirstOrDefaultAsync(r => r.Id == radicadoId, ct);
        if (radicado is null) { return RadicarResult.Fail("Radicado no encontrado."); }

        var agregados = 0;
        foreach (var a in archivos)
        {
            if (a.Contenido is not { Length: > 0 }) { continue; }
            var ext = System.IO.Path.GetExtension(a.Nombre).TrimStart('.').ToLowerInvariant();
            var contentType = a.MimeType ?? DocumentoRules.ContentType(a.Nombre);
            var hash = DocumentoRules.HashSha256(a.Contenido);
            var key = $"{tenantId.Value}/{Guid.NewGuid():N}" + (string.IsNullOrEmpty(ext) ? "" : $".{ext}");
            using (var ms = new MemoryStream(a.Contenido, writable: false))
            {
                await _storage.PutAsync(key, ms, contentType, ct);
            }
            radicado.Archivos.Add(new RadicadoArchivo
            {
                TenantId = tenantId.Value,
                Nombre = a.Nombre,
                Extension = string.IsNullOrEmpty(ext) ? null : ext,
                MimeType = contentType,
                TamanoBytes = a.Contenido.LongLength,
                StorageKey = key,
                Sha256 = hash,
                FechaCarga = DateTime.UtcNow
            });
            agregados++;
        }
        if (agregados == 0) { return RadicarResult.Fail("No se adjunto ningun archivo valido."); }

        radicado.Trazas.Add(new RadicadoTrazabilidad
        {
            TenantId = tenantId.Value,
            Accion = "DIGITALIZAR",
            Fecha = DateTime.UtcNow,
            UsuarioId = _tenant.UserId,
            Detalle = $"Digitalizacion: {agregados} documento(s) adjuntado(s) al radicado {radicado.NumeroRadicado}."
        });

        await _db.SaveChangesAsync(ct);
        return RadicarResult.Success(radicado.Id, radicado.NumeroRadicado);
    }

    public async Task<RadicarResult> RadicarConArchivosAsync(RadicarNuevoRequest request,
        IReadOnlyList<AdjuntoBytes> archivos, bool estampar = false, double estampaX = 62, double estampaY = 6,
        CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) { return RadicarResult.Fail("Sesion no valida."); }
        if (archivos is not { Count: > 0 }) { return await RadicarAsync(request, ct); }

        // Folios totales por conteo de paginas (para fijar Folios en la cabecera antes de radicar).
        var totalFolios = 0;
        foreach (var a in archivos)
        {
            if (a.Contenido is not { Length: > 0 }) { continue; }
            var ext0 = System.IO.Path.GetExtension(a.Nombre).TrimStart('.').ToLowerInvariant();
            totalFolios += ext0 == "pdf" ? ContarPaginasPdf(a.Contenido) : 1;
        }

        // 1) Radica PRIMERO (sin archivos) para obtener el numero real, necesario para la estampa (RF02-5).
        var res = await RadicarAsync(request with { Adjuntos = null, Folios = request.Folios ?? totalFolios }, ct);
        if (!res.Ok || res.RadicadoId is not long rid) { return res; }

        // 2) Estampa cada PDF con el numero real en la posicion elegida por el operador, sube a object
        //    storage (invariante 9) y cuelga los archivos del radicado. Key opaca tenant-scoped.
        var fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        var radicado = await _db.Radicados.Include(r => r.Archivos).FirstOrDefaultAsync(r => r.Id == rid, ct);
        if (radicado is null) { return res; }
        var agregados = 0;
        foreach (var a in archivos)
        {
            if (a.Contenido is not { Length: > 0 }) { continue; }
            var ext = System.IO.Path.GetExtension(a.Nombre).TrimStart('.').ToLowerInvariant();
            var contentType = a.MimeType ?? DocumentoRules.ContentType(a.Nombre);
            var bytes = estampar && ext == "pdf"
                ? _estampador.EstamparRadicado(a.Contenido, res.Numero ?? "", fecha, estampaX, estampaY)
                : a.Contenido;
            var hash = DocumentoRules.HashSha256(bytes);
            var key = $"{tenantId.Value}/{Guid.NewGuid():N}" + (string.IsNullOrEmpty(ext) ? "" : $".{ext}");
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                await _storage.PutAsync(key, ms, contentType, ct);
            }
            radicado.Archivos.Add(new RadicadoArchivo
            {
                TenantId = tenantId.Value,
                Nombre = a.Nombre,
                Extension = string.IsNullOrEmpty(ext) ? null : ext,
                MimeType = contentType,
                TamanoBytes = bytes.LongLength,
                StorageKey = key,
                Sha256 = hash,
                FechaCarga = DateTime.UtcNow
            });
            agregados++;
        }
        if (agregados > 0)
        {
            radicado.Trazas.Add(new RadicadoTrazabilidad
            {
                TenantId = tenantId.Value,
                Accion = "ADJUNTAR",
                Fecha = DateTime.UtcNow,
                UsuarioId = _tenant.UserId,
                Detalle = estampar
                    ? $"{agregados} documento(s) electronico(s) aportado(s) y estampado(s) (RF02-5)."
                    : $"{agregados} documento(s) electronico(s) aportado(s)."
            });
            await _db.SaveChangesAsync(ct);
        }
        return res;
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
