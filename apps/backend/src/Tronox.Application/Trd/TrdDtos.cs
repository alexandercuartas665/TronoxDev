using Tronox.Domain.Enums;

namespace Tronox.Application.Trd;

/// <summary>
/// Estado DERIVADO de la TRD de una dependencia dentro de una version (no se persiste). Reproduce el
/// badge del legacy doc_tablaRetencionDocumental sin una entidad "TRD por dependencia": se calcula a
/// partir del numero de series activas y del estado de la version (ver ADR-007).
/// </summary>
public enum EstadoTrdDependencia
{
    /// <summary>La dependencia no tiene ninguna serie asignada en esta version.</summary>
    SinTrd = 0,
    /// <summary>Tiene series y la version esta En Construccion.</summary>
    EnConstruccion = 1,
    /// <summary>Tiene series y la version esta Vigente (TRD activa).</summary>
    Activa = 2,
    /// <summary>Tiene series y la version es Historico.</summary>
    Historica = 3,
    /// <summary>Tiene series y la version es Inactivo.</summary>
    Inactiva = 4
}

/// <summary>Resumen por dependencia para la LISTA de entrada de RF04 ("Gestion de TRD por dependencia").
/// Columnas fieles al legacy gvDependenciasTRD: codigo, dependencia, version TRD, estado TRD, fecha
/// creacion, usuario creador, acciones. Fecha/usuario se DERIVAN de la primera asignacion (ADR-007:
/// no hay entidad cabecera por dependencia).</summary>
public sealed record DependenciaTrdResumenDto(
    long DependenciaId,
    string Codigo,
    string Nombre,
    int SeriesAsignadas,
    int SeriesActivas,
    EstadoTrdDependencia Estado,
    DateTimeOffset? FechaCreacion,
    long? CreadoPorId);

/// <summary>Una asignacion Serie->Dependencia (el cruce) con sus reglas TRD, metadatos y tipologias.</summary>
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
    long? SerieParentId,
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
    IReadOnlyList<TrdMetadatoDto> Metadatos,
    IReadOnlyList<TrdTipologiaDto> Tipologias);

/// <summary>Metadato de expediente (RF04) o de documento (RF05, colgado de una tipologia).</summary>
public sealed record TrdMetadatoDto(
    long Id,
    long TrdAsignacionId,
    long? TrdTipologiaId,
    string Nombre,
    TipoDatoMetadato TipoDato,
    bool Obligatorio,
    int Orden,
    long? ListaMaestraId,
    string? ListaNombre,
    ContextoMetadato Contexto,
    bool IsArchived);

/// <summary>Tipo documental (RF05) de una asignacion, con sus metadatos de documento.</summary>
public sealed record TrdTipologiaDto(
    long Id,
    long TrdAsignacionId,
    string Nombre,
    SoporteTipologia Soporte,
    string? Formato,
    bool ObligatorioEnExpediente,
    bool IsArchived,
    IReadOnlyList<TrdMetadatoDto> Metadatos);

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

/// <summary>Alta/edicion de un metadato (RF04 paso 6 = expediente / RF05 3.5.3 = documento).</summary>
public sealed record SaveMetadatoRequest(
    string Nombre,
    TipoDatoMetadato TipoDato,
    bool Obligatorio,
    long? ListaMaestraId = null);

/// <summary>Alta/edicion de un tipo documental (RF05 3.5.2).</summary>
public sealed record SaveTipologiaRequest(
    string Nombre,
    SoporteTipologia Soporte,
    string? Formato = null,
    bool ObligatorioEnExpediente = false);
