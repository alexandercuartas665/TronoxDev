using Tronox.Domain.Enums;

namespace Tronox.Application.TrdVersiones;

/// <summary>Vista de una version de TRD para el listado y el detalle (RF01).</summary>
public sealed record TrdVersionDto(
    long Id,
    string CodigoVersion,
    string? Descripcion,
    string? ActoAdministrativo,
    DateOnly FechaVigenciaDesde,
    DateOnly? FechaAprobacion,
    DateOnly? FechaConvalidacion,
    TrdVersionEstado Estado)
{
    public bool EsVigente => Estado == TrdVersionEstado.Vigente;
    public bool EnConstruccion => Estado == TrdVersionEstado.EnConstruccion;
}

/// <summary>KPIs del modulo: total, si hay una Vigente, en construccion e historicas.</summary>
public sealed record TrdVersionKpisDto(int Total, bool HayVigente, int EnConstruccion, int Historicas);

/// <summary>
/// Alta/edicion de una version (RF01 3.1.1). El estado NO se fija aqui: una version nace
/// EnConstruccion y cambia con ActivarAsync / DescartarAsync.
/// </summary>
public sealed record SaveTrdVersionRequest(
    string CodigoVersion,
    DateOnly FechaVigenciaDesde,
    string? Descripcion = null,
    string? ActoAdministrativo = null,
    DateOnly? FechaAprobacion = null,
    DateOnly? FechaConvalidacion = null);
