using System.Text.Json;
using Tronox.Application.Common;
using Tronox.Application.Tenancy;
using Tronox.Application.Terceros;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Application.Radicacion.Agentes;

/// <summary>
/// Primer toolset de agente de TRONOX (RQ16): permite a un agente OPERAR sobre el dominio documental.
/// Herramientas: buscar_tercero (lectura, DAT-02) y radicar_entrada (crea un radicado de entrada,
/// reutilizando IRadicadorService, resolviendo el tipo de comunicacion PQRSD del tenant). En modo
/// SUGERENCIA (autonomous=false) no radica: solo informa lo que registraria, para que un humano confirme.
/// </summary>
public sealed class RadicacionToolset : IAgentToolset
{
    private readonly IApplicationDbContext _db;
    private readonly IRadicadorService _radicador;
    private readonly ITerceroService _terceros;

    public RadicacionToolset(IApplicationDbContext db, IRadicadorService radicador, ITerceroService terceros)
    {
        _db = db;
        _radicador = radicador;
        _terceros = terceros;
    }

    public string GroupKey => "radicacion";
    public string GroupLabel => "Radicacion documental";

    public IReadOnlyList<AiToolSpec> GetSpecs() => new[]
    {
        new AiToolSpec(
            "buscar_tercero",
            "Busca un tercero (ciudadano/empresa) por su documento de identidad. Devuelve sus datos si ya existe en el catalogo. Usalo antes de radicar para no duplicar personas.",
            """
            {"type":"object","properties":{
              "tipo_documento":{"type":"string","description":"Tipo de documento (ej. CC, NIT, CE). Por defecto CC."},
              "numero_documento":{"type":"string","description":"Numero del documento a buscar."}
            },"required":["numero_documento"]}
            """),
        new AiToolSpec(
            "radicar_entrada",
            "Radica un documento de ENTRADA (p.ej. una PQRSD ciudadana) en el sistema de gestion documental. Devuelve el numero de radicado. Usalo cuando ya tengas el asunto y los datos del remitente.",
            """
            {"type":"object","properties":{
              "asunto":{"type":"string","description":"Asunto breve del radicado."},
              "descripcion":{"type":"string","description":"Descripcion/resumen del contenido."},
              "remitente_nombre":{"type":"string","description":"Nombre del remitente/peticionario."},
              "remitente_documento":{"type":"string","description":"Documento del remitente (opcional)."},
              "remitente_email":{"type":"string","description":"Correo del remitente (opcional)."},
              "remitente_telefono":{"type":"string","description":"Telefono del remitente (opcional)."}
            },"required":["asunto"]}
            """)
    };

    public async Task<AgentToolResult> ExecuteAsync(string toolName, string argumentsJson, long actorUserId, bool autonomous, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            var args = doc.RootElement;
            return toolName switch
            {
                "buscar_tercero" => await BuscarTerceroAsync(args, cancellationToken),
                "radicar_entrada" => await RadicarEntradaAsync(args, autonomous, cancellationToken),
                _ => Fail($"Herramienta desconocida: {toolName}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"Error al ejecutar {toolName}: {ex.Message}");
        }
    }

    private async Task<AgentToolResult> BuscarTerceroAsync(JsonElement args, CancellationToken ct)
    {
        var tipoDoc = Str(args, "tipo_documento") ?? "CC";
        var numDoc = Str(args, "numero_documento");
        if (string.IsNullOrWhiteSpace(numDoc)) { return Fail("Falta numero_documento."); }

        var t = await _terceros.BuscarPorDocumentoAsync(tipoDoc, numDoc!, ct);
        if (t is null)
        {
            return Ok(new { encontrado = false, mensaje = "No existe un tercero con ese documento." });
        }
        var nombre = !string.IsNullOrWhiteSpace(t.RazonSocial) ? t.RazonSocial
            : string.Join(" ", new[] { t.Nombre, t.Apellidos }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return Ok(new { encontrado = true, t.Id, nombre, t.TipoDocumento, t.NumeroDocumento, t.Email, t.Telefono });
    }

    private async Task<AgentToolResult> RadicarEntradaAsync(JsonElement args, bool autonomous, CancellationToken ct)
    {
        var asunto = Str(args, "asunto");
        if (string.IsNullOrWhiteSpace(asunto)) { return Fail("Falta el asunto para radicar."); }
        var descripcion = Str(args, "descripcion");
        var remNombre = Str(args, "remitente_nombre");
        var remDoc = Str(args, "remitente_documento");
        var remEmail = Str(args, "remitente_email");
        var remTel = Str(args, "remitente_telefono");

        // Resuelve el tipo de comunicacion de ENTRADA del tenant (prefiere el PQRSD). Sin esto no se puede radicar.
        var tipo = await _db.TiposComunicacion.AsNoTracking()
            .Where(t => t.Activo && t.Direccion == RadicacionDireccion.Entrada)
            .OrderByDescending(t => t.EsPqrsd).ThenBy(t => t.Nombre)
            .Select(t => new { t.Id, t.Nombre, t.EsPqrsd })
            .FirstOrDefaultAsync(ct);
        if (tipo is null)
        {
            return Fail("El tenant no tiene un tipo de comunicacion de Entrada configurado. Configura uno en Radicacion.");
        }

        // Modo sugerencia: no radica, informa lo que registraria para que un humano confirme.
        if (!autonomous)
        {
            return Ok(new
            {
                radicado = false,
                modo = "sugerencia",
                mensaje = "Listo para radicar; un humano debe confirmar.",
                tipo_comunicacion = tipo.Nombre,
                asunto,
                remitente = remNombre
            });
        }

        var req = new RadicarNuevoRequest(
            Tipo: RadicadoTipo.Entrada,
            TipoComunicacionId: tipo.Id,
            Asunto: asunto,
            Descripcion: descripcion,
            Canal: RadicadoCanal.Correo,
            Anonimo: string.IsNullOrWhiteSpace(remNombre) && string.IsNullOrWhiteSpace(remDoc),
            RemitenteNombre: remNombre,
            RemitenteEmail: remEmail,
            RemitenteTipoDoc: string.IsNullOrWhiteSpace(remDoc) ? null : "CC",
            RemitenteDocumento: remDoc,
            RemitenteTelefono: remTel);

        var res = await _radicador.RadicarAsync(req, ct);
        if (!res.Ok)
        {
            return Fail(res.Error ?? "No se pudo radicar.");
        }
        // Radicar cierra la sesion del agente (deja la cache lista para una nueva atencion).
        return new AgentToolResult(
            JsonSerializer.Serialize(new { ok = true, radicado = true, numero = res.Numero, radicado_id = res.RadicadoId, tipo_comunicacion = tipo.Nombre }),
            SessionCompleted: true);
    }

    private static string? Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static AgentToolResult Ok(object payload) =>
        new(JsonSerializer.Serialize(payload));

    private static AgentToolResult Fail(string error) =>
        new(JsonSerializer.Serialize(new { ok = false, error }));
}
