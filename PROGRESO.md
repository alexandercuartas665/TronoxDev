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

---

## DEFECTO CORREGIDO (era ALTA) - La auditoria de creacion perdia el id de la entidad

**Estado: CORREGIDO el 2026-07-22, con test de regresion verificado en rojo.**

**Arreglo aplicado (sistemico, no diez parches).** Se anadio a `IApplicationDbContext` la
primitiva `DeferUntilIdsAssigned(Action)`, implementada en `TronoxDbContext.SaveChangesAsync`:
si hay trabajo diferido, vacia la cola, aplica join-or-begin de transaccion, guarda, ejecuta las
acciones ya con los ids reales, vuelve a guardar si hizo falta y confirma. Sin trabajo diferido
delega tal cual, asi que no cuesta nada en el 99% de los guardados.

`IAuditWriter` gana la sobrecarga preferente que recibe la **entidad** en vez de un `long?`. Esa
forma no inserta el asiento de inmediato: lo construye (serializando ya los valores) y difiere la
resolucion del id, de modo que el asiento se INSERTA una sola vez ya correcto. Nada de UPDATE
posterior, que es lo que exige el caracter append-only de RNF-04.

Migrados **30 sitios**, no solo los 10 de altas: la forma por entidad queda como norma tambien en
updates y deletes, para que sea el patron por defecto y no una excepcion que haya que recordar.
El unico que conserva la forma por id es `RolService` (`rol.save-permisos`), donde legitimamente
solo llega un `rolId` por parametro. `TenantAdminService` se alineo y volvio a un solo guardado.

Aparecieron **dos sitios afectados que no estaban en la lista original**: `EmailConfigService`
(`email.config.create`) y `AiServerConfigService` (`ai.provider.create`). Ambos con forma
"get-or-create", donde el asiento quedaba a 0 solo la primera vez: facil de no ver en pruebas
manuales.

**Riesgo residual conocido.** La sobrecarga por `long?` sigue existiendo y el compilador no impide
usarla en un alta. Esta documentada como excepcion. Cerrar del todo esa puerta (analizador Roslyn,
o marcarla `[Obsolete]` cuando no queden usos legitimos) queda como mejora futura.

**Propiedad del diseno a conocer.** La resolucion diferida se dispara en el siguiente
`SaveChangesAsync` del contexto. Un servicio que auditara y retornara SIN guardar dejaria el
asiento en cola. Se revisaron los 30 sitios y ninguno lo hace; el comportamiento anterior tenia
el mismo riesgo.

### Registro historico del defecto

`AuditWriter.Write` copia el `entityId` en el momento de la llamada:

```csharp
_db.SuperAdminAuditLogs.Add(new SuperAdminAuditLog { ... EntityId = entityId ... });
```

Con ids `Guid` generados en la aplicacion, el id ya existia cuando se auditaba. Con ids de
IDENTIDAD generados por la base, **vale 0 hasta que se ejecuta SaveChanges**. Resultado: toda
entrada de auditoria de una CREACION queda con `EntityId = 0` y no se puede rastrear hasta el
registro que documenta.

RNF-04 exige pistas de auditoria inalterables y completas. Una entrada de alta que no apunta a
nada incumple ese requisito.

**Sitios afectados (10), todos con el patron "auditar antes de guardar":**

| Archivo | Linea aprox. | Accion auditada |
|---|---|---|
| `Admin\OnboardingService.cs` | 115 | `tenant.onboard` |
| `Admin\PaymentAdminService.cs` | 43 | `payment.register` |
| `Admin\PlanAdminService.cs` | 40 | `plan.create` |
| `Admin\PlatformBrandingService.cs` | 76 | `platform.branding.save` |
| `Admin\PlatformOperatorService.cs` | 75 | `platform_operator.create` |
| `Admin\SubscriptionAdminService.cs` | 44 | `subscription.assign` |
| `Admin\GoogleAuthConfigService.cs` | 63 | `google.auth.create` |
| `Roles\RolService.cs` | 107 | `rol.create` |
| `Tenancy\BusinessUnitService.cs` | 77 | `business-unit.create` |
| `Tenancy\TenantUserService.cs` | 91 | `tenant-user.invite` |

`Admin\TenantAdminService.cs` (`tenant.create`) YA se corrigio guardando primero y auditando
despues, y sirve de referencia del arreglo puntual.

**Recomendacion: NO repetir ese arreglo diez veces.** Conviene un arreglo sistemico, por ejemplo
una sobrecarga `Write(entidad, ...)` que guarde la REFERENCIA a la entidad y resuelva su id en
el interceptor de `SaveChanges`, cuando ya existe. Asi el patron correcto es el camino facil y
el defecto no puede reaparecer al escribir el siguiente servicio.

**Anadir ademas un test de regresion** que cree una entidad y afirme que su entrada de auditoria
lleva un `EntityId` distinto de 0. Hoy no existe: por eso el defecto entro sin ruido.

---

## INCIDENTE - Poda excesiva de endpoints (corregido)

Al podar `Tronox.Api` se borro `ConnectEndpoints.cs` junto con los endpoints de WhatsApp y
la pasarela de pagos, por asociacion. **No era dominio ajeno: era la AUTENTICACION de la API**
(`/connect/token`, `/connect/switch-tenant`, `/connect/me`, `/platform/me`,
`/tenant/configurations`). Tambien se borro entero `TenantEndpoints.cs`, que mezclaba
`/tenant/users` (legitimo, RF06) con pipeline y dashboard de CRM.

Sin `/connect/token` la API no tiene forma de emitir un token: queda inutilizable.

**Como se detecto.** 17 tests de integracion del grupo Auth fallando con 404.

**Criterio que fallo.** La regla de corte era "si no aparece en las 17 specs ni sostiene la
fundacion multi-tenant, se borra". La autenticacion SI sostiene la fundacion; se elimino por
estar en la misma carpeta y en el mismo lote que codigo que si sobraba. Al podar por lotes hay
que revisar cada archivo por su contenido, no por su vecindad.

**Correccion en curso:** restaurar `ConnectEndpoints` completo y de `TenantEndpoints` solo el
grupo `/tenant/users`, adaptados a ids `long`.
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
7. **Dos aserciones debiles detectadas (marcadas, NO modificadas):**
   - `MenuConfigEditorTests.cs:190` - `Assert.NotEqual(0, keep)` es vacua: `keep` es un id de
     identidad generado por la base, nunca puede ser 0. Parece un resto de cuando la variable
     necesitaba un uso.
   - `TenantUsersTests.InvitedUsers_AreIsolatedPerTenant` **no comprueba lo que su nombre dice**:
     solo confirma que el usuario nuevo aparece en la lista de su propio tenant, no que sea
     invisible desde otro. Su propio comentario lo admite. Reescribirla al abordar 1.3.
8. **Pantallas de plataforma dentro de `Tronox.Web`** (`Tenants`, `Plans`, `EquipoPlataforma`,
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
