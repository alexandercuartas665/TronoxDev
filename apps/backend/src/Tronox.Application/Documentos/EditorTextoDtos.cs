namespace Tronox.Application.Documentos;

/// <summary>
/// Contenido para reabrir un borrador en el editor de texto interno (RF08), calca AbrirBorrador del
/// legacy (nombre + CONTENIDO_HTML). Solo aplica a borradores propios con cuerpo de editor.
/// </summary>
public sealed record EditorContenidoDto(long DocId, string Nombre, string? ContenidoHtml);

/// <summary>
/// Peticion de guardado del editor de texto (RF08). <c>DocId</c> null/0 = crear un borrador nuevo;
/// &gt; 0 = actualizar el borrador existente. <c>Html</c> es el cuerpo crudo de TinyMCE (sin sanitizar aun).
/// El nombre vacio se sustituye por "Documento_sin_nombre" (calca el legacy).
/// </summary>
public sealed record GuardarContenidoRequest(long? DocId, string Nombre, string Html);
