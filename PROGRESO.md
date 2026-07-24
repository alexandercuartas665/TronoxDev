# PROGRESO - TRONOX SGDEA

Bitacora de entregables y decisiones. Fuente de verdad funcional: vault Obsidian
`OBSIDIAN.TRONOX` (empezar por `00 - INDICE.md`).

> En este archivo, **ECOREX** siempre se refiere al sistema hermano del que se clono el
> backbone (`C:\desarrolloia\ecorex.tareas`). No confundir con TRONOX.

**Estado a 2026-07-24:** 36 commits · **389 tests en verde** · 6 migraciones aplicadas ·
desplegado en produccion (demo) · RQ01 con 6 de 9 RF construidos.

---

## 1. Fase 0 - Fundacion (CERRADA)

| # | Entregable | Estado |
|---|---|---|
| 0.1 | Clonar backbone + recablear remotos | HECHO (historia huerfana, `.git` 97 -> 10.9 MB) |
| 0.2 | Renombrar `Ecorex.*` -> `Tronox.*` | HECHO (343 archivos, 10 proyectos) |
| 0.3 | Podar dominio ajeno | HECHO (106 entidades, 15 carpetas, 129 migraciones) |
| 0.4 | Ids `Guid` -> `BIGINT` (DAT-01) | HECHO |
| 0.5 | Docker: bloque de puertos propio + preflight | HECHO (5 servicios healthy, 30 vecinos intactos) |
| 0.6 | Migracion inicial limpia PostgreSQL | HECHO (29 tablas, `tenant_id bigint NOT NULL`) |
| 0.7 | Test de aislamiento cross-tenant | HECHO (+ guarda estructural) |
| 0.8 | Plantilla Velzon integrada | HECHO |
| 0.9 | `CLAUDE.md` propio de TRONOX | HECHO |

Puertos de desarrollo: 5443 postgres · 6390 redis · 5683/15683 rabbitmq · 9004/9005 minio ·
8093 adminer · 8095 web. Proyecto compose `tronox`, red `tronox-net`.

---

## 2. Fase 1 - RQ01 Configuracion General y Organizacional

| RF | Modulo | Estado |
|---|---|---|
| RF01 | **Datos de la Entidad** | HECHO - DV del NIT (algoritmo DIAN), `codigo_fondo_agn` autogenerado y de solo lectura (M01), sigla max 10, obligatoriedad condicional si Publica, selectores encadenados, subformulario de Sedes |
| RF01-P.3 | **Niveles de Clasificacion** | HECHO - los 4 niveles sembrados al alta del tenant |
| RF02 | **Fondos Documentales** | HECHO (backend) - codigo unico por tenant, fondo Cerrado de solo lectura, `sede_id` NULL = transversal. **Sin pantalla propia todavia** |
| RF03 | **Dependencias** | HECHO - arbol unico con clasificador (ADR-003), ciclos fail-closed, archivado, vigencias, sucesora |
| RF04 | **Catalogo de Cargos** | HECHO - vista de catalogo de los nodos `Cargo`; `codigo_dafp` solo si la entidad es Publica; no se inactiva un cargo con funcionarios activos |
| RF05 | **Roles y Permisos** | HECHO - 6 acciones, multi-rol con vigencia, union por OR, nivel maximo, **fail-closed** (ADR-004) |
| RF06 | **Usuarios / Funcionarios** | HECHO - documento y correo unicos por tenant, activacion exige dependencia+cargo+rol, dependencia **derivada** del cargo |
| RF07 | Mi Perfil | PENDIENTE |
| RF08 | Carga Masiva Asistida | PENDIENTE |
| RF09 | **Administrador de Menu** | HECHO - menu en BD (136 nodos), arbol canonico del prototipo, filtrado por permisos |

**Lo que NO existe todavia:** ningun modulo de RQ02 a RQ17. El menu muestra 106 opciones,
pero ~91 llevan a una ficha de "modulo pendiente". **Menu completo != sistema construido.**

---

## 3. Interfaz

- Login reconstruido sobre `auth-signin-cover` de Velzon con la marca de RQ01 (PLAN 3.1).
- Shell con el patron del prototipo: sidebar plano de 280px, sin el rail heredado, logo TRONOX +
  SGDEA, chip del tenant al pie.
- **5 paletas conmutables** portadas del prototipo (classic-light por defecto), persistidas en
  cookie y **renderizadas por el servidor** para que la navegacion no pierda el tema ni parpadee.
- Iconos Bootstrap Icons (MIT) vendorizados: los 117 nodos del menu con icono real.
- Panel de Control en `/inicio` con Chart.js vendorizado y **datos de demostracion marcados**.

---

## 4. Despliegue

Desplegado el 2026-07-23 en el host compartido `10.0.0.3` (con Visal, ECOREX, DokTrino y 11
stacks mas). Puerto **5680**, Postgres interno, proyecto compose `tronox`.
**27 contenedores vecinos antes y despues del alta: ninguno se cayo.**
Runbook completo y credenciales en el vault (`06. Deploy`).

Pendiente para URL publica: **dominio + bloque en el Caddy externo** (decision del usuario).

---

## 5. Decisiones tomadas (ADR en `docs/decisiones/`)

