namespace Tronox.Application.Documentos;

/// <summary>Destinatario candidato del buscador de Compartir (RF07): usuario ("U") o rol ("R").</summary>
public sealed record DestinoBusquedaDto(string Tipo, long Id, string Nombre, string Sub);

/// <summary>Un destinatario elegido para otorgar acceso. Tipo "U" (PlatformUserId) o "R" (RolId).</summary>
public sealed record DestinoInput(string Tipo, long Id);

/// <summary>
/// Peticion de compartir (RF07). "Ver" es implicito (siempre se otorga); solo se piden los opcionales
/// Editar metadatos y Descargar. Un rol se expande a sus usuarios en el momento (snapshot).
/// </summary>
public sealed record CompartirRequest(
    long DocId, IReadOnlyList<DestinoInput> Destinos, bool PuedeEditarMetadatos, bool PuedeDescargar);

/// <summary>Acceso activo de un documento, para la lista "Accesos actuales" del modal.</summary>
public sealed record CompartidoActivoDto(
    long BeneficiarioPlatformUserId, string Nombre, bool PuedeEditarMetadatos, bool PuedeDescargar, bool DesdeRol);

/// <summary>Fila de la bandeja "Compartidos conmigo": un documento compartido con el usuario actual.</summary>
public sealed record CompartidoConmigoDto(
    long DocId, string Nombre, string? Formato, string? TipologiaNombre, bool TieneBinario,
    string CompartidoPor, DateTime FechaOtorgado, bool PuedeDescargar, IReadOnlyList<string> Permisos);
