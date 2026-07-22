---
type: ADR
status: accepted
date: 2026-07-22
---
# ADR-004: Enforcement fail-closed y anclaje del Super Administrador

## Estatus

Aceptado. Implementa el invariante 10 y **elimina una puerta trasera heredada del backbone**.

## Contexto

El backbone ECOREX resuelve los permisos con un concepto `Unrestricted`: un usuario Owner/Admin,
o un usuario **sin ningun rol**, o una resolucion que lanza excepcion, obtenian acceso sin
restriccion. Su justificacion literal era *"si la resolucion falla, no bloqueamos la consola"*.

Eso es defendible en una consola interna de administracion. **No lo es en un SGDEA** que maneja
niveles de clasificacion Reservado y Clasificado (Ley 1712/2014 Art. 18 y 19): ahi un fallo de
resolucion que concede acceso total es una fuga de informacion reservada, no una molestia.

El PLAN DE ARRANQUE §2.1 ya lo habia senalado como divergencia obligatoria.

## Decision

**1. Se elimina `Unrestricted` / `AllowAll` del modelo de permisos.** No queda ningun camino en el
codigo que conceda acceso por omision. En concreto:

| Situacion | Antes (ECOREX) | Ahora (TRONOX) |
|---|---|---|
| Usuario sin ningun rol vigente | Acceso total | **Sin permisos** |
| Resolucion de permisos que lanza | Acceso total | **Sin permisos** |
| Sin identidad en el contexto | Acceso total | **Sin permisos** |
| Rol vencido o inactivo | (no existia) | **No cuenta** |

**2. El acceso total del Super Administrador pasa a ser un DATO, no una excepcion de codigo.**

Al quitar el bypass aparece un problema de arranque: un tenant recien creado nace con usuarios
Owner/Admin que no tienen ningun rol asignado, luego quedarian sin permisos y el tenant nacería
inutilizable.

Se resuelve en el **aprovisionamiento del alta**: los usuarios Owner/Admin que no tengan ninguna
asignacion quedan anclados al rol `Super Administrador`, y ese rol nace con la matriz completa
derivada del menu.

La diferencia es sustantiva: quien puede verlo todo, y por que, queda **registrado en
`usuarios_roles` y en la matriz**, es auditable, y puede revocarse desde la UI. Antes era una rama
`if` invisible que ninguna pista de auditoria reflejaba.

**3. Los claims se leen del `AuthenticationState`, nunca del `IHttpContextAccessor`.** En un
circuito Blazor interactivo no hay `HttpContext`: los claims salian nulos, la resolucion caia en
fail-open y **el gateado en pagina no restringia a nadie** aunque su rol lo prohibiera. Se elimino
`CookieUserContext`, codigo muerto que aun leia del `HttpContext`.

## Consecuencias

- **Un error de configuracion ahora BLOQUEA en vez de abrir.** Es el comportamiento correcto, pero
  cambia la experiencia: si alguien queda sin permisos, la causa es que le falta rol o matriz, no
  que el sistema este roto. Los mensajes de la UI deben decirlo con claridad.
- **Efecto colateral detectado durante la implementacion:** `NavMenu` resolvia permisos en un scope
  nuevo sin fijar el tenant ambiental. Con fail-open pasaba inadvertido; con fail-closed habria
  dejado **el menu en blanco para todos los usuarios**. Corregido. Es un buen ejemplo de que el
  fail-open no solo es inseguro: tambien esconde defectos reales.
- **Deuda conocida:** `AmbientTenantContext` todavia cae al `HttpContext` cuando no hay scope
  ambiental. No es explotable —en un circuito da `TenantId = null`, el filtro global devuelve cero
  filas y el resultado es fail-closed— pero conviene eliminar esa via.
- Revocar el rol `Super Administrador` a TODOS los usuarios de un tenant lo deja sin quien lo
  administre. La UI de RF05 debe impedir quedarse sin ningun Super Administrador activo.

## Alternativa descartada

Mantener un bypass para un usuario "root" de plataforma. Se descarto porque reintroduce
exactamente el camino que este ADR elimina, y porque la impersonacion auditada de RQ14 ya cubre
el caso legitimo de soporte de A&D.

## Referencias

- `RQ01_Configuracion_General_y_Organizacional_v3` - RF05, y seccion 3 (3 capas de acceso)
- `PLAN DE ARRANQUE DESARROLLO` §2.1 - divergencia de fail-closed y bug de claims en Blazor
- `CLAUDE.md` - invariante 10
- `ADR-003` - estructura organizacional (Capa 2 del control de acceso)
