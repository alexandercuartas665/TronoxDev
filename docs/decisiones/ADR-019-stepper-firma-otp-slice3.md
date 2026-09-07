# ADR-019: Stepper de firma con OTP (RQ05 - RF08) - slice 3

- Estado: Aceptada
- Fecha: 2026-09-07
- Contexto: RQ05 (Firma). Continua el slice 1 (ADR-017, firma directa) y el slice 2 (ADR-018, bandeja).

## Contexto

El control legacy `ctrlFirmaStepper.ascx` (+`.vb`, 1248 lineas) es la Experiencia del Firmante
(RF08): un stepper de 5 pasos que cumple una solicitud de firma pendiente con verificacion OTP.
Pasos: Lectura (visor con gating de lectura) -> Identidad -> Consentimiento (Decreto 2364 de 2012)
-> Verificacion OTP -> Resultado (certificado + PDF firmado), con sub-paso Rechazo. El OTP
(`FirmaOtpHelper`) genera un codigo de 6 digitos, guarda SOLO su hash SHA-256 (nunca el codigo),
con vigencia acotada, invalida los previos y lo valida contando intentos.

El slice 2 dejo la bandeja "Mis Firmas" con una accion "Firmar" que cumplia la solicitud con un
consentimiento simple (sin OTP). Este slice reemplaza esa accion por el stepper completo.

## Decision

Portar el stepper de 5 pasos y el OTP como la accion "Firmar" de la bandeja.

### Datos
- Entidad `FirmaOtp` (tabla `firma_otps`), calca FIR_OTP: FirmaId, Usuario (PlatformUserId),
  CodigoHash (SHA-256 hex, nunca el codigo en claro), Canal, ExpiraAt, Intentos, VerificadoAt.
  TENANT-SCOPED. Cascade con la firma. Vigencia 5 min (configurable a futuro).

### Backend (`IFirmaService`)
- `GenerarOtpAsync(firmaId)` - invalida los OTP vigentes previos de la firma, genera un codigo de 6
  digitos con RNG criptografico, guarda el hash, y lo envia por correo (best-effort). Devuelve el
  correo enmascarado + la vigencia; y `CodigoDemo` SOLO si el envio fallo (SMTP no configurado),
  para poder completar la firma en entornos sin correo. En produccion con correo, `CodigoDemo=null`.
- `FirmarConOtpAsync(firmaId, codigo)` - valida el OTP (ultimo vigente, no verificado, no expirado,
  hash coincidente; incrementa intentos; sella VerificadoAt) y, si es correcto, sella el PDF y marca
  Firmado (reutiliza el mismo motor de sellado que la firma directa - CumplirFirmaAsync).
- `FirmarSolicitadaAsync` (sin OTP) se conserva para solicitudes con OtpRequerido=false.

### UI
- Componente `FirmaStepperModal` (5 pasos, diseno `fst-*` calcado). Paso 1 abre el visor documental
  (gating por apertura); paso 4 (OTP) solo si la solicitud exige OTP. Sub-paso Rechazo con motivo.
  El valor del codigo se lee del campo por JS al enviar (robusto ante el round-trip del binding).
- La accion "Firmar" de la bandeja abre el stepper (reemplaza el consentimiento simple del slice 2).

## Alcance diferido
- Gating de lectura por PAGINAS (el legacy exige ver todas las paginas via callback del visor); aqui
  el gate es la apertura del visor.
- Autofirma: ubicacion del recuadro de firma dibujada en el visor.
- Grafo de firma (imagen), QR y certificado en PDF descargable, PDF/A, sellado XMP, hora legal NTP.
- Circuitos multi-firmante (RF07), firma masiva (RF09), pista de auditoria.

## Consecuencias
- La firma de una solicitud pendiente pasa por la experiencia completa (lectura, identidad,
  consentimiento legal, OTP) antes de sellar; queda trazada (intentos OTP, hash, timestamp).
- El OTP nunca se persiste en claro (solo hash SHA-256). Un solo uso, vigencia acotada, fail-closed.
- Contrato `IFirmaService` ampliado sin romper firmas existentes (invariante 5).
- Verificado e2e con el codigo real (revelado en modo sin-correo): la firma con OTP correcto sella y
  marca Firmado; un codigo incorrecto se rechaza (intentos++), la solicitud sigue Pendiente.
- Nota de prueba: el DbContext de Blazor vive por circuito, por lo que alterar el hash del OTP por
  fuera (SQL) durante una sesion abierta no lo ve el servicio (entidad ya trackeada); esto es un
  artefacto de testing, no afecta el flujo real (el codigo llega por correo/pantalla).
