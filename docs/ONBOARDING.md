# TRONOX SGDEA - Puesta en marcha (otro PC / nuevo desarrollador)

Guia para clonar el repo, levantar la infraestructura local en Docker y dejar el sistema
corriendo. Pensada para arrancar desde cero en una maquina nueva.

---

## 1. Que es este proyecto

**TRONOX SGDEA** - Sistema de Gestion Documental Electronica de Archivo. Es un **SaaS
multi-tenant** para entidades publicas colombianas, conforme a la normativa del Archivo General
de la Nacion (AGN), el Decreto 1080/2015 y la Ley 1755/2015. Cliente: A&D GROUP. Desarrollo: Bitcode.

Son **17 modulos (RQ01-RQ17)** sobre una plataforma multi-tenant, mas una consola de plataforma
(RQ14) y cuatro portales externos. Se clono del backbone `ECOREX.tareas` y se podo su dominio
propio, heredando la columna vertebral SaaS (multi-tenant, identidad/JWT, menu configurable,
roles/permisos, organigrama, registro de modulos).

**Stack:**

| Capa | Decision |
|---|---|
| Plataforma | .NET 10 / ASP.NET Core, Clean Architecture |
| UI | Blazor Server interactivo (plantilla Velzon minimal) |
| Base de datos | **PostgreSQL, motor UNICO** (no hay SQL Server ni DAL dual) |
| Binarios | Object storage S3 (MinIO en local). Nunca BLOB en BD |
| Cache | Redis · **Colas** RabbitMQ |

**Lo que YA esta construido (corte actual):** RQ01 completo (Datos de la Entidad, Dependencias,
Cargos, Usuarios, Roles/Permisos, Menu, Niveles de clasificacion, Sedes, Fondos) y buena parte de
RQ02 (RF01 Versiones de TRD, RF02 Catalogo de Series, RF03 Listas, RF04 Construccion de la TRD,
RF06 Topografia Fisica). Ver **`PROGRESO.md`** para el detalle vivo.

### Documentos que hay que leer (en este orden)

1. **`CLAUDE.md`** (raiz) - contrato de desarrollo: los 10 invariantes, el stack, las convenciones,
   la estructura del repo y el bloque de puertos. **Lectura obligatoria antes de tocar codigo.**
2. **`PROGRESO.md`** (raiz) - bitacora de avance: que hay hecho, decisiones y deuda tecnica.
3. **`docs/decisiones/`** - ADRs del repo (arbol organizacional unico, fail-closed, hosts, etc.).
4. Las specs funcionales viven en un **vault Obsidian externo** (no en el repo); si no tienes acceso,
   `CLAUDE.md` + `PROGRESO.md` + los ADRs cubren lo necesario para desarrollar.

---

## 2. Requisitos de la maquina

- **Docker Desktop** (con al menos ~2 GB libres; corre 5 contenedores ligeros).
- **.NET 10 SDK** (`dotnet --version` debe dar 10.x).
- **git**.
- **PowerShell** (para `preflight.ps1`) y una terminal.

---

## 3. Clonar el repositorio (rama compartida)

El repo es **PUBLICO**. Se trabaja en la rama compartida `desarrollo`.

```bash
git clone https://github.com/alexandercuartas665/TronoxDev.git
cd TronoxDev
git checkout desarrollo        # rama compartida de trabajo
```

> Flujo de la rama compartida: `git pull --rebase` antes de empezar, commits pequenos y
> descriptivos en espanol (sin tildes en el mensaje), `git push` al terminar. Los merges a `main`
> se hacen por PR/acuerdo. **Nunca** subir secretos: el repo es publico.

---

## 4. Crear los archivos de configuracion locales (gitignored)

Dos archivos NO estan en el repo (contienen claves). Se crean desde sus plantillas:

### 4.1 `.env` del stack Docker

```powershell
cd deploy\docker
Copy-Item .env.example .env
# Edita .env: cambia cada 'cambia-esta-clave' por un valor propio. Recuerda el POSTGRES_PASSWORD.
```

### 4.2 Cadena de conexion de la app

```powershell
cd ..\..\apps\backend\src\Tronox.Web
Copy-Item appsettings.Development.local.example.json appsettings.Development.local.json
# Edita el Password: DEBE ser el mismo POSTGRES_PASSWORD que pusiste en deploy/docker/.env.
```

> El puerto de Postgres es **5443** en ambos archivos. Si ya alineaste Docker con estos mismos
> puertos, no hay que cambiar nada mas.

---

## 5. Levantar la infraestructura (Docker)

```powershell
cd deploy\docker
.\preflight.ps1                            # valida docker vivo y puertos libres (sin tumbar vecinos)
docker compose --env-file .env up -d
docker compose ps                          # los 5 contenedores 'healthy'
```

Servicios: `tronox-postgres` (5443), `tronox-redis` (6390), `tronox-rabbitmq` (5683/15683),
`tronox-minio` (9004/9005), `tronox-adminer` (8093). Detalle en `deploy/docker/README.md`.

