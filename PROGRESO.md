# PROGRESO - TRONOX SGDEA

Bitacora de entregables y decisiones. Fuente de verdad funcional: vault Obsidian
`OBSIDIAN.TRONOX` (empezar por `00 - INDICE.md`).

> Nota: en este archivo, **ECOREX** siempre se refiere al sistema hermano del que se clono
> el backbone (`C:\desarrolloia\ecorex.tareas`). No confundir con TRONOX.

---

## Fase 0 - Fundacion

| # | Entregable | Estado |
|---|---|---|
| 0.1 | Clonar backbone + recablear remotos | HECHO |
| 0.2 | Renombrar `Ecorex.*` -> `Tronox.*` + reestructura de hosts | HECHO (build verde) |
| 0.3 | Podar dominio ajeno | HECHO (queda barrido de residuos) |
| 0.4 | Ids `Guid` -> `BIGINT` (ADR-001 / DAT-01) | HECHO (build verde) |
| 0.5 | Docker: bloque de puertos propio + MinIO + preflight | HECHO (5 servicios healthy, 30 hermanos intactos) |
| 0.6 | Migracion inicial limpia PostgreSQL | HECHO (29 tablas, `tenant_id bigint NOT NULL`, idempotente) |
| 0.7 | Test de aislamiento cross-tenant en verde | HECHO (6/6 ejecutados, incluida la guarda estructural) |

---

## INCIDENTE - El filtro de tenant estuvo desactivado (resuelto)

**Que paso.** Durante la poda (0.3) se filtraron los bloques `modelBuilder.Entity<X>(...)`
del DbContext por balance de parentesis, conservando solo las entidades vivas. Ese filtro
tambien elimino la linea:

```csharp
modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
```

porque estaba dentro del metodo generico `ApplyTenantFilter<TEntity>` y `TEntity` no figuraba
en la lista de entidades a conservar. El metodo quedo **con el cuerpo vacio**.

**Por que es grave.** La solucion siguio compilando en verde, la aplicacion habria arrancado
sin un solo error, y **toda consulta habria devuelto filas de todos los tenants**. El
aislamiento multi-tenant (DAT-01), que es el invariante numero uno del sistema, estuvo
desactivado durante varios commits sin ninguna senal.

**Como se detecto.** Al ejecutar por primera vez `TenantIsolationTests` (entregable 0.7):
2 de 3 fallaron. Sin ese test, el defecto habria llegado a produccion.

**Correccion.** Linea restaurada, con un comentario "NO BORRAR" que explica el riesgo.

**Refuerzo anadido.** Nuevo `TenantFilterGuardTests` que valida la ESTRUCTURA del modelo, no
solo el comportamiento: falla si alguna entidad `ITenantScoped` se queda sin filtro global, si
`tenant_id` fuera nullable, o si el modelo se quedara sin entidades scoped. **Se verifico que
la guarda realmente detecta el fallo** desactivando el filtro a proposito y confirmando que
la prueba se pone en rojo.

**Leccion.** Las transformaciones masivas de codigo por patron textual pueden desactivar en
silencio una defensa de seguridad. Todo cambio de este tipo debe cerrarse EJECUTANDO los
tests de invariantes, no solo compilando.
| 0.8 | Plantilla Velzon integrada | PARCIAL (assets copiados; falta aplicarla al layout) |
| 0.9 | `CLAUDE.md` reescrito para TRONOX | HECHO |

### 0.1 - Clonado (cerrado, verificado)

- Clon de `C:\desarrolloia\ecorex.tareas` (punta `64bde77` de `main`).
- **Historia limpia**: raiz huerfana `57f9de2`, sin padres. `.git` 97.2 MB -> 10.9 MB.
  Se eliminaron los dos tags heredados que anclaban los 495 commits del backbone.
- Motivo: el repo destino es PUBLICO y la historia de ECOREX contenia su clave de BD de
  desarrollo (`EcorexDev2026pg`) y un `Demo123*` de usuario semilla. No habia `.env` real,
  ni claves de API, ni secretos de produccion (verificado sobre las 500 revisiones).
- Remotos: `origin` -> TronoxDev · `backbone` -> ECOREX local (solo lectura, para recuperar
  piezas puntuales; con historia huerfana **NO hay cherry-pick**).
- **No se ha hecho push todavia.** Puerta previa al primer push: escaneo de secretos.

### 0.2 - Renombrado (cerrado)

343 archivos reescritos, 10 proyectos y sus carpetas renombrados, `Ecorex.sln` -> `Tronox.sln`.
Variables de entorno `ECOREX_*` -> `TRONOX_*`. Cero ocurrencias de "ecorex" en el codigo.
Se eliminaron los 47 ADRs heredados de ECOREX y su carpeta `docs/arquitectura`.

> Efecto colateral detectado y corregido: el reemplazo global tambien reescribio este
> PROGRESO.md, convirtiendo las referencias historicas a ECOREX en "Tronox". Por eso la
> nota del encabezado.

### 0.3 - Poda (compila en verde, falta barrido de residuos)

Eliminado: proyecto `Infrastructure.SqlServer` y su matriz de tests · scaffold `Ecorex.Web` /
`Ecorex.Web.Client` · `Ecorex.Shared` vacio · agente colmena (`apps/agent`) ·
`apps/web-prototype` · 106 entidades de dominio · 15 carpetas de Application ·
6 integraciones de Infrastructure · 50 paginas Razor · 58 archivos de test ·
las 129 migraciones del backbone · `DatabaseSeeder` (tenant demo SKY SYSTEM).

