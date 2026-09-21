# ADR-031: Ola 4 - Estampa de firma en imagenes y deuda de visor (marca de agua, full-text) (RQ04/RQ05)

- Estado: Aceptada
- Fecha: 2026-09-21
- Contexto: "Ola 4" del plan por olas. Tres frentes previstos: (1) Firma Digital Certificada (ECD),
  (2) estampa de la cajita de firma en documentos IMAGEN, (3) deuda de visor (marca de agua de seguridad
  y busqueda full-text en "Compartidos conmigo").

## Decisiones

### 1. Firma Digital Certificada (ECD) - DIFERIDA (sin codigo)
- El estudio del legacy (D:\Desarrollo\core) confirma que la ECD (RF14/RF19) es un **placeholder**: en
  `FIR_FIRMAS` existen 4 columnas ECD_* (ECD_PROVEEDOR, ECD_ID_TRANSACCION, ECD_URL_VALIDADOR,
  ECD_TIMESTAMP_TSA) que **nunca se escriben**; no hay clases de proveedor/vigencia, ni tablas, ni
  paginas, ni integracion con ninguna AC/HSM/TSA. Toda firma real del legacy es Electronica
  (SHA-256 + sellado), y "Digital Certificada" es solo una etiqueta.
- No existe integracion que portar fielmente. Una ECD real exige **definir el proveedor con el cliente**
  (Certicamara / Andes SCD / GSE, o API de firma en la nube) y sus credenciales. Se difiere a la
  "Seccion B / RQ08-B", igual que en el legacy. No se agrega scaffolding vacio (columnas sin consumidor).

### 2. Estampa de la cajita de firma en IMAGENES (RQ05 - RF03-B)
- `IImageSignatureStamper` / `SkiaImageSignatureStamper` (SkiaSharp): cuando el documento firmado no es
  PDF sino imagen (jpg/jpeg/png/tif/tiff/bmp/gif), dibuja la cajita de firma en una **banda al pie** de
  la imagen (titulo/nombre/fecha/URL de verificacion + grafo manuscrito a la derecha si el firmante lo
  registro), y devuelve la imagen en su mismo formato (jpg/png). No aplica PDF/A ni XMP (eso es solo PDF):
  la integridad la da el SHA-256 del resultado.
- `FirmaService.SellarPdfEnSitioAsync` ramifica por formato: imagen -> estampa Skia + SHA-256; PDF ->
  camino PDF/A + XMP existente. Los dos gates que antes rechazaban todo lo que no fuera PDF
  (`FirmarDirectoAsync` y `ValidarFirmablePdf`) ahora aceptan tambien imagenes. Best-effort: si el
  estampado falla, se conserva el binario original (calca el try/catch del legacy).

### 3. Deuda de visor
- **Marca de agua de seguridad (RQ04 - RF04):** `ISecurityWatermarker` / `SecurityWatermarker` hornea en
  el binario que se **visualiza** (no en la descarga) una marca de agua diagonal a -45 grados repetida en
  mosaico, gris azulado semitransparente, con `Usuario: {nombre} - Fecha: {dd/MM/yyyy HH:mm:ss} - IP: {ip}`,
  cuando el documento es Reservado o Clasificado. Calca `MarcarPdfSeguridad`/`EstamparImpresionImagen` de
  `doc_visor.ashx` del legacy. PDF con PdfSharpCore (XGraphics), imagen con SkiaSharp. Nuevo metodo de
  servicio `DocumentoService.GetVisorBinarioAsync(id, actor, ip)`; el endpoint `/visor/bin` lo usa para la
  vista inline y deja `DescargarAsync` (limpio) para `dl=1` (el control de descarga es el permiso
  PuedeDescargar, como en el legacy con `esDescarga`).
- **Full-text en "Compartidos conmigo" (RQ04):** `ListarCompartidosConmigoAsync` filtraba solo por nombre
  en memoria. Ahora el filtro se mueve a la query EF y busca tambien por `NombreArchivoOriginal`,
  tipologia, `OcrTexto` y `ContenidoHtml`, calcando el bloque full-text de las otras bandejas
  (`ListarBorradoresAsync`, `ListarArchivadosPorMiAsync`, `BuscarAvanzadoAsync`).

## Consecuencias

- Sin migracion (prod permanece en migraciones 46). No hay paquetes nuevos: SkiaSharp y PdfSharpCore ya
  estaban en Infrastructure.
- Verificado: build de solucion verde; suite completa (578 tests, incluye 17 nuevos: 13 del estampador de
  imagenes + 4 del watermarker de seguridad). E2e en dev: doc Reservado en el visor muestra el mosaico
  diagonal Usuario/Fecha/IP (inline 37 KB vs descarga limpia 15 KB, ambos PDF validos); busqueda por una
  palabra que solo esta en el contenido ("zafiro_tronox") filtra la lista de Compartidos al documento
  correcto.
- Diferido: ECD (requiere proveedor + credenciales del cliente); estampa de multiples firmantes en una
  misma imagen (el circuito re-estampa una sola banda; para imagen se asume firmante unico, como el legacy).

## Nota de entorno

- El object storage local (Azurite, `tronox-azurite`) y `tronox-postgres` habian quedado detenidos por un
  evento del sistema; se rearrancaron para la verificacion. Los blobs de documentos previos de dev se
  perdieron en esa caida (no afecta prod ni el codigo); la verificacion se hizo con un documento nuevo.