Consola de BD (opcional): **http://localhost:8093** (Adminer) -> sistema PostgreSQL, servidor
`tronox-postgres`, usuario/clave/BD del `.env`.

---

## 6. Crear el esquema (migraciones EF)

Con la infra arriba y el `appsettings.Development.local.json` apuntando a `localhost:5443`:

```powershell
cd apps\backend
dotnet build Tronox.sln
# Pon la MISMA cadena que en appsettings.Development.local.json (mismo password que el .env):
$env:TRONOX_DB_CONNECTION = "Host=localhost;Port=5443;Database=tronox_dev;Username=tronox;Password=TU_PASSWORD_LOCAL"
dotnet ef database update --project src\Tronox.Infrastructure --startup-project src\Tronox.Infrastructure
```

Esto crea todas las tablas (multi-tenant, identidad, menu, roles, RQ01, RQ02, ...). Es idempotente.

> Alternativa: correr la app con `TRONOX_RUN_MIGRATIONS=true` y las migraciones se aplican al
> arrancar. `dotnet ef` deja el esquema listo sin arrancar la app.

---

## 7. Sembrar un usuario para poder entrar

La BD nace **vacia** (no hay seeder de demo, a proposito). Se crea el primer tenant + usuario Owner
por el camino REAL de alta (que ademas provisiona menu + roles + matriz de permisos). Como el SMTP
no esta configurado en local, la cuenta se activa por SQL.

Con la app corriendo (ver paso 8) en otra terminal:

```powershell
# 1) Registrar (crea la entidad + usuario Owner en estado PendingActivation)
curl.exe -s -X POST http://localhost:8095/auth/register `
  --data-urlencode "agencyName=Entidad Demo" `
  --data-urlencode "displayName=Administrador" `
  --data-urlencode "email=admin@demo.local" `
  --data-urlencode "password=Demo2026Local"

# 2) Activar la cuenta por SQL (y opcional: hacerla SuperAdmin de plataforma)
docker exec tronox-postgres psql -U tronox -d tronox_dev -c "update platform_users set status='Active', email_verified=true where email='admin@demo.local';"
```

Luego entra en **http://localhost:8095** con `admin@demo.local` / `Demo2026Local`.

> Estas credenciales son de ejemplo LOCAL; cada quien pone las suyas. Nunca se guardan en el repo.

---

## 8. Correr la app

```powershell
cd apps\backend
dotnet run --project src\Tronox.Web --launch-profile tronox-dev
```

Abre **http://localhost:8095**. El perfil `tronox-dev` usa entorno Development (persiste las llaves
de DataProtection en `.dpkeys-dev/`, asi que los reinicios no cierran la sesion).

Comprobaciones utiles:

```powershell
dotnet build Tronox.sln          # debe quedar verde
dotnet test  Tronox.sln          # tests (usan Postgres efimero via Testcontainers, no tocan tu BD)
```

---

## 9. Estructura del repo (mapa rapido)

```
TRONOXdev/
  apps/backend/
    Tronox.sln
    src/
      Tronox.Domain/          entidades + enums
      Tronox.Application/     servicios, DTOs, logica PURA (reglas, arbol, permisos) - testeable sin BD
      Tronox.Infrastructure/  EF Core PostgreSQL, migraciones, integraciones
      Tronox.Web/             app del tenant (Blazor Server)
      Tronox.Api/             /api/v1 con API Key
      Tronox.Workers/         procesos asincronos (SLA, OCR, notificaciones)
    tests/                    Application.Tests (puros) + Integration.Tests (Testcontainers)
  deploy/docker/              compose + .env.example + preflight.ps1 (infra local)
  docs/decisiones/            ADRs · docs/ONBOARDING.md (este archivo)
  CLAUDE.md · PROGRESO.md
```

Cada modulo nuevo sigue el mismo patron: entidad en Domain -> reglas puras + servicio + DTOs en
Application -> DbSet/config/migracion en Infrastructure -> pagina Blazor en Web -> tests. Mira un
modulo ya hecho (p. ej. Series o Topografia) como plantilla.

---

## 10. Problemas frecuentes

- **`dotnet ef` se conecta al puerto equivocado (5442):** la factory de diseno usa un fallback.
  Exporta `TRONOX_DB_CONNECTION` con tu cadena real (puerto 5443) antes de correr `dotnet ef`,
  como en el paso 6.
- **Login da "credenciales invalidas" tras recrear la BD:** la BD nueva no tiene tu usuario;
  repite el paso 7.
- **Un puerto esta ocupado:** cambialo en `deploy/docker/.env` (y en `appsettings.Development.local.json`
  si es el de Postgres) y vuelve a correr `preflight.ps1`.
- **La sesion se cierra al reiniciar la app:** verifica que corres con `--launch-profile tronox-dev`
  (Development), que persiste las llaves en `.dpkeys-dev/`.