`dotnet build Tronox.sln` **VERDE** (6 proyectos src + 4 de tests).

### 0.5 - Docker (cerrado, verificado)

Bloque en uso: 5443 (postgres) / 6390 (redis) / 5683 y 15683 (rabbitmq) / 9004 y 9005 (minio)
/ 8093 (adminer). Proyecto compose `tronox`, red `tronox-net`, volumenes prefijados.
El `.env` local tiene claves aleatorias de 24 caracteres y esta gitignored.

**Criterio de salida verificado:** los 5 contenedores `tronox-*` quedaron `healthy` y el
conteo de contenedores hermanos se mantuvo en **30** antes y despues del `compose up`.

### 0.8 - Velzon (parcial)

Copiados a `wwwroot/velzon` (22 MB, 497 archivos): `css`, `js`, `fonts` y las libs
`bootstrap`, `simplebar`, `node-waves`, `feather-icons`.
**NO** se copiaron `images/` (25 MB de fotos demo) ni las librerias de graficos (echarts,
apexcharts, chart.js, ckeditor...): se agregan cuando un modulo las necesite.
`custom.css` sigue siendo el UNICO punto de extension permitido.
Falta aplicar la plantilla al layout y a las pantallas.

---

## Decisiones tomadas

| ID | Decision | ADR |
|---|---|---|
| D-01 | Historia git huerfana en vez de linaje conservado (repo publico) | pendiente |
| D-02 | `Ecorex.SuperAdmin` -> `Tronox.Web` (app del tenant); Console se crea nuevo y delgado | **ADR-002** |
| D-03 | TRONOX no procesa pagos: fuera la pasarela y el cobro automatico del cambio de plan | pendiente |
| D-04 | Estructura organizacional: **arbol unico con clasificador** (modelo ECOREX) | **ADR-003** |
| D-05 | Ids de entidad a `BIGINT` antes de la migracion inicial | ADR-001 del vault |

### ACCION PENDIENTE SOBRE EL VAULT (importante)

1. **RQ01 -> RF03 y RF04**: la decision D-04 **contradice la spec vigente**, que define
   Dependencias y Cargos como catalogos separados. Hay que reescribir RF03/RF04 segun
   `ADR-003` y subir la version de RQ01. Mientras no se haga, codigo y spec divergen.
2. **PLAN DE ARRANQUE 4**: corregir el mapeo de hosts segun `ADR-002`.
3. **PLAN DE ARRANQUE 2.2**: marcar la decision de dependencias como resuelta.

---

## Deuda registrada (no perder)

1. **Barrido de residuos de la poda.** Quedan referencias muertas que compilan pero no
   pertenecen: enums huerfanos en `Domain/Enums` (WhatsApp*, Wompi*, Evolution*, Workflow*,
   Rule*, DataContainer*), paquetes SqlServer/MsSql en varios `.csproj`, y cadenas de UI
   del backbone (incluido "Sky System" en `Login.razor`, que se rehace en 1.1).
2. **`IMenuProvisioningService`**: implementado en
   `Infrastructure/Persistence/MenuProvisioningService.cs` con el acceso Inicio + las 7
   secciones canonicas. **Los 17 modulos y sus pantallas faltan**; se completan en 1.5 junto
   con el filtro por permisos.
3. **Pruebas dadas de baja a reponer**: `TenantUserServiceTests` (su doble de
   `IApplicationDbContext` declaraba todos los DbSets). Reponer en 1.3.
4. **Paginas a rehacer sobre Velzon**: `Inicio` (dashboard), `Cuenta` (RF07 Mi Perfil),
   `Metricas` (RQ13/RQ14). Se borraron por estar atadas a tareas/pagos/CRM.
5. **`/api/v1`**: se elimino `TenantApiService` (era ingesta de leads). La API con API Key
   se disena segun spec; la entidad `TenantApiConfig` se conserva.
6. **FAIL-OPEN heredado**: `Tronox.Web/Auth/CurrentPermissions.cs` resuelve `Unrestricted`
   para Owner/Admin y para usuarios sin rol. TRONOX debe ser **fail-closed**. Corregir en 1.3,
   y leer los claims del `AuthenticationState`, nunca del `HttpContext`.
7. **Pantallas de plataforma dentro de `Tronox.Web`** (`Tenants`, `Plans`, `EquipoPlataforma`,
   `Anuncios`): se mueven a `Tronox.Console` en Fase 2 (ver ADR-002).
8. **Un commit no atomico**: `3a773ac` ("docker: ...") arrastro trabajo en curso del
   renombrado de ids porque se uso `git add -A` mientras corria un agente en paralelo.
   No afecta el estado final; solo la legibilidad de la historia.

---

## Notas para el vault

- `DAT-01` dice `tenant_id BIGINT UNSIGNED`: ese tipo es de MySQL, **no existe en
  PostgreSQL**. Se implementa como `bigint NOT NULL` con `CHECK (tenant_id > 0)`.
- `RF01` define `sigla` de 20 caracteres; la resolucion `M01` la limita a 10. Prevalece M01.
- `ARQ_Aislamiento_MultiTenant` describe GCP/Firestore y contradice el stack relacional de
  las 17 specs. Se trata como **obsoleta**.