| ID | Decision | ADR |
|---|---|---|
| D-01 | Historia git huerfana (repo publico) | - |
| D-02 | `Ecorex.SuperAdmin` -> `Tronox.Web`; Console sera host nuevo | **ADR-002** |
| D-03 | TRONOX no procesa pagos: fuera la pasarela | - |
| D-04 | Estructura organizacional: **arbol unico con clasificador** | **ADR-003** |
| D-05 | Ids de entidad `BIGINT` | ADR-001 (vault) |
| D-06 | Enforcement **fail-closed** + anclaje del Super Administrador | **ADR-004** |
| D-07 | **No autenticado != no autorizado** | **ADR-005** |
| D-08 | Diseno **hibrido**: estructura Velzon + tokens de marca del prototipo | pendiente de ADR |

---

## 6. Defectos encontrados y corregidos (los que importan)

1. **El filtro global de tenant quedo desactivado** por la poda: el metodo generico
   `ApplyTenantFilter<TEntity>` se quedo vacio. Compilaba, arrancaba y **habria servido datos de
   todos los tenants**. Lo detecto el test de aislamiento al ejecutarlo por primera vez.
   Se restauro y se anadio `TenantFilterGuardTests` (guarda estructural), **verificando que la
   guarda detecta el fallo** al reintroducirlo a proposito.
2. **La auditoria de altas guardaba `EntityId = 0`**: `AuditWriter` copiaba el id antes de que la
   base lo generara. Afectaba a las 10 entradas de creacion. Se resolvio con resolucion diferida
   en la unidad de trabajo, no con 10 parches.
3. **Invitar usuario estaba roto**: se leia el id del `PlatformUser` antes de guardarlo (FK 23503).
4. **Poda excesiva**: se elimino `ConnectEndpoints.cs`, que **era la autenticacion de la API**
   (`/connect/token`). Se restauro. Leccion: al podar por lotes, juzgar cada archivo por su
   contenido, no por su vecindad.
5. **La matriz de permisos nacia vacia**: el catalogo de modulos se deriva del menu y el menu solo
   sembraba 8 nodos, asi que el Super Administrador nacia **sin un solo permiso** y con
   fail-closed el sistema era inusable. Se sembro el arbol canonico completo.
6. **Clic en el menu expulsaba al login** (reportado por el usuario): el handler de permisos leia
   del `AuthenticationState`, que **no tiene estado en la pasada HTTP** del middleware -> excepcion
   -> fail-closed -> `AccessDeniedPath` (= `/login`). Se resolvio usando
   `AuthorizationHandlerContext.User`, sin volver a tocar `IHttpContextAccessor` (ADR-004 intacto).
7. **La imagen de produccion casi se lleva la cadena de conexion de DESARROLLO**: `Program.cs`
   carga `appsettings.Development.local.json` incondicionalmente y el archivo entraba a la imagen.
   Se atrapo verificando la imagen en local ANTES de subirla.
8. **Un token de Mapbox** venia dentro de la plantilla Velzon y bloqueo el primer push. Se purgo
   de la historia. Mi escaneo previo no lo detecto: buscaba `password=`, no formatos de token.

> Patron: **ninguno de estos lo encontro el compilador.** Los encontraron los tests de
> invariantes, arrancar la aplicacion de verdad, y el usuario usandola.

---

## 7. Deuda tecnica registrada

1. **Modulos funcionales:** RQ02 a RQ17 sin construir. 91 pantallas son fichas de "pendiente".
2. **DIVIPOLA incompleto:** 33 departamentos reales, pero solo **37 municipios** (32 capitales +
   5 de Cundinamarca) de los ~1.100 del DANE. Declarado en `DivipolaSeed.cs`.
3. **`/auth/register` (auto-registro publico) debe retirarse:** en TRONOX los tenants se
   aprovisionan desde la Console (RQ14), no por un formulario abierto en internet.
4. **Sin backup** del Postgres de produccion.
5. **Licencia de Velzon:** es una plantilla comercial y el repo de codigo es PUBLICO. Sus assets
   (22 MB) estan versionados. **Sin resolver.**
6. **Delegacion temporal (RF06)**: es el pendiente P-01 del vault, a la espera del Product Owner.
7. **Solo el Super Administrador cambia el estado de la entidad** (RF01): la UI lo dice, el backend
   aun no lo impide.
8. **`Sede` con ubicacion nullable**: volverla obligatoria exige decidir que pasa con las sedes ya
   creadas sin ubicacion.
9. **Sin disparador para re-aprovisionar** un tenant existente (menu/matriz) desde la UI.
10. **Vulnerabilidad transitiva** `System.Security.Cryptography.Xml` (3 avisos de severidad alta),
    heredada de `DataProtection`; no hay version superior que aplicar hoy.
11. Pantallas de plataforma dentro de `Tronox.Web` (`Tenants`, `Plans`, `EquipoPlataforma`,
    `Anuncios`): se mueven a `Tronox.Console` cuando exista (ADR-002).
12. **Fondos (RF02) sin pantalla**: hoy crear una dependencia exige que exista un fondo cargado
    por otra via.

---

## 8. Divergencias con el vault (ver `ESTADO DE IMPLEMENTACION` en el vault)

El codigo se aparta de la especificacion en 8 puntos, todos con decision explicita del usuario y
ADR. Estan consolidados en el vault para que el equipo que lea Obsidian no lea ficcion.
