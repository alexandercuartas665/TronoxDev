# CLAUDE.md - Memoria del agente de desarrollo de TRONOX SGDEA

> Lectura obligatoria antes de modificar codigo en este repositorio.
> Convencion del proyecto: **solo ASCII** en archivos de codigo y scripts
> (`.cs`, `.ps1`, `.sh`, `.sql`). No aplica a HTML/CSS/JS de UI ni a Markdown.

---

## 1. Que es este proyecto

**TRONOX SGDEA** - Sistema de Gestion Documental Electronica de Archivo, SaaS multi-tenant
para entidades publicas colombianas. Cliente: A&D GROUP. Desarrollo: Bitcode.

Es un **producto nuevo (green field)**: no hay sistema legacy que migrar ni ETL de datos.
Se construye conforme a la normativa del Archivo General de la Nacion (AGN), el Decreto
1080/2015 y la Ley 1755/2015.

Son 17 modulos (RQ01-RQ17) sobre una plataforma multi-tenant, mas una consola de plataforma
separada y cuatro portales externos.

### Origen del codigo

Este repo se clono del backbone **ECOREX.tareas** y se renombro `Ecorex.*` -> `Tronox.*`.
Se heredo la columna vertebral SaaS ya probada (multi-tenant, identidad/JWT, menu
configurable, roles y permisos, organigrama, registro de modulos, gateway de IA) y se podo
todo su dominio propio (tareas, Kanban, BPMN, formularios EAV, motor de reglas, CRM,
WhatsApp, pagos, inventario, scraping).

- `origin`   -> https://github.com/alexandercuartas665/TronoxDev.git (**repositorio PUBLICO**)
- `backbone` -> `C:\desarrolloia\ecorex.tareas` (solo lectura, para recuperar piezas puntuales)

La historia de git es **huerfana**: TronoxDev arranca en un commit inicial propio. NO hay
cherry-pick desde el backbone; si hace falta una correccion suya, se copia el cambio a mano.

---

## 2. Fuente de verdad

Las especificaciones funcionales NO viven en este repo. Viven en el vault Obsidian:

```
C:\Users\acuartas\OneDrive - Bitcode IT Services S.A.S\Bitcode\13. Proyectos\042. Tronox\07. Obsidian\TRONOX
```

Empezar SIEMPRE por `00 - INDICE.md`. Orden de lectura antes de tocar un modulo:

1. `04. Notas para desarrollador/TRONOX_Observaciones_Tecnicas_v1.md` - las 23 decisiones
   cruzadas y las 7 decisiones transversales DAT-01..07. Es el contrato entre modulos.
2. `02. Inventario de modulos/MAPA_MENU_SISTEMA_TRONOX.md` - navegacion real del sistema.
3. La spec del modulo que se va a construir (`01. Requerimiento/REQ0XX/`).
4. `03. Hoja de Ruta desarrollo/` - PLAN DE ARRANQUE y HOJA DE RUTA.

**No reinterpretar requerimientos de memoria.** Si el codigo debe contradecir la spec, se
pregunta al usuario, se escribe un ADR en `docs/decisiones/` y se avisa para actualizar el vault.

> Nota: `04. Notas para desarrollador/ARQ_Aislamiento_MultiTenant.md` describe una arquitectura
> GCP/Firestore que **contradice** el stack relacional de las 17 specs. Se trata como OBSOLETA.

---

## 3. Stack

| Capa | Decision |
|---|---|
| Plataforma | .NET 10 / ASP.NET Core, Clean Architecture |
| UI | Blazor Server interactivo sobre la plantilla **Velzon minimal** |
| Base de datos | **PostgreSQL, motor UNICO.** No hay DAL dual |
| Binarios | Object storage S3 (MinIO en local). **Nunca BLOB en base de datos** |
| Cache | Redis (KPIs, menu resuelto por `(tenant_id, menu_vista_id)`) |
| Colas | RabbitMQ (motor SLA, OCR, eventos de workflow, notificaciones, ETL de analitica) |
| Seguridad | bcrypt/argon2 (cost >= 12), AES-256 para secretos, SHA-256 integridad, TLS 1.2+ |

---

## 4. LOS 10 INVARIANTES (rechaza tu propio codigo si los rompe)

1. **Aislamiento por tenant (DAT-01).** Toda tabla lleva `tenant_id`, generado solo por la
   tabla `tenants` (RQ14). Toda query filtra por el. El filtro global de EF se aplica por
   reflexion **solo a `ITenantScoped`**: tener la columna no basta. El test de aislamiento
   cross-tenant DEBE fallar si esto se rompe.
