using Tronox.Domain.Enums;

namespace Tronox.Application.Trd;

/// <summary>Resumen por dependencia para la pantalla de entrada de RF04 (una TRD por dependencia).</summary>
public sealed record DependenciaTrdResumenDto(
    long DependenciaId,
    string Codigo,
    string Nombre,
    int SeriesAsignadas,
    int SeriesActivas);

/// <summary>Una asignacion Serie->Dependencia (el cruce) con sus reglas TRD y metadatos.</summary>
public sealed record TrdAsignacionDto(
    long Id,
    long TrdVersionId,
    long DependenciaId,
    string DependenciaCodigo,
    string DependenciaNombre,
    long SerieId,
    string SerieCodigo,
    string SerieNombre,
    bool SerieEsSubserie,
    string CodigoCcd,
    int TiempoGestion,
    int TiempoCentral,
    DisposicionFinal DisposicionFinal,
    bool ReproduccionTecnica,
    bool SerieDdhhDih,
    string? Procedimiento,
    long NivelClasificacionId,
    string NivelClasificacionNombre,
    bool IsArchived,
    IReadOnlyList<TrdMetadatoDto> Metadatos);

public sealed record TrdMetadatoDto(
    long Id,
    long TrdAsignacionId,
    string Nombre,
    TipoDatoMetadato TipoDato,
    bool Obligatorio,
    int Orden,
    long? ListaMaestraId,
    string? ListaNombre,
    bool IsArchived);

/// <summary>Cabecera de una version para la pantalla RF04 (identidad + si es editable).</summary>
public sealed record TrdVersionCabeceraDto(
    long Id,
    string CodigoVersion,
    TrdVersionEstado Estado)
{
    public bool EnConstruccion => Estado == TrdVersionEstado.EnConstruccion;
    public bool Vigente => Estado == TrdVersionEstado.Vigente;
    public bool Editable => Estado == TrdVersionEstado.EnConstruccion;
    /// <summary>Solo consulta total (Historico/Inactivo): ni siquiera se agregan series.</summary>
    public bool SoloConsulta => Estado is TrdVersionEstado.Historico or TrdVersionEstado.Inactivo;
}

/// <summary>Alta de una asignacion (cruce Dependencia+Serie + reglas TRD, RF04 pasos 2-5).</summary>
public sealed record AddAsignacionRequest(
    long DependenciaId,
    long SerieId,
    int TiempoGestion,
    int TiempoCentral,
    DisposicionFinal DisposicionFinal,
    long NivelClasificacionId,
    bool ReproduccionTecnica = false,
    bool SerieDdhhDih = false,
    string? Procedimiento = null);

/// <summary>Edicion de las reglas TRD de una asignacion (RF04 paso 4-5).</summary>
public sealed record UpdateAsignacionRequest(
    int TiempoGestion,
    int TiempoCentral,
    DisposicionFinal DisposicionFinal,
    long NivelClasificacionId,
    bool ReproduccionTecnica = false,
    bool SerieDdhhDih = false,
    string? Procedimiento = null);

/// <summary>Alta/edicion de un metadato de expediente (RF04 paso 6).</summary>
public sealed record SaveMetadatoRequest(
    string Nombre,
    TipoDatoMetadato TipoDato,
    bool Obligatorio,
    long? ListaMaestraId = null);
