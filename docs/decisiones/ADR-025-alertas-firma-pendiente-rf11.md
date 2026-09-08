# ADR-025: Alertas de firma pendiente (RQ05 - RF11 Inc.2) - Fase B.4

- Estado: Aceptada
- Fecha: 2026-09-08
- Contexto: RQ05 (Firma), ultimo item de la Fase B. Cierra RF11 (notificaciones): el Inc.1 (avisos por
  evento) se hizo en Fase A.2; este es el Inc.2 (recordatorios de firmas que llevan mucho pendientes).

## Contexto

Legacy: `FirmaAlertaRepository` lee firmas pendientes de dos fuentes (FIR_FIRMAS directa y
FIR_CIRCUITO_FIRMANTES activo), marca lo alertado; el calculo de dias habiles y el disparo viven en un
job (`fir_alertas.ashx`). `FirmaConfig.firma_dias` = umbral de dias para alertar; `firma_frecuencia_dias`
= cada cuantos dias re-alertar.

Estado en TRONOX al abordarlo:
- **Calendario habil YA existe** (validado): `ICalendarioHabilService` (EsHabil / ProximoHabil /
  SumarDiasHabiles), config (dias de la semana + jornada) y festivos de Colombia, configurado en Datos
  de la Entidad (panel `CalendarioHabilPanel`) + pagina `/modulo/calendario-habil`. Ya lo usa el SLA de
  radicacion. No habia que construirlo.
- `CrearCircuitoAsync` crea una fila `Firma` (Estado=Pendiente) por cada firmante ACTIVADO del circuito.
  Por tanto **todas** las firmas pendientes (directa/solicitada/circuito) viven en `firmas` con
  Estado=Pendiente: una sola fuente para el escaneo, sin doble alerta.
- **`Tronox.Workers` NO se despliega a prod** (el compose de prod solo tiene postgres, azurite y app).

## Decision

### Fuente y calculo
- Escanear `firmas` con Estado=Pendiente (cubre todas las vias). Vencimiento con **dias HABILES**:
  `umbral = ICalendarioHabilService.SumarDiasHabilesAsync(fechaSolicitud, FirmaConfig.FirmaDias)`; se
  alerta cuando `hoy >= umbral`. Re-alerta respetando la frecuencia: solo si
  `hoy >= SumarDiasHabiles(UltimaAlertaAt, FirmaFrecuenciaDias)`.
- Nueva columna `firmas.ultima_alerta_at` (nullable) para marcar lo alertado (migracion `FirmaUltimaAlerta`,
  aditiva). Calca el "marca lo alertado" del legacy.
- Emision al firmante: campana in-app (`INotificationService`) + correo (`IEmailSender`), best-effort,
  mismo patron que `NotificarFirmaAsync` (RF11 Inc.1).

### Servicio y hospedaje
- `IFirmaAlertaService.EscanearYAlertarAsync` (Application/Firmas): tenant-scoped, lee FirmaConfig (si
  ModuloFirmaActivo y FirmaDias>0), escanea, emite y marca. Devuelve cuantas alerto.
- **Hospedaje en la app (Tronox.Web), NO en Workers** (Workers no se despliega): `FirmaAlertasHostedService`
  (`BackgroundService`), retraso inicial 3 min + `PeriodicTimer` cada 12 h. Cada ciclo descubre los
  tenants con firmas pendientes (`IgnoreQueryFilters`), y por cada uno fija el tenant ambient
  (`AmbientTenantContext.Begin`) en un scope propio y ejecuta el servicio. Best-effort por tenant.

## Alternativas descartadas

- **Construir un calendario habil propio para firma:** ya existe uno de entidad (RQ01), reutilizado.
- **Alojar en Tronox.Workers:** no se despliega a prod; el BackgroundService en la app garantiza que
  corra sin nueva infraestructura (no toca el invariante de "29 contenedores").
- **Escanear tambien FirmaCircuitoFirmante:** innecesario, los circuitos ya crean filas `firmas`.
- **Dias calendario como fallback:** no hace falta, el calendario habil esta disponible.

## Consecuencias

- Migracion aditiva (44 -> 45): `firmas.ultima_alerta_at`.
- En el primer ciclo tras el deploy se alertaran las firmas que ya llevan tiempo pendientes (una vez
  cada una; luego respetan la frecuencia). Comportamiento esperado de RF11 Inc.2.
- Multi-instancia: hoy prod es una sola instancia de la app; el dedup por `ultima_alerta_at` acota el
  riesgo de duplicados si algun dia se escala.
- Verificado e2e local: firma pendiente vencida -> el worker emite la campana "Firma pendiente" (+ correo
  best-effort) y marca `ultima_alerta_at`; log "Alertas de firma emitidas (tenant N): 1".