2. **Terceros como fuente unica (DAT-02).** Todo actor externo (ciudadano, remitente,
   contratista, postulante...) es UN registro en `terceros` (RQ07). Ningun modulo crea
   tablas propias de personas o empresas externas.
3. **Inmutabilidad TRD (DAT-03).** Expedientes y documentos conservan el `trd_detalle_id`
   de la version bajo la que nacieron. Nada se recalcula solo (Acuerdo AGN 002/2014).
4. **Metadatos sobre RQ02 (DAT-04).** RQ12, RQ15 y RQ17 NO extienden la tabla `expedientes`;
   usan el motor de metadatos de RQ02 mas tablas de proceso propias que referencian
   `expediente_id`.
5. **Contrato de firma estable (RQ05).** `solicitarFirma` / `consultarEstadoFirma` /
   `cancelarFirma`. Nunca se cambian esas firmas; si falta algo, se agrega.
6. **Un solo motor SLA (DAT-06).** El contador vive en RQ09. RQ15 solo le pasa el termino
   legal calculado. El panel de RQ09 es la fuente unica de vencimientos.
7. **Interruptor maestro de IA (DAT-07).** `tenants.ia_habilitada` manda sobre `ia_config`.
   En false, ningun elemento de IA se renderiza (ni el icono).
8. **Sin eliminacion real.** Expedientes, documentos, terceros y dependencias se
   inactivan/archivan con motivo y auditoria. Unica excepcion: borrador nunca archivado (RQ04).
9. **Binarios en object storage**, nunca BLOB en base de datos.
10. **FAIL-CLOSED en permisos.** Un usuario sin rol, o una resolucion de permisos que falla,
    resuelve a **SIN PERMISOS**. El backbone es fail-open a proposito; TRONOX no puede serlo
    porque maneja niveles Reservado/Clasificado.

### Dos trampas heredadas del backbone

- **Claims en Blazor interactivo:** la resolucion de permisos debe leer los claims del
  `AuthenticationState`, **nunca** del `IHttpContextAccessor`. En un circuito interactivo no
  hay `HttpContext`: los claims salen nulos, la resolucion cae en fail-open y el gateado en
  pagina no restringe a nadie.
- **Aprovisionamiento del menu:** debe colgar del camino de ALTA DEL TENANT, no de un seeder.
  En el backbone solo se sembraba el tenant demo, asi que los clientes creados desde el panel
  nacian sin menu y sus usuarios no veian nada.

---

## 5. Seguridad (el repositorio es PUBLICO)

- Ninguna credencial, cadena de conexion, clave de API ni contrasena entra al repo.
  Todo en `.env` local (gitignored) o gestor de secretos.
- Prohibido MD5/SHA-1 para contrasenas.
- Secretos por tenant (SMTP, API keys, buzones) cifrados AES-256 en base de datos.
- OTP en portales externos, reCAPTCHA v3 en formularios publicos, rate limiting en API y login.
- Console con identidad y clave JWT **separadas** de la app del tenant.
- Pistas de auditoria append-only (RNF-04): ningun rol puede modificarlas ni borrarlas.

---

## 6. Diseno: Velzon minimal (milimetrico)

Plantilla en `D:\HTML\velzon-minimal`, ya compilada.

1. Se usan las clases y el CSS de Velzon **tal cual** (`bootstrap.min.css`, `app.min.css`,
   `icons.min.css`, `custom.min.css`).
2. **PROHIBIDO editar `app.min.css` o `bootstrap.min.css`.** Toda personalizacion de marca
   TRONOX va EXCLUSIVAMENTE en `assets/css/custom.css`, que la plantilla deja vacio como
   punto de extension.
3. El layout se controla por los `data-*` del `<html>` (`data-layout`, `data-topbar`,
   `data-sidebar`, `data-sidebar-size`, `data-preloader`), no reescribiendo markup.
   El comportamiento lo maneja `assets/js/layout.js`.
4. **Cada pantalla nueva PARTE de la pagina equivalente de la plantilla**, no de un HTML
   en blanco.
5. No se introducen frameworks CSS adicionales ni componentes de otra libreria.

---

## 7. Estructura del repositorio

