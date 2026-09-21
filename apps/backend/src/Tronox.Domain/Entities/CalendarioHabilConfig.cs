using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Configuracion del calendario habil de la entidad (RQ01, legacy ctrlCalendarioHabil). Define que dias
/// de la semana son habiles y la jornada laboral. Base para el calculo de terminos SLA de radicacion junto
/// con los <see cref="DiaFestivo"/>. Una sola fila por tenant. TENANT-SCOPED.
/// </summary>
public class CalendarioHabilConfig : TenantEntity
{
    public bool Lunes { get; set; } = true;
    public bool Martes { get; set; } = true;
    public bool Miercoles { get; set; } = true;
    public bool Jueves { get; set; } = true;
    public bool Viernes { get; set; } = true;
    public bool Sabado { get; set; }
    public bool Domingo { get; set; }

    /// <summary>Hora de inicio de jornada en formato "HH:mm".</summary>
    public string JornadaInicio { get; set; } = "08:00";

    /// <summary>Hora de fin de jornada en formato "HH:mm".</summary>
    public string JornadaFin { get; set; } = "17:00";

    public bool Activo { get; set; } = true;
}
