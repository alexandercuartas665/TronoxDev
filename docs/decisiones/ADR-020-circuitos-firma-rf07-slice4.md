# ADR-020: Circuitos de firma multi-firmante (RQ05 - RF07) - slice 4

- Estado: Aceptada
- Fecha: 2026-09-07
- Contexto: RQ05 (Firma). Continua slice 1 (ADR-017), slice 2 (ADR-018), slice 3 (ADR-019).

## Contexto

`CircuitoRepository.vb` (RF07) agrupa varios firmantes sobre el mismo documento en modo Secuencial
(uno tras otro por orden) o Paralelo (todos a la vez). Tablas FIR_CIRCUITOS (cabecera) +
FIR_CIRCUITO_FIRMANTES (detalle ordenado). Al firmar cada uno avanza el circuito; el sellado con
todas las cajitas ocurre al completar. Un rechazo cancela TODO el circuito (§3.7.3).

## Decision

Portar los circuitos reutilizando la entidad `Firma` (slice 1-3) y el stepper (slice 3).

### Datos
- `FirmaCircuito` (tabla `firma_circuitos`): documento, modo (Secuencial/Paralelo), estado
  (Activo/Completado/Cancelado), total/completados, tipo mixto, otp, solicitante, motivo cancelacion.
- `FirmaCircuitoFirmante` (tabla `firma_circuito_firmantes`): orden, firmante (PlatformUserId),
  nombre/cargo, tipo, estado (EnEspera/Pendiente/Firmado/Rechazado), FirmaId, timestamp.
- `Firma.CircuitoId` (nullable): enlaza la solicitud individual con su circuito.

### Backend (`IFirmaService`)
- `CrearCircuitoAsync`: valida (creador, PDF con binario, no firmado, sin circuito activo, sin
  repetidos, sin auto-inclusion). Crea cabecera + firmantes; activa segun modo (Secuencial: orden 1;
  Paralelo: todos) creando la `Firma` Pendiente de cada activo. Documento -> EstadoFirma Pendiente.
- Avance en `CumplirFirmaAsync` (compartido con firma directa/solicitada/OTP): al firmar una `Firma`
  con `CircuitoId`, marca el firmante Firmado, incrementa completados; Secuencial activa al siguiente
  En_Espera (crea su `Firma`); el documento solo queda **Firmado** cuando el circuito se completa
  (mientras tanto sigue Pendiente). Cada firma sella su cajita apilada (indice = ya firmados).
- Rechazo en `RechazarSolicitudAsync`: si la firma es de un circuito, cancela TODO (circuito
  Cancelado + motivo, firmante Rechazado, resto de firmas Pendientes -> Rechazado, doc -> SinFirma).
- `ListarCircuitosEnviadosAsync`: circuitos del solicitante con su progreso (bandeja Enviadas).
- KPI "En progreso" = circuitos Activos del solicitante.

### UI
- `SolicitarCircuitoModal`: modo, lista ordenada de firmantes (agregar/quitar/reordenar), tipo por
  firmante, OTP, fecha limite, instrucciones. Cableado en "Circuito de firma" del menu del documento.
- Bandeja Enviadas: tarjetas de progreso `.mf-circ` (barra, estado por firmante, timestamps).

## Alcance diferido
- Cajita por coordenadas (autofirma) por firmante; el slice apila las cajitas por indice.
- Sellado unico al final (aqui es progresivo por firma, resultado equivalente: todas las cajitas).
- Recrear circuito desde uno cancelado; notificar a los que ya firmaron; tipo Digital certificada real.

## Consecuencias
- Firma multi-firmante secuencial y paralela sobre un documento, con progreso visible y cancelacion.
- El documento no queda Firmado hasta que el circuito completa (integridad del flujo).
- Verificado e2e (3 usuarios de prueba): secuencial [Rita->Carlos] completa -> doc Firmado; paralelo
  con rechazo de un firmante -> circuito Cancelado, firmas Rechazadas, doc SinFirma.
- Contrato `IFirmaService` ampliado sin romper firmas existentes (invariante 5).
