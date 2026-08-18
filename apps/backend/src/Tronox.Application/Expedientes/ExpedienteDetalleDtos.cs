using Tronox.Domain.Enums;

namespace Tronox.Application.Expedientes;

// ---- Ubicacion fisica (RF12) ----

public sealed record TopografiaOpcionDto(long Id, string Codigo, string Nombre);

/// <summary>
/// Nodo de la topografia fisica para la cascada de Asignar/Cambiar ubicacion (RF12), calcada del
/// legacy (ObtenerElementosRaiz/Hijos + ValidarUbicacionAsignable). Aplana el arbol con enlace al
/// padre para que la UI arme la cascada por niveles (raiz -> hoja). <c>EsHoja</c> marca los nodos
/// finales (sin hijos), unicos asignables; <c>Asignable</c> es true si esa hoja no esta Llena ni
/// pertenece a una rama Inactiva; <c>Motivo</c> lleva el bloqueo cuando no lo es.
/// </summary>
public sealed record TopografiaCascadaNodoDto(
    long Id,
    long? ParentId,
    int NivelOrden,
    string NivelNombre,
    string Nombre,
    string Sigla,
    string Codigo,
    TopografiaEstado Estado,
    bool EsHoja,
    bool Asignable,
    string? Motivo);

public sealed record UbicacionActualDto(
    long TopografiaElementoId, string Ubicacion, FaseArchivo Fase, string? AsignadoPor, DateTimeOffset Fecha);

public sealed record UbicacionHistorialItemDto(
    long Id, string Ubicacion, FaseArchivo Fase, string? AsignadoPor, DateTimeOffset Fecha, string? Observacion);

public sealed record ExpedienteUbicacionDto(
    UbicacionActualDto? Actual, IReadOnlyList<UbicacionHistorialItemDto> Historial);

// ---- Vinculos (RF14) ----

public sealed record VinculoDto(
    long VinculoId, long OtroExpedienteId, string OtroCodigo, string OtroNombre, EstadoExpediente OtroEstado,
    string? VinculadoPor, DateTimeOffset Fecha, string? Observacion);

public sealed record VinculoBusquedaDto(long Id, string Codigo, string Nombre, EstadoExpediente Estado);

// ---- Trazabilidad (RF09) ----

public sealed record TrazaItemDto(
    string Accion, string? Usuario, DateTimeOffset Fecha, string? Detalle);

// ---- Cierre / reapertura (RF08) ----

public sealed record CierreItemDto(
    int NumeroCierre, string HashSha256, string? Usuario, DateTimeOffset Fecha, bool EsReapertura, string? Justificacion);
