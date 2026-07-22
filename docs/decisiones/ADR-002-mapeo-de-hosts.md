---
type: ADR
status: accepted
date: 2026-07-21
---
# ADR-002: Mapeo de hosts al clonar el backbone

## Estatus

Aceptado. Corrige el mapeo escrito en PLAN DE ARRANQUE §4.

## Contexto

El PLAN DE ARRANQUE §4 define el mapeo de proyectos al clonar ECOREX:

```
Tronox.Web     <- Ecorex.Web         (app del tenant)
Tronox.Console <- Ecorex.SuperAdmin  (RQ14)
```

Al inspeccionar el repositorio real, ese mapeo no se sostiene:

- **`Ecorex.Web` + `Ecorex.Web.Client` son el scaffold sin tocar** de la plantilla Blazor Web
  App de .NET. El proyecto cliente contiene unicamente `Counter.razor`. No hay ninguna pantalla
  de negocio.
- **`Ecorex.SuperAdmin` es la aplicacion real**: alrededor de 95 paginas Razor. Contiene tanto
  lo de plataforma (tenants, planes, metricas) como lo del tenant, y en particular
  `ConfiguracionMenu.razor`, `RolesPermisos.razor` y `Dependencias.razor`, que son justamente la
  base heredable de RF09, RF05 y RF03.

Seguir el mapeo literal dejaria `Tronox.Web` vacio y encerraria todo el valor heredado dentro de
`Tronox.Console`, ademas de mezclar la app de plataforma con la del tenant, que RQ14 exige
separadas (identidad y clave JWT propias).

## Decision

1. **`Ecorex.SuperAdmin` se renombra a `Tronox.Web`**: es la aplicacion del tenant
   (`{sigla}.tronox.co`), donde viven RF03/RF05/RF09.
2. **`Ecorex.Web` y `Ecorex.Web.Client` se eliminan**: son scaffold vacio.
3. **`Tronox.Console` se crea como host NUEVO y delgado**, con identidad y clave JWT separadas
   de las del tenant, conforme a RQ14. No hereda el host mezclado del backbone.
4. `Ecorex.SuperAdmin.Tests` pasa a `Tronox.Web.Tests`.

## Consecuencias

- El backbone heredado se aprovecha donde esta el valor real, en vez de arrancar la app del
  tenant desde cero.
- Queda deuda explicita: `Tronox.Web` contiene todavia pantallas que pertenecen a Console
  (`Tenants`, `Plans`, `EquipoPlataforma`, `Anuncios`). Al construir `Tronox.Console` (Fase 2)
  esas pantallas se mueven, y con ellas la separacion de identidades.
- Hay que actualizar PLAN DE ARRANQUE §4 en el vault para que el mapeo documentado coincida
  con el codigo.

## Referencias

- `PLAN DE ARRANQUE DESARROLLO` §4
- `RQ14_TRONOX_Console_v1` - identidad y 2FA propias de la consola de plataforma
