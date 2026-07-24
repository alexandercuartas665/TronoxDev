---
type: ADR
status: accepted
date: 2026-07-24
---
# ADR-005: "No autenticado" y "no autorizado" dejan de ser el mismo destino

## Estatus

**Aceptado.** Complementa ADR-004 (fail-closed). **No lo relaja**: la decision de denegar no
cambia; cambia lo que el usuario ve cuando se le deniega, y se corrige un caso en que se denegaba
a quien SI tenia el permiso.

## Contexto

Sintoma reportado: *"cada vez que hago clic en cualquier opcion del menu, el sistema me saca al
login"*. El usuario seguia autenticado; lo que fallaba era la AUTORIZACION, y las dos fallas
terminaban en el mismo sitio.

Tres causas concurrentes:

1. **La raiz `/` exigia la policy `PlatformOperator`** (claim `platform_role`). Un usuario de
   tenant no la pasaba, y `Routes.razor` respondia a la falla de autorizacion con
   `<RedirectToLogin />`. Resultado: `GET /` con sesion valida -> 302 a `/login`.
2. **`AccessDeniedPath = "/login"`** en la cookie: cualquier 403 futuro tendria el mismo sintoma
   enganoso, indistinguible de una sesion expirada.
3. **El handler de permisos denegaba a usuarios que SI tenian el permiso.** Una pagina enrutable
   de Blazor con `[Authorize(Policy = "Perm:...")]` se autoriza DOS veces: en el middleware de
   autorizacion de la peticion HTTP y despues en el `AuthorizeRouteView` del circuito. El handler
   resolvia la matriz por `ICurrentPermissions.GetAsync()`, que lee el `AuthenticationState`; en
   la primera evaluacion ese estado todavia no existe, el proveedor lanza, la resolucion cae en su
   rama fail-closed y la pagina responde 302 al `AccessDeniedPath` — es decir, al login. El menu
   mostraba la opcion (el filtro del menu si resolvia bien, dentro del render) y la pagina la
   negaba: exactamente "hago clic en el menu y me saca".

Ademas, el sidebar podia generar `<a>` para nodos AGRUPADORES. Sus rutas (`configuracion`,
`req001`, `req001-organizacional`...) existen porque son la LLAVE DE PERMISOS del nodo (RF09
5.9.3) y de ellas se deriva el catalogo de modulos de RF05, pero ninguna pagina las atiende: un
enlace ahi da 404, y un nodo sin ruta daria `href=""`, que resuelve a `/` — la causa 1.

## Decision

**1. `/` reparte por rol en vez de exigir `PlatformOperator`.** Solo exige estar autenticado:
operador de plataforma -> consola de gobierno; usuario de tenant -> `/inicio`; autenticado sin
ninguno de los dos -> acceso denegado (no se le inventa un destino). El super administrador
conserva su consola porque conserva su `platform_role`.

**2. Pantalla propia de ACCESO DENEGADO.** `AccessDeniedPath = "/acceso-denegado"` y el bloque
`<NotAuthorized>` de `Routes.razor` distingue: sin identidad -> `/login`; autenticado sin permiso
-> la pantalla, que dice que la sesion sigue activa, que ruta se pidio y como se resuelve (asignar
rol + marcar "Ver" en la matriz de RF05). La pantalla NO lleva policy: es el mensaje que explica
una denegacion, no un recurso protegido.

**3. La matriz se resuelve con el principal del contexto de autorizacion.** El handler usa
`AuthorizationHandlerContext.User` (`ICurrentPermissions.GetForAsync`) en vez de releer el
`AuthenticationState`. Ese principal es la MISMA identidad en la peticion y en el circuito, asi
que la respuesta ya no depende de en cual de las dos evaluaciones se este.

**4. El sidebar no enlaza nodos agrupadores.** `Seccion` y `Grupo/modulo` se pintan como
encabezado y desplegable; solo `Item`/`QuickLink` **con ruta** generan enlace. La ruta se
CONSERVA en la semilla: es la llave de permisos y la fuente del catalogo de modulos: quitarla
habria roto la matriz de RF05 y los tests que cuentan modulos.

## Consecuencias

- Se corrige una **denegacion falsa**: usuarios con el permiso en su matriz ya pueden abrir las
  paginas con policy `Perm:*` por navegacion directa. Esto no abre nada nuevo: la matriz sigue
  siendo la unica fuente, y quien no la tenga sigue denegado.
- ADR-004 punto 3 sigue vigente: **no se lee el `IHttpContextAccessor`**. La identidad viene del
  principal que el framework ya resolvio, no de un acceso al `HttpContext`.
- La memoizacion de `CurrentPermissions` pasa a estar indexada por `(tenant, usuario)` porque en
  la misma peticion se resuelve por principal explicito y por AuthenticationState.
- Un fallo de resolucion **no se cachea**: sigue denegando esa llamada, pero un error transitorio
  de base ya no deja al usuario sin permisos durante toda la peticion.
- El mensaje de denegacion revela el nombre de la ruta pedida. Es informacion que el usuario ya
  tenia (la escribio o la clico) y sin ella el mensaje no sirve para diagnosticar.

## Referencias

- `CLAUDE.md` - invariante 10 (fail-closed)
- `ADR-004` - enforcement fail-closed y anclaje del super administrador
- `RQ01_Configuracion_General_y_Organizacional_v3` - RF05, RF09 5.9.3 (la ruta como llave de permisos)
