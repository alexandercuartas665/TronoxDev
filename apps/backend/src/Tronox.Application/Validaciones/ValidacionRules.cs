using Tronox.Domain.Enums;

namespace Tronox.Application.Validaciones;

/// <summary>Reglas puras de las tareas de validacion (RQ04 - RF11/RF12). Sin EF: testeable sin base.</summary>
public static class ValidacionRules
{
    /// <summary>Estados validos como RESPUESTA a una tarea (no incluye Pendiente).</summary>
    public static bool EsRespuestaValida(EstadoValidacion estado)
        => estado is EstadoValidacion.Aprobado or EstadoValidacion.Devuelto or EstadoValidacion.Rechazado;

    /// <summary>Devolver y Rechazar EXIGEN comentario (RF11 CA-3).</summary>
    public static bool RequiereComentario(EstadoValidacion estado)
        => estado is EstadoValidacion.Devuelto or EstadoValidacion.Rechazado;

    public static string? ValidateRespuesta(EstadoValidacion nuevoEstado, string? comentario)
    {
        if (!EsRespuestaValida(nuevoEstado)) { return "Respuesta invalida."; }
        if (RequiereComentario(nuevoEstado) && string.IsNullOrWhiteSpace(comentario))
        {
            return "El comentario es obligatorio al devolver o rechazar (RF11).";
        }
        return null;
    }

    /// <summary>Dias restantes hasta la fecha limite (negativo si vencida), o null si no hay fecha.</summary>
    public static int? DiasRestantes(DateOnly? fechaLimite, DateOnly hoy)
        => fechaLimite is DateOnly f ? f.DayNumber - hoy.DayNumber : null;
}