```txt
TRONOXdev/
+-- apps/backend/
|   +-- Tronox.sln
|   +-- src/
|   |   +-- Tronox.Domain/          entidades + enums
|   |   +-- Tronox.Application/     servicios, DTOs, logica pura (permisos, menu, arbol)
|   |   +-- Tronox.Infrastructure/  EF Core PostgreSQL, migraciones, integraciones
|   |   +-- Tronox.Web/             app del tenant (Blazor)  [ver ADR-002]
|   |   +-- Tronox.Api/             /api/v1 con API Key
|   |   +-- Tronox.Workers/         procesos asincronos (SLA, OCR, notificaciones)
|   +-- tests/
+-- deploy/docker/                  compose + .env.example + preflight.ps1
+-- docs/decisiones/                ADRs del repo
+-- PROGRESO.md                     bitacora de avance (OBLIGATORIA)
+-- CLAUDE.md                       este archivo
```

`Tronox.Console` (RQ14) se creara como host nuevo y delgado con identidad propia.

---

## 8. Infraestructura local (bloque de puertos DEDICADO)

La maquina corre ~30 contenedores de 8 stacks hermanos. **Ningun contenedor hermano puede
caerse al levantar TRONOX.**

| Servicio | Puerto host | Contenedor |
|---|---|---|
| PostgreSQL 16 | 5443 | tronox-postgres |
| Redis | 6390 | tronox-redis |
| RabbitMQ | 5683 / 15683 | tronox-rabbitmq |
| MinIO | 9004 / 9005 | tronox-minio |
| Adminer | 8093 | tronox-adminer |
| API | 8094 | tronox-api |
| Web | 8095 | tronox-web |

Proyecto compose `tronox`, red `tronox-net`, puertos parametrizados en `.env` (no versionado).

**Correr `deploy/docker/preflight.ps1` ANTES de `docker compose up`.** Tras levantar,
comparar `docker ps`: el numero de contenedores hermanos NO debe bajar.

Arranque de base de datos idempotente: se espera el healthcheck real (`pg_isready`), solo se
aplican migraciones pendientes, y **nunca** `EnsureDeleted`/`EnsureCreated` fuera de tests.

---

## 9. Convenciones tecnicas

- `BaseEntity` (id, `created_at/by`, `updated_at/by` por interceptor de `SaveChanges`) y
  `TenantEntity : ITenantScoped`.
- **Naming en BD:** snake_case; `pk_<tabla>`, `fk_<tabla>_<principal>_<col>`, `ix_<tabla>_<cols>`.
- **Enums persistidos como string** con longitud acotada, no como entero.
- **Resultados tipados** (`Ok / NotFound / Invalid / Conflict`) hacia la presentacion, no
  excepciones crudas.
- Patron transaccional que **se une a una transaccion existente** en vez de anidar.
- **Logica pura en Application** (resolver de permisos, filtro de menu, validacion de ciclos,
  expansion de candidatos): sin EF, testeable sin base de datos.
- Consecutivos con `SELECT FOR UPDATE` y scope `(tenant, tipo, anio)`.

---

## 10. Checklist antes de cada commit

- [ ] `dotnet build apps/backend/Tronox.sln` verde.
- [ ] Test de aislamiento cross-tenant en verde.
- [ ] Ninguna query tenant-scoped sin filtro por `tenant_id`.
- [ ] Ningun actor externo persistido fuera de `terceros`.
- [ ] Binarios fuera de la base de datos.
- [ ] Sin secretos versionados ni en logs.
- [ ] Borrados son inactivacion/archivado con motivo y auditoria.
- [ ] Si se toca firma, se respetan los tres contratos de RQ05.
- [ ] Estilos propios solo en `custom.css`.
- [ ] Archivos de codigo y scripts en ASCII.
- [ ] `PROGRESO.md` actualizado. Decision nueva -> ADR en `docs/decisiones/` + avisar al
      usuario para reflejarla en el vault.
- [ ] Commits pequenos y descriptivos, en espanol, sin tildes en el mensaje.

---

## 11. Comandos

```powershell
# Build / test
cd C:\DesarrolloIA\TRONOXdev\apps\backend
dotnet build Tronox.sln
dotnet test  Tronox.sln

# Infraestructura local (preflight SIEMPRE primero)
cd C:\DesarrolloIA\TRONOXdev\deploy\docker
.\preflight.ps1
docker compose --env-file .env up -d
docker compose ps

# Migraciones EF (desde apps/backend)
dotnet ef migrations add NombreMigracion `
  --project src/Tronox.Infrastructure --startup-project src/Tronox.Infrastructure
dotnet ef database update `
  --project src/Tronox.Infrastructure --startup-project src/Tronox.Infrastructure
```
