# ADR-014: Portal ciudadano publico - resolucion de tenant por slug server-side

- Estado: Aceptada
- Fecha: 2026-08-06
- Contexto: RQ09 RF03 - port del portal ciudadano de radicacion (rad_portal) desde el legacy VB.NET.

## Contexto

El portal ciudadano es una superficie PUBLICA (sin login): un ciudadano radica una PQRSD o consulta el
estado de su radicado. El legacy resuelve la entidad (sucursal = tenant) por un parametro de query
`?e=<sucursal>` en la URL, **manipulable**: editando la URL se puede radicar o consultar contra cualquier
sucursal. Ademas la unica "seguridad" era la existencia de una sesion, inexistente en el portal.

En TRONOX el aislamiento por tenant (invariante 1 / DAT-01) se aplica por el filtro global de EF sobre
`ITenantScoped`, que depende de `ITenantContext.TenantId`. En una request publica no hay usuario ni
claims, luego no hay tenant en el contexto.

## Decision

1. **El tenant se resuelve server-side por un SLUG** propio del portal (`RadPortalConfig.Slug`, unico
   global), no por un id/sucursal manipulable en la query. La URL publica es `/portal/{slug}`.
2. La resolucion del slug -> tenant se hace con `IgnoreQueryFilters()` (unica consulta que puede correr
   sin tenant en contexto), devolviendo el `tenant_id`.
3. Con el tenant resuelto, cada operacion del portal (leer branding, radicar, consultar) corre dentro de
   `AmbientTenantContext.Begin(tenantId)`: un scope `AsyncLocal` que fija el tenant para el filtro global
   de EF durante esa cadena async. Asi el resto del codigo (incluido `RadicadorService`) opera aislado en
   el tenant correcto SIN un usuario autenticado.
4. El radicar reutiliza `RadicadorService` (consecutivo + vencimiento) con `canal = Web` y agrega un
   `PortalToken` de seguimiento. La consulta expone SOLO datos publicos (numero, estado, tipo, dependencia,
   semaforo de vencimiento, timeline publico, respuesta publica); nunca funcionario ni notas internas, y
   los radicados anonimos no son consultables (no hay documento con el que casar).

## Refuerzos de seguridad (superficie publica, CLAUDE.md seccion 5)

Alcance elegido con el usuario: "funcional + refuerzos base".
- **reCAPTCHA v3:** toggle `ExigirCaptcha` en la config; hoy un check de cliente, con las llaves reales
  DIFERIDAS (integracion posterior). NO se replica el captcha casero con System.Drawing del legacy.
- **Rate limiting:** a reforzar con Redis en radicar/consultar (el legacy usaba un diccionario en memoria).
- **OTP:** el legacy no usa OTP (la consulta es numero + documento + captcha); se mantiene ese modelo.
- **Adjuntos:** a object storage (invariante 9), nunca BLOB.
- **Consulta:** numero + documento del solicitante como secreto compartido; anonimos sin seguimiento.

## Consecuencias

- No es posible saltar de tenant manipulando la URL: el slug es el unico punto de entrada y mapea a un
  unico tenant server-side.
- El portal funciona sin usuario autenticado respetando el aislamiento por tenant.
- Queda pendiente (refuerzo): reCAPTCHA v3 con llaves reales, rate limiting en Redis, y la subida de
  adjuntos (requiere el flujo de upload a object storage).

## Alternativas descartadas

- **Tenant por query `?e=` (legacy):** manipulable; viola el aislamiento. Descartado.
- **Subdominio por tenant:** los tenants de TRONOX no tienen subdominio hoy; el slug en la ruta es
  equivalente y no requiere infraestructura DNS por tenant. Se puede migrar a subdominio despues sin
  cambiar el dominio.
