# PROGRESO - TRONOX SGDEA

Bitacora de entregables y decisiones. Fuente de verdad funcional: vault Obsidian
`OBSIDIAN.TRONOX` (empezar por `00 - INDICE.md`).

---

## Fase 0 - Fundacion

| # | Entregable | Estado |
|---|---|---|
| 0.1 | Clonar backbone + recablear remotos | HECHO |
| 0.3 | Podar dominio ajeno | HECHO (queda barrido de residuos) |
| 0.2 | Renombrar `Ecorex.*` -> `Tronox.*` + reestructura de hosts | PENDIENTE |
| 0.4 | Ids `Guid` -> `BIGINT` (ADR-001 / DAT-01) | PENDIENTE |
| 0.5 | Docker con bloque de puertos propio + MinIO + preflight | PENDIENTE |
| 0.6 | Migracion inicial limpia PostgreSQL | PENDIENTE |
| 0.7 | Test de aislamiento cross-tenant en verde | PENDIENTE (el test existe, falta correrlo) |
| 0.8 | Plantilla Velzon integrada | PENDIENTE |
| 0.9 | `CLAUDE.md` reescrito para TRONOX | PENDIENTE |

### 0.1 - Clonado (cerrado, verificado)

- Clon de `C:\desarrolloia\ecorex.tareas` (punta `64bde77` de `main`).
- **Historia limpia**: raiz huerfana `57f9de2`, sin padres. `.git` 97.2 MB -> 10.9 MB.
  Se eliminaron los dos tags heredados que anclaban los 495 commits del backbone.
- Motivo: el repo destino es PUBLICO y la historia del backbone contenia la clave de
  BD de desarrollo `EcorexDev2026pg` y un `Demo123*` de usuario semilla. No habia `.env`
  real ni claves de API ni secretos de produccion (verificado sobre las 500 revisiones).
- Remotos: `origin` -> TronoxDev · `backbone` -> ecorex local (solo lectura, para
  recuperar piezas puntuales; con historia huerfana NO hay cherry-pick).
- **No se ha hecho push todavia.** Puerta previa al primer push: escaneo de secretos.

### 0.3 - Poda (compila en verde, falta barrido de residuos)

Eliminado: proyecto SqlServer y su matriz de tests · scaffold `Ecorex.Web`/`Web.Client` ·
`Ecorex.Shared` vacio · agente colmena (`apps/agent`) · `apps/web-prototype` ·
106 entidades de dominio · 15 carpetas de Application · 6 integraciones de Infrastructure ·
50 paginas Razor · 58 archivos de test · las 129 migraciones del backbone ·
`DatabaseSeeder` (SKY SYSTEM).

`dotnet build Ecorex.sln` **VERDE** (6 proyectos src + 4 de tests).

---

## Decisiones tomadas

| ID | Decision | Estado ADR |
|---|---|---|
| D-01 | Historia git huerfana en vez de linaje conservado | Pendiente de ADR |
| D-02 | `Ecorex.SuperAdmin` -> `Tronox.Web` (app del tenant); Console se crea nuevo y delgado con identidad y clave JWT propias. Contradice PLAN DE ARRANQUE 4, que mapeaba `Tronox.Web <- Ecorex.Web`: en el repo real ese proyecto es el scaffold vacio de la plantilla y toda la base de RF03/RF05/RF09 vive en SuperAdmin | **Pendiente ADR-002 + actualizar el vault** |
| D-03 | TRONOX no procesa pagos: se elimina la pasarela y la ruta de cobro automatico del cambio de plan | Pendiente de ADR |

## Decisiones abiertas (bloquean Fase 1)

- **Dependencias vs Cargos (RF03/RF04 vs arbol unico de ECOREX).** Bloquea el
  entregable 1.4. Sin resolver; no tomarla por cuenta propia.

---

## Deuda registrada (no perder)

1. **Barrido de residuos de la poda.** Quedan referencias muertas que compilan pero no
   pertenecen: enums huerfanos en `Domain/Enums` (WhatsApp*, Wompi*, Evolution*,
   Workflow*, Rule*, DataContainer*), paquetes SqlServer/MsSql en varios `.csproj`,
   y cadenas de UI del backbone (incluido "Sky System" en `Login.razor`, que se rehace
   en 1.1).
2. **`IMenuProvisioningService`**: implementado en `Infrastructure/Persistence/MenuProvisioningService.cs`
   con el acceso Inicio + las 7 secciones canonicas. **Los 17 modulos y sus pantallas
   faltan**; se completan en 1.5 junto con el filtro por permisos.
3. **Pruebas dadas de baja a reponer**: `TenantUserServiceTests` (su doble de
   `IApplicationDbContext` declaraba todos los DbSets). Reponer en 1.3.
4. **Paginas a rehacer sobre Velzon**: `Inicio` (dashboard), `Cuenta` (RF07 Mi Perfil),
   `Metricas` (RQ13/RQ14). Se borraron por estar atadas a tareas/pagos/CRM.
5. **`/api/v1`**: se elimino `TenantApiService` (era ingesta de leads). La API con
   API Key se disena segun spec; la entidad `TenantApiConfig` se conserva.
6. **FAIL-OPEN heredado**: `Ecorex.SuperAdmin/Auth/CurrentPermissions.cs` resuelve
   `Unrestricted` para Owner/Admin y para usuarios sin rol. TRONOX debe ser
   **fail-closed** (maneja Reservado/Clasificado). Corregir en 1.3, y leer los claims
   del `AuthenticationState`, nunca del `HttpContext` (bug que ECOREX corrigio).
7. **Nombres de variables de entorno** siguen siendo `ECOREX_*`; se renombran en 0.2.

---

## Notas para el vault

- `DAT-01` dice `tenant_id BIGINT UNSIGNED`: ese tipo es de MySQL, **no existe en
  PostgreSQL**. Se implementara como `bigint NOT NULL` con `CHECK (tenant_id > 0)`.
- `RF01` define `sigla` de 20 caracteres; la resolucion `M01` la limita a 10. Prevalece M01.
- `ARQ_Aislamiento_MultiTenant` describe GCP/Firestore y contradice el stack relacional
  de las 17 specs. Se trata como **obsoleta**.
