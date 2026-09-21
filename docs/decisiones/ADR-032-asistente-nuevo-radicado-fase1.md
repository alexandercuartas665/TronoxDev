# ADR-032: Asistente "Nuevo Radicado" (rad_radicar) - Fase 1 (RQ09 RF02)

- Estado: Aceptada
- Fecha: 2026-09-21
- Contexto: El boton "+ Nuevo Radicado" de la bandeja de radicacion (/modulo/radicacion) era un
  placeholder (`NuevoRadicado()` solo mostraba un flash "se habilita al portar ese modulo"). Se porta el
  asistente `rad_radicar` del legacy VB.NET (ASP.NET WebForms, `rad_radicar.aspx` + `rad_op.ashx.vb`),
  que es un wizard de 5 pasos para radicar Entrada/Salida/Interna.

## Hallazgo habilitador

El backend de radicar YA existia y estaba completo: `IRadicadorService.RadicarAsync` (consecutivo
transaccional por tenant/tipo/anio via ISequenceService, vencimiento SLA con calendario habil, trazas)
ya lo consumian "radicar desde correo" y el portal ciudadano. El port es, por tanto, principalmente UI
cableada a servicios existentes, no reconstruccion de logica.

## Decisiones (Fase 1)

### 1. Alcance: Entrada + Interna (Salida diferida)
El asistente cubre los dos sentidos mas comunes. La Salida ("Responder" / `rad_salida`: canal de envio,
vinculo a la entrada, respuesta definitiva, envio por email) queda para una fase posterior, como el otro
placeholder `Responder`. El "circular" de Interna (multiples destinatarios) tambien se difiere; Fase 1
usa destino unico.

### 2. Wizard de 4 pasos (calca el legacy sin el paso de documentos electronicos)
Pasos: (1) Clasificacion (comunicacion Entrada/Interna, tipo de comunicacion filtrado por direccion con
badges PQRSD/Tutela, nivel de reserva autollenado del tipo, canal); (2) Remitente inline para Entrada
(tipo de tercero con "Anonimo" gateado por `PermiteAnonimo` del tipo, tipo/nro doc, nombre, email, tel) u
Origen (dependencia + funcionario) para Interna; (3) Documento (soporte, asunto, descripcion, folios,
anexos); (4) Destino (dependencia + funcionario + prioridad) -> Radicar. Los pasos de documentos
electronicos (3.5, estampa arrastrable) y digitalizacion (5) van en Fase 2.

### 3. Radicar + distribuir en un solo paso (calca el legacy)
Al radicar, si se eligio dependencia destino se encadena `IRadicacionDistribucionService.DistribuirAsync`
tras `RadicarAsync`, replicando el "radicar y distribuir" del legacy en una sola accion, reutilizando los
dos servicios existentes. Si no se elige destino, el radicado queda "Radicado / Sin distribuir" y el
operador lo distribuye luego desde la bandeja (modelo de TRONOX). Cero cambios de flujo en el backend.

### 4. Remitente inline (RQ07 Terceros pendiente)
El remitente se captura inline en el radicado (como ya hace el legacy y el modelo de TRONOX) sin selector
ni autocompletado de directorio: RQ07 Terceros aun no existe (solo enums). Cuando exista se agregara
`RemitenteTerceroId` (invariante 2 / DAT-02). Nota de la propia entidad Radicado.

### 5. Cambio aditivo minimo en el DTO (sin migracion)
`RadicarNuevoRequest` se extendio con parametros OPCIONALES al final (`Folios`, `NumAnexos`,
`DependenciaOrigenId`, `FuncionarioOrigenId`) para persistir metadatos basicos y el origen de las
internas; las columnas ya existen en `radicados`. Se agrego `TiposAsistenteAsync` (tipos con banderas:
direccion, PQRSD/Tutela, permite anonimo, nivel default) y `NivelesReservaAsync` a
`IRadicacionBandejaService` para alimentar el paso 1. Los llamadores existentes no se ven afectados.

## Fuera de alcance / diferido (dependen de modulos no construidos)
- Salida / "Responder" (rad_salida): canal de envio, vinculo a entrada, envio email.
- Documentos electronicos (paso 3.5): subida a object storage, folios automaticos, estampa arrastrable,
  visor. Digitalizacion (paso 5). -> Fase 2.
- Autocompletar remitente desde directorio de terceros (RQ07 Terceros).
- Deteccion de duplicados del asunto, acuse/sticker imprimible, municipios DIVIPOLA reales, escaner TWAIN
  y antivirus (Windows Defender, no aplica en contenedor Linux). El legacy los trae; se difieren.

## Consecuencias

- Sin migracion. Sin paquetes nuevos. Solo UI (Radicacion.razor) + un metodo de catalogo y una extension
  aditiva del DTO en Application.
- Verificado: build de solucion verde; 581 tests (incl. aislamiento cross-tenant). E2e en dev: el
  asistente creo el radicado ALCPRU-E-2026-000002 (Entrada, Derecho de Peticion/PQRSD, remitente inline,
  folios 3), lo distribuyo a Gestion Documental en un paso (estado Distribuido), y la bandeja + contadores
  se refrescaron (Todos 37->38, PQRSD 21->22).

## Actualizacion - Fase 2 (documentos electronicos / adjuntos)

Cuando el soporte es Electronico, el paso 3 del asistente permite subir documentos (InputFile, calcando
CargaArchivosModal): PDF/DOCX/XLSX/imagenes/XML, max 50 MB c/u, con lista y quitar. Al radicar:

- `IRadicadorService.RadicarConArchivosAsync(request, archivos)` (nuevo) sube cada archivo a **object
  storage** (invariante 9, key opaca tenant-scoped `{tenant}/{guid}.{ext}`), calcula contentType y SHA-256
  (reusa `DocumentoRules`) y **folios automaticos** (PDF: conteo de objetos /Type /Page, calca
  `ContarPaginasPdf` de Documentos; otros = 1), y cuelga los `RadicadoArchivo` del radicado via el radicar
  base. La UI solo pasa los bytes; la logica de storage/tenant queda en Application. `RadicadorService`
  ahora inyecta `IObjectStorage`.
- Diferido a fase posterior: estampa arrastrable del sticker sobre el PDF, visor en el asistente,
  digitalizacion post-radicado (TWAIN), y el flujo de Salida ("Responder"/rad_salida).
- Verificado e2e (Chrome + file_upload): ALCPRU-E-2026-000003 con adjunto radicado_prueba.pdf subido a
  object storage (storage_key + sha256 de 64 hex) y **folios=2 auto-calculados** del PDF de 2 paginas.
  582 tests verdes.
