using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Documentos;

namespace Tronox.Web.Visor;

/// <summary>
/// Endpoints de datos del visor documental (RF04), calcados de exp_visor_data.ashx / doc_visor.ashx.
/// El visor (wwwroot/visor/exp_visor.js, pdf.js) consume estas rutas:
///  - GET  /visor/bin?doc=N[&amp;dl=1]  -> binario del documento (inline para pdf.js; descarga si dl=1).
///  - GET  /visor/data?op=traz|vers|ocr|tipos|campos&amp;doc=N[&amp;tipo=T] -> JSON.
///  - POST /visor/data (op=guardar)   -> guarda metadatos.
///  - GET  /visor/anotaciones?op=list -> anotaciones (diferido: lista vacia).
/// Reusa IDocumentoService (binario + metadatos) y la auditoria para la trazabilidad.
/// </summary>
public static class VisorEndpoints
{
    public static void MapVisorEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/visor").RequireAuthorization("TenantMember");

        // ---- Binario (doc_visor.ashx) ----
        g.MapGet("/bin", async (HttpContext http, long doc, IDocumentoService svc) =>
        {
            var actor = ActorId(http);
            var res = await svc.DescargarAsync(doc, actor);
            if (!res.IsOk || res.Value is null) { return Results.NotFound(); }
            var d = res.Value;
            var descargar = http.Request.Query["dl"] == "1";
            // Inline para pdf.js; con nombre de archivo solo cuando se pide descargar.
            return descargar
                ? Results.File(d.Contenido, d.ContentType, d.NombreArchivo)
                : Results.File(d.Contenido, d.ContentType);
        });

        // ---- Datos (exp_visor_data.ashx) GET ----
        g.MapGet("/data", async (HttpContext http, string op, long doc, long? tipo,
            IDocumentoService svc, IApplicationDbContext db) =>
        {
            var actor = ActorId(http);
            switch ((op ?? "").ToLowerInvariant())
            {
                case "traz":
                    return Results.Json(await TrazabilidadAsync(db, doc));

                case "vers":
                    // RF03 (versionado) diferido: sin historial de versiones aun.
                    return Results.Json(Array.Empty<object>());

                case "ocr":
                {
                    var docRow = await db.Documentos.AsNoTracking()
                        .Where(x => x.Id == doc).Select(x => new { x.OcrEstado }).FirstOrDefaultAsync();
                    return Results.Json(new { estado = docRow?.OcrEstado.ToString() ?? "NoAplica", texto = (string?)null });
                }

                case "reocr":
                    // OCR diferido: no reprocesa.
                    return Results.Json(new { success = false, error = "El OCR se habilita en un avance posterior." });

                case "tipos":
                {
                    var m = await svc.GetEditarMetadatosAsync(doc, actor);
                    if (!m.IsOk || m.Value is null) { return Results.Json(Array.Empty<object>()); }
                    return Results.Json(m.Value.Tipos.Select(t => new { reg = t.Id, nombre = t.Nombre }));
                }

                case "campos":
                {
                    if (tipo is not long t || t <= 0) { return Results.Json(Array.Empty<object>()); }
                    var campos = await svc.GetMetadatosTipologiaConValoresAsync(doc, t);
                    return Results.Json(campos.Select(c => new
                    {
                        reg = c.TrdMetadatoId,
                        nombre = c.Nombre,
                        tipo = c.TipoDato.ToString(),
                        obligatorio = c.Obligatorio,
                        valor = c.Valor ?? ""
                    }));
                }

                default:
                    return Results.Json(new { });
            }
        });

        // ---- Guardar metadatos (exp_visor_data.ashx op=guardar) POST ----
        g.MapPost("/data", async (HttpContext http, IDocumentoService svc) =>
        {
            var actor = ActorId(http);
            var form = await http.Request.ReadFormAsync();
            var op = form["op"].ToString();
            if (!string.Equals(op, "guardar", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new { error = "Operacion no soportada." });
            }
            if (!long.TryParse(form["doc"], out var doc)) { return Results.Json(new { error = "Documento invalido." }); }

            var nombre = form["nombre"].ToString().Trim();
            var fechaStr = form["fecha"].ToString().Trim();
            DateOnly? fecha = DateOnly.TryParse(fechaStr, out var f) ? f : null;
            long? tipo = long.TryParse(form["tipo"], out var tp) && tp > 0 ? tp : null;

            var metas = new List<DocMetadatoInput>();
            foreach (var key in form.Keys)
            {
                if (key.StartsWith("meta_", StringComparison.Ordinal)
                    && long.TryParse(key.AsSpan(5), out var mReg) && mReg > 0)
                {
                    metas.Add(new DocMetadatoInput(mReg, form[key].ToString()));
                }
            }

            var res = await svc.GuardarMetadatosAsync(
                new GuardarMetadatosRequest(doc, nombre, fecha, tipo, metas), actor);
            return res.IsOk
                ? Results.Json(new { success = true })
                : Results.Json(new { error = res.Error ?? "No se pudo guardar." });
        });

        // ---- Anotaciones (doc_anotaciones.ashx) — diferido ----
        g.MapGet("/anotaciones", (string? op) => Results.Json(Array.Empty<object>()));
        g.MapPost("/anotaciones", () => Results.Json(new { error = "Las anotaciones se habilitan en un avance posterior." }));
    }

    private static long ActorId(HttpContext http)
        => long.TryParse(http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>Trazabilidad del documento desde la auditoria (append-only). Calca los campos del legacy op=traz.</summary>
    private static async Task<IReadOnlyList<object>> TrazabilidadAsync(IApplicationDbContext db, long doc)
    {
        var logs = await db.SuperAdminAuditLogs.AsNoTracking()
            .Where(l => l.EntityName == nameof(Tronox.Domain.Entities.Documento) && l.EntityId == doc)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new { l.ActionName, l.ActorUserId, l.CreatedAt, l.NewValue })
            .ToListAsync();

        var actorIds = logs.Select(l => l.ActorUserId).Distinct().ToList();
        var nombres = await db.PlatformUsers.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, Nombre = u.DisplayName ?? u.Email })
            .ToListAsync();
        var nomMap = nombres.ToDictionary(n => n.Id, n => n.Nombre);

        return logs.Select(l => (object)new
        {
            accion = AccionLegible(l.ActionName),
            usuario = l.ActorUserId != 0 && nomMap.TryGetValue(l.ActorUserId, out var n) ? n : "(sistema)",
            fecha = l.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            cargo = "",
            rol = "",
            ip = "",
            detalle = l.NewValue
        }).ToList();
    }

    private static string AccionLegible(string action) => action switch
    {
        "documento.crear_borrador" => "Creacion de borrador",
        "documento.crear_fisico" => "Creacion (fisico)",
        "documento.editar_borrador" => "Edicion de borrador",
        "documento.editar_metadatos" => "Edicion de metadatos",
        "documento.incorporar" => "Incorporacion a expediente",
        "documento.archivar" => "Archivado",
        _ => action
    };
}
