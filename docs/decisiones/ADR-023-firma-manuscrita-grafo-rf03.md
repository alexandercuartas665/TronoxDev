# ADR-023: Firma manuscrita (grafo) en la cajita (RQ05 - RF03 3.3.3) - Fase B.1

- Estado: Aceptada
- Fecha: 2026-09-08
- Contexto: RQ05 (Firma), Fase B del plan de pendientes. Primer item tras cerrar el nucleo (ADR-017..022) y la Fase A (config RF01 + notificaciones RF11).

## Contexto

El legacy captura la firma manuscrita del usuario en un pad (`ctrlMiFirma.ascx`, `signature_pad`) y la
guarda en `FIR_FIRMA_GRAFO` (una por entidad/tenant/usuario, PNG base64). Al sellar, `FirmaCajitaHelper`
pinta ese grafo en la cajita de firma; si el usuario no tiene grafo, dibuja su nombre en cursiva. El RQ
3.3.3 dice que la cajita muestra "la imagen del grafo del firmante"; RQ01 nunca implemento el
`firma_imagen_path`, asi que el modulo de firma guarda su propia tabla.

En TRONOX la cajita (`PdfSharpSignatureStamper`) era una caja de TEXTO (titulo + nombre/cargo/dependencia
+ fecha + URL). No habia captura del grafo ni pintado en la cajita.

## Decision

Portar el grafo como un slice aditivo que NO cambia el contrato estable de firma (invariante 5): solo se
agregan metodos y una columna.

### Dominio / datos
- Nueva entidad `FirmaGrafo : TenantEntity` (tabla `firma_grafos`), una vigente por `(tenant_id,
  platform_user_id)` (indice unico). Guarda `imagen_base64` (PNG base64 puro, sin prefijo data URI),
  `content_type`, `activo`. El dueno es el **PlatformUserId** (mismo espacio de identidad que
  `Firma.FirmanteUserId`), para que el sellado lo resuelva por el id del firmante.
- Migracion `FirmaGrafo` (aditiva, solo CREATE TABLE). TENANT-SCOPED (filtro global de EF por ser
  `ITenantScoped`, invariante 1).

### Servicio (`IFirmaService`, se AGREGA)
- `GetMiGrafoAsync(actor)`: devuelve el grafo vigente como data URI (o null).
- `GuardarMiGrafoAsync(dataUri, actor)`: upsert del grafo del usuario. Valida que el base64 decodifique
  y que no exceda 1 MB (el pad genera PNG pequeno). Limpia el prefijo data URI.
- `SellarPdfEnSitioAsync`: antes de estampar, busca el grafo del firmante (`snap.UserId`) y lo pasa en
  la cajita. Reusa el mismo motor de sellado para firma directa/solicitada/OTP/circuito/lote (todos
  pasan por aqui), asi que el grafo aparece en TODAS las vias de firma sin tocar cada una.

### Sellado (`IPdfSignatureStamper` / `PdfSharpSignatureStamper`)
- `CajitaFirma` gana `GrafoBase64` (opcional).
- Con grafo: la caja crece a 120pt y pinta el grafo escalado/centrado en una franja superior + separador,
  con el texto debajo. Sin grafo: caja compacta de 74pt como antes (sin regresion).
- El PNG se materializa en un archivo temporal y se coloca con `XImage.FromFile` (decodifica via
  ImageSharp, cross-platform: verificado en Windows y valido en la imagen Linux de prod). Best-effort:
  si el base64 es invalido, se omite el grafo sin romper la cajita (calca el try/catch del legacy).

### UI
- Pagina "Mi Firma" (`/modulo/firmas-mifirma`), port de `ctrlMiFirma.ascx`: pad de captura, botones
  Limpiar/Guardar, previsualizacion de la firma vigente. Autoservicio: se gatea con el permiso de
  `firmas-mis` (todo usuario con acceso a firma registra la suya) y se entra por un boton "Mi Firma" en
  la bandeja Mis Firmas.
- El pad es canvas + pointer events puros (`wwwroot/js/firma-grafo.js`), SIN libreria externa ni CDN
  (el resto del app no usa CDNs). Resolucion interna fija 1200x400 para no depender de `offsetWidth`,
  que en el primer render de Blazor todavia puede ser 0.

## Alternativas descartadas

- **Usar `signature_pad` por CDN (como el legacy):** el app no carga nada de CDN y la imagen de prod no
  garantiza salida a internet en runtime. Un canvas propio de ~90 lineas cubre el caso.
- **Nodo de menu propio `firmas-mifirma`:** se deja en el catalogo, pero el menu de un tenant ya
  existente no se re-siembra al agregar nodos (la huella de reconciliacion es de un arbol anterior), asi
  que el acceso real es el boton en Mis Firmas + la ruta gateada por `firmas-mis`.

## Consecuencias

- El grafo aparece en la cajita de cualquier via de firma. Sin grafo, la cajita es la de texto de antes.
- Diferido (Fase C): el grafo tambien deberia ir en la hoja "Certificacion de firmas" (RF03 3.3.1) junto
  al QR/token cuando se porte esa hoja; hoy la cajita ya lo muestra.
- No se toca el contrato estable de firma (invariante 5): todo es agregado.
