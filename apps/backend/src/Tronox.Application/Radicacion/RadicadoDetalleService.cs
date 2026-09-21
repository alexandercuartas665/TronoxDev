using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion del detalle (rad_detalle). Gate de visibilidad fail-closed; LINQ parametrizado; el
/// binario de los archivos NO se lee (solo metadatos + referencia a storage, invariante 9). El endpoint
/// de descarga hace stream desde object storage aparte.
/// </summary>
public sealed class RadicadoDetalleService : IRadicadoDetalleService
{
    private readonly IApplicationDbContext _db;
    private readonly RadicacionVisibilidadService _vis;

    public RadicadoDetalleService(IApplicationDbContext db, RadicacionVisibilidadService vis)
    {
        _db = db;
        _vis = vis;
    }

    private static readonly string[] Previsualizables = { "pdf", "txt", "csv", "log", "jpg", "jpeg", "png", "gif", "bmp" };

    public async Task<RadicadoDetalleDto?> ObtenerAsync(long radicadoId, CancellationToken ct = default)
    {
        if (!await _vis.PuedeVerAsync(radicadoId, ct)) { return null; }

        var r = await _db.Radicados.AsNoTracking()
            .Where(x => x.Id == radicadoId)
            .Select(x => new
            {
                x.Id, x.NumeroRadicado, x.Tipo, x.Estado, x.Canal, x.Asunto, x.Descripcion, x.FechaRadicacion,
                x.Anonimo, x.RemitenteNombre, x.RemitenteTipoDoc, x.RemitenteDocumento, x.RemitenteEmail, x.RemitenteTelefono,
                x.Prioridad, x.Folios, x.NumAnexos, x.Soporte, x.FechaVencimiento, x.FechaDistribucion,
                x.RadicadoRelacionadoId,
                TipoNombre = x.TipoComunicacion != null ? x.TipoComunicacion.Nombre : null,
                TipoColor = x.TipoComunicacion != null ? x.TipoComunicacion.Color : null,
                EsPqrsd = x.TipoComunicacion != null && x.TipoComunicacion.EsPqrsd,
                EsTutela = x.TipoComunicacion != null && x.TipoComunicacion.EsTutela,
                DiasTermino = x.TipoComunicacion != null ? x.TipoComunicacion.DiasRespuesta : null,
                TipoDia = x.TipoComunicacion != null && x.TipoComunicacion.TipoDia != null ? x.TipoComunicacion.TipoDia.ToString() : null,
                DepNombre = x.DependenciaDestino != null ? x.DependenciaDestino.Name : null,
                DepOrigenNombre = x.DependenciaOrigen != null ? x.DependenciaOrigen.Name : null,
                Nivel = x.NivelReserva != null ? x.NivelReserva.Nombre : null,
                FuncNombre = _db.TenantUsers.Where(u => u.Id == x.FuncionarioAsignadoId).Select(NombreUsuario()).FirstOrDefault(),
                FuncOrigen = _db.TenantUsers.Where(u => u.Id == x.FuncionarioOrigenId).Select(NombreUsuario()).FirstOrDefault(),
                Operador = _db.TenantUsers.Where(u => u.Id == x.UsuarioRadicaId).Select(NombreUsuario()).FirstOrDefault(),
                RelNumero = x.RadicadoRelacionado != null ? x.RadicadoRelacionado.NumeroRadicado : null
            })
            .FirstOrDefaultAsync(ct);
        if (r is null) { return null; }

        var hoy = DateTime.UtcNow.Date;
        int? dias = r.FechaVencimiento is null ? null : (int)(r.FechaVencimiento.Value.Date - hoy).TotalDays;

        var info = new RadicadoInfoDto(
            r.Id, r.NumeroRadicado, r.Tipo, r.TipoNombre, r.TipoColor, r.EsPqrsd, r.EsTutela,
            r.Estado.ToString(), r.Canal.ToString(), r.Asunto, r.Descripcion,
            r.FechaRadicacion.ToString("dd/MM/yyyy HH:mm"), r.Anonimo,
            r.Anonimo ? null : r.RemitenteNombre, r.RemitenteTipoDoc, r.RemitenteDocumento, r.RemitenteEmail, r.RemitenteTelefono,
            r.Nivel, r.DepNombre, r.DepOrigenNombre, r.FuncOrigen, r.FuncNombre, r.Prioridad.ToString(),
            r.Folios, r.NumAnexos, r.Soporte, r.RelNumero, r.Operador,
            r.FechaVencimiento?.ToString("dd/MM/yyyy"), r.FechaDistribucion?.ToString("dd/MM/yyyy HH:mm"),
            dias, r.DiasTermino, r.TipoDia);

        var docs = await _db.RadicadosArchivos.AsNoTracking().Where(a => a.RadicadoId == radicadoId)
            .OrderBy(a => a.FechaCarga)
            .Select(a => new RadicadoDocDto(a.Id, a.Nombre, a.Extension, a.TamanoBytes / 1024,
                a.FechaCarga.ToString("dd/MM/yyyy HH:mm"),
                a.Extension != null && Previsualizables.Contains(a.Extension.ToLower())))
            .ToListAsync(ct);

        var traza = await _db.RadicadosTrazabilidad.AsNoTracking().Where(t => t.RadicadoId == radicadoId)
            .OrderBy(t => t.Fecha).ThenBy(t => t.Id)
            .Select(t => new
            {
                t.Fecha, t.Accion, t.Detalle,
                Usuario = _db.TenantUsers.Where(u => u.Id == t.UsuarioId).Select(NombreUsuario()).FirstOrDefault()
            }).ToListAsync(ct);
        var trazaDto = traza.Select(t => new RadicadoTrazaDto(
            t.Fecha.ToString("dd/MM/yyyy HH:mm"), t.Usuario, t.Accion.Replace('_', ' '), t.Detalle)).ToList();

        var tareas = await _db.RadicadosTareas.AsNoTracking().Where(t => t.RadicadoId == radicadoId)
            .OrderByDescending(t => t.FechaAsignacion)
            .Select(t => new
            {
                t.Estado, t.Activa, t.Prioridad, t.Instrucciones, t.FechaAsignacion, t.Observacion,
                DepNombre = t.Dependencia != null ? t.Dependencia.Name : "",
                Funcionario = _db.TenantUsers.Where(u => u.Id == t.FuncionarioId).Select(NombreUsuario()).FirstOrDefault(),
                DistribuidoPor = _db.TenantUsers.Where(u => u.Id == t.DistribuidoPorId).Select(NombreUsuario()).FirstOrDefault()
            }).ToListAsync(ct);
        var tareasDto = tareas.Select(t => new RadicadoTareaDto(
            t.DepNombre, t.Funcionario, t.Estado.ToString(), t.Activa, t.Prioridad.ToString(),
            t.Instrucciones, t.FechaAsignacion.ToString("dd/MM/yyyy HH:mm"), t.DistribuidoPor, t.Observacion)).ToList();

        var comuns = await _db.RadicadosComunicaciones.AsNoTracking().Where(c => c.RadicadoId == radicadoId)
            .OrderByDescending(c => c.Fecha)
            .Select(c => new
            {
                c.Fecha, c.Canal, c.Destino, c.Detalle, c.Estado,
                Usuario = _db.TenantUsers.Where(u => u.Id == c.UsuarioId).Select(NombreUsuario()).FirstOrDefault()
            }).ToListAsync(ct);
        var comunsDto = comuns.Select(c => new RadicadoComDto(
            c.Fecha.ToString("dd/MM/yyyy HH:mm"), c.Usuario, c.Canal, c.Destino, c.Detalle, c.Estado)).ToList();

        var tareasActivas = tareas.Count(t => t.Activa);

        RadicadoVinculoDto? padre = null;
        if (r.RadicadoRelacionadoId is long pid)
        {
            padre = await _db.Radicados.AsNoTracking().Where(p => p.Id == pid)
                .Select(p => new RadicadoVinculoDto(p.Id, p.NumeroRadicado, p.Estado.ToString(), p.Tipo))
                .FirstOrDefaultAsync(ct);
        }

        var salidas = await _db.Radicados.AsNoTracking()
            .Where(s => s.RadicadoRelacionadoId == radicadoId && s.Tipo == RadicadoTipo.Salida)
            .OrderBy(s => s.FechaRadicacion)
            .Select(s => new RadicadoSalidaDto(s.Id, s.NumeroRadicado, s.FechaRadicacion.ToString("dd/MM/yyyy"),
                s.EsRespuestaDefinitiva, s.EstadoEnvio, s.CanalEnvio))
            .ToListAsync(ct);

        return new RadicadoDetalleDto(info, docs, trazaDto, tareasDto, comunsDto, tareasActivas, padre, salidas);
    }

    // Proyeccion reutilizable del nombre visible de un TenantUser (nombres+apellidos o email).
    private static System.Linq.Expressions.Expression<Func<TenantUser, string>> NombreUsuario()
        => u => (u.Nombres + " " + u.Apellidos).Trim() != "" ? (u.Nombres + " " + u.Apellidos).Trim() : u.Email;
}
