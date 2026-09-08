# ADR-024: Rotulacion de expedientes (RQ03 - RF17) - Fase B.2

- Estado: Aceptada
- Fecha: 2026-09-08
- Contexto: RQ03 (Expedientes), Fase B del plan de pendientes RQ04/RQ05. Segundo item de la fase.

## Contexto

El legacy NO implementa la rotulacion como un `ctrlRotuloModal.ascx` (esa ruta no existe; tampoco hay
"Mantis #6516" en el codigo). Esta como un modal inline en `exp_bandeja.aspx` (Panel `pnlModalRotulos`),
lanzado desde la barra de seleccion masiva (boton "Rotulos", visible solo con permiso de imprimir), que
llama a `RotuloExportador.vb`:

- **PdfSharp** para el PDF (A4) + **BarcodeLib** Code 128 (`IncludeLabel=True`) del codigo del
  expediente. **No hay QR.**
- 3 tamanos: Caja (21x10 cm), Carpeta (18x6 cm), Sticker (10x5 cm); N por hoja (1/2/4) y posicion de
  inicio (para aprovechar hojas de etiquetas parciales, dibujando huecos vacios).
- Cada rotulo: encabezado azul "ARCHIVO DE GESTION" + 5 secciones (fondo/seccion/serie/subserie;
  expediente[codigo]+nombre; fechas/folios; caja/carpeta o ubicacion; codigo de barras).
- Datos en runtime (no persiste): expediente (fondo/dependencia/serie/subserie/codigo/nombre/fechas),
  folios (SUM de folios de los documentos vigentes) y caja/carpeta/ubicacion de la topografia fisica.
  Tope de 50 rotulos por generacion.

## Decision

Portar RF17 como un slice sobre la infraestructura TRONOX existente, sin persistir nada nuevo.

### Backend (`IExpedienteService`, se AGREGA)
- `GenerarRotulosAsync(ids, tamano, porHoja, posicionInicio, actor)`: **fail-closed por clasificacion**
  (RF10, reusa `ResolveNivelMaxOrdenAsync`: solo rotula lo que el usuario puede ver) + tope de 50.
  Arma por expediente: Fondo, Seccion (dependencia), Serie y Subserie (resueltas del arbol
  `SerieDocumental` por `ParentId`: el nodo asignado con padre es la subserie y su padre la serie),
  Codigo, Nombre, Fechas, Folios (`SUM(Documento.Folios)` de documentos vigentes) y Ubicacion (ultima
  `ExpedienteUbicacion` -> `TopografiaElemento` "Sigla - Nombre"). Delega el dibujo a `IRotuloExportador`.
- DTOs nuevos: `RotuloTamano` (Caja/Carpeta/Sticker) y `RotuloDatoDto`.

### Exportador (`IRotuloExportador` / `QuestPdfRotuloExportador`)
- **QuestPDF** (ya es la herramienta PDF de la casa, licencia Community; misma que el comprobante de
  pago) en vez de PdfSharp, por consistencia. Codigo de barras **Code 128** con **ZXing.Net** (patron de
  modulos) rasterizado a PNG con **SkiaSharp** (la misma version que arrastra QuestPDF); el codigo va
  como texto debajo (equivale al IncludeLabel del legacy). Sin QR (fiel al legacy).
- Layout calcado: encabezado azul "ARCHIVO DE GESTION", 5 secciones, huecos iniciales tenues para la
  posicion de inicio, footer con el tamano como referencia de corte.

### UI (`Expedientes.razor`)
- El boton "Rotulos" de la barra de seleccion masiva (antes un placeholder Toast) abre un modal con las
  3 tarjetas de tamano + "rotulos por hoja" (1/2/4) + "posicion de inicio" (recalculada segun por hoja),
  calcando el modal del legacy. "Generar PDF" llama al servicio y descarga via `window.tronoxDownload`
  (mismo patron que Documentos/ExpedienteDetalle).

## Alternativas / simplificaciones

- **PdfSharp + BarcodeLib (legacy):** se usa QuestPDF + ZXing.Net por consistencia con el resto del
  repo y para evitar el resolver de fuentes GDI del legacy.
- **N por hoja vs tamano fisico:** el legacy define tamanos fisicos (cm) Y por-hoja, que en A4 se
  contradicen (una Caja de 21 cm no cabe 2 por fila). Aca "N por hoja" gobierna la rejilla (1 y 2 -> 1
  columna; 4 -> 2x2) llenando la pagina, y el tamano se rotula como referencia de corte en el footer.
- **Caja/Carpeta desde topografia:** el legacy resuelve caja/carpeta con una CTE recursiva fragil (por
  `LIKE '%CAJA%'`) y de hecho suele caer a "UBICACION". Aca se muestra la Ubicacion actual (Sigla -
  Nombre) del ultimo `ExpedienteUbicacion`. El desglose caja/carpeta queda diferido.

## Consecuencias

- Sin migracion ni tablas: el rotulo se arma en runtime. Nuevas dependencias: `ZXing.Net` y `SkiaSharp`
  (esta ultima ya venia transitiva por QuestPDF).
- Fiel al alcance del legacy (sin QR, Code 128, 3 tamanos, huecos de posicion). Diferido: desglose
  caja/carpeta de la topografia, y gate por permiso de imprimir especifico (hoy la accion la ve quien
  ve la bandeja; la generacion ya es fail-closed por clasificacion).
