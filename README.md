# TRONOX SGDEA

Sistema de Gestion Documental Electronica de Archivo. SaaS multi-tenant para entidades publicas
colombianas (normativa AGN, Decreto 1080/2015, Ley 1755/2015). .NET 10 / Blazor Server /
PostgreSQL, Clean Architecture. 17 modulos (RQ01-RQ17) sobre una plataforma multi-tenant.

## Arrancar en una maquina nueva

Guia completa (clonar, infra Docker, migraciones, sembrar usuario, correr): **[`docs/ONBOARDING.md`](docs/ONBOARDING.md)**.

Resumen rapido:

```bash
git clone https://github.com/alexandercuartas665/TronoxDev.git
cd TronoxDev && git checkout desarrollo
# 1) config local (ver ONBOARDING): deploy/docker/.env  y  appsettings.Development.local.json
# 2) infra:      cd deploy/docker && ./preflight.ps1 && docker compose --env-file .env up -d
# 3) esquema:    dotnet ef database update (con TRONOX_DB_CONNECTION apuntando a localhost:5443)
# 4) correr:     dotnet run --project apps/backend/src/Tronox.Web --launch-profile tronox-dev
```

App en http://localhost:8095.

## Mapa de documentacion

| Archivo | Para que |
|---|---|
| [`docs/ONBOARDING.md`](docs/ONBOARDING.md) | Poner el proyecto a correr desde cero (otro PC / nuevo dev) |
| [`CLAUDE.md`](CLAUDE.md) | Contrato de desarrollo: los 10 invariantes, stack, convenciones, puertos. **Leer antes de tocar codigo** |
| [`PROGRESO.md`](PROGRESO.md) | Bitacora de avance: que hay hecho, decisiones, deuda tecnica |
| [`docs/decisiones/`](docs/decisiones/) | ADRs del repo |
| [`deploy/docker/README.md`](deploy/docker/README.md) | Detalle de la infraestructura local (puertos, servicios) |

> El repositorio es **PUBLICO**: ninguna credencial, cadena de conexion ni secreto entra en el.
> Los archivos con claves (`deploy/docker/.env`, `appsettings.Development.local.json`) estan
> gitignored; se crean desde sus plantillas `.example`.
