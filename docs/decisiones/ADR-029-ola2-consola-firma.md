# ADR-029: Ola 2 - Consola de firma (auditoria con filtros, metricas, plantillas, presets) (RQ05)

- Estado: Aceptada
- Fecha: 2026-09-21
- Contexto: RQ05 (Firma), "Ola 2" del plan por olas: visibilidad y productividad de firma. Cuatro piezas.

## Decisiones

### 1. Auditoria con filtros (RF12) - port de fir_auditoria
- `IFirmaService.ListarPistaFiltradaAsync(filtro, actor)`: sobre el ledger `super_admin_audit_logs`
  (acciones de firma), filtra por evento/usuario/documento/fechas con paginacion. `GetEventosAuditoria`
  da las opciones. Pagina `/modulo/firmas-auditoria` (gateada por `firmas-mis`), enlazada desde el boton
  "Pista de auditoria" de Mis Firmas (antes abria un modal de recientes). Solo lectura.

### 2. Consumo y metricas (RF20)
- `GetMetricasAsync(actor)`: KPIs (total/firmadas/pendientes/canceladas/circuitos) + SLA promedio (horas
  entre solicitud y firma) + volumen de firmas ejecutadas por mes (ultimos 6). Pagina
  `/modulo/firmas-metricas` (nodo de menu ya existente) con tarjetas + barras CSS (sin Chart.js).

### 3. Plantillas de firmante (TRON-20) - port de FirmaPlantillaRepository
- Entidad `FirmaPlantilla` (tabla `firma_plantillas`, migracion `FirmaPlantillas`): Nombre, Modo, Otp,
  TotalFirmantes, Resumen, `ConfigJson` ({ modo, otp, firmantes:[{uid,nombre,tipo}] }), baja logica
  (Activo). Service Guardar/Listar/Obtener/Eliminar. UI en `SolicitarCircuitoModal`: "Guardar como
  plantilla" (serializa el disenador) y "Usar plantilla" (precarga modo+otp+firmantes).

### 4. Presets de circuito desde tokens {{firma}} (RF13) - port de FirmaPlantillaParser/FirmaPresetRepository
- `FirmaPlantillaParser.Parse`: reconoce `{{firma}}`, `{{firma:Cargo}}`, `{{firma:correo}}`,
  `{{firma:Rol | orden:N | opcional}}` (mismo regex y semantica del legacy).
- `ResolverPresetAsync(docId, actor)`: lee el `ContenidoHtml` del documento, parsea los tokens y resuelve
  cada uno a un usuario del tenant **por correo** (exacto) o **por nombre** (DisplayName exacto);
  ambiguo/no encontrado -> generico (Resuelto=false). Al abrir el modal de circuito se precargan los
  firmantes resueltos, en orden.
- **Sin persistir** el preset (a diferencia de FIR_CIRCUITO_PRESET del legacy): se resuelve en vivo al
  abrir el modal (no hace falta una tabla; el circuito real se crea igual con CrearCircuitoAsync).

## Consecuencias

- Una migracion (`FirmaPlantillas`, 45 -> 46). Items 1, 2 y 4 sin migracion.
- Verificado e2e: metricas (KPIs + barras + SLA), auditoria con filtro por evento, preset precarga 2
  firmantes por correo, guardar/usar plantilla.
- Diferido: resolucion de tokens por cargo/grupo (TRONOX no tiene aun el vinculo usuario<->cargo/grupo
  del legacy SUCURSAL_GRUPOS; hoy resuelve por correo y nombre). Persistencia del preset si se requiere
  reabrir con el mismo estado sin re-parsear.
