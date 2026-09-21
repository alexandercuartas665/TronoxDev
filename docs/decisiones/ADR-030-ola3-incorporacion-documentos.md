# ADR-030: Ola 3 - Incorporacion de documentos (digitalizar, fuente externa, auto-OCR) (RQ04)

- Estado: Aceptada
- Fecha: 2026-09-21
- Contexto: RQ04 (Documentos), "Ola 3" del plan por olas: nuevas vias de incorporacion. Tres piezas, todas
  sobre el flujo de binario existente (`DocSvc.CrearBorradorBinarioAsync`, que marca OcrEstado=Pendiente).

## Decisiones

### 1. Digitalizar por camara (RF18)
- `DigitalizarModal` + `js/camara.js` (getUserMedia, `facingMode: environment` para la camara trasera del
  movil). Captura fotos, las muestra como miniaturas y al guardar sube cada JPG como Borrador
  (CrearBorradorBinarioAsync). El escaner TWAIN local queda fuera del alcance web (agente nativo). Sin
  camara disponible, el modal muestra un fallback claro.

### 2. Fuente externa - SFTP (RF20/RF21)
- `IFuenteExternaService`/`SftpFuenteExternaService` (SSH.NET): conecta por host/usuario/clave (de un
  solo uso, NO se persiste), lista una ruta y descarga archivos (tope 100 MB, timeout 20 s). Resultado
  tipado, best-effort. `FuenteExternaModal`: pestana SFTP funcional; OneDrive/Google Drive/SharePoint
  quedan como "proximamente" (requieren registro OAuth de la entidad). Los importados entran como
  Borrador por el mismo flujo de binario.

### 3. OCR automatico al incorporar (RF04)
- `OcrAutoHostedService` (BackgroundService en la app; el host de Workers no se despliega a prod):
  retraso 2 min + cada 5 min, recorre los tenants con documentos OcrEstado=Pendiente, y para los que
  tienen OcrConfig activa ejecuta el OCR real (Azure Computer Vision via OcrService.ReprocesarAsync) de
  un lote de 10 por tenant. El OcrService marca Completado/Error, asi que cada pendiente se procesa una
  sola vez (sin bucles). Reemplaza el "Reprocesar" manual como via por defecto (queda como respaldo).

## Alternativas descartadas

- **OAuth cloud (OneDrive/GDrive/SharePoint) ahora:** requiere registro de app OAuth y consentimiento del
  usuario; sin credenciales ni forma de probar en el entorno. Se difiere; SFTP es la primera fuente real.
- **OCR inline al subir:** la llamada a Azure (~segundos) bloquearia la respuesta de la subida; el worker
  asincrono lo desacopla y acota el consumo por lote.
- **FTP plano:** inseguro; se implementa SFTP (SSH.NET). FTP simple se puede anadir despues si un cliente
  lo exige.

## Consecuencias

- Sin migracion. Paquete nuevo: SSH.NET (2026.0.0). La imagen ya trae LibreOffice de olas previas.
- Verificado e2e: SFTP contra test.rebex.net (listar + descargar readme.txt), modal Digitalizar (abre y
  cae al fallback sin camara en el entorno headless), auto-OCR (Pendiente 7->5 con Procesando/Error).
- Diferido: proveedores cloud OAuth (RF20/21), escaner TWAIN local (agente nativo).
