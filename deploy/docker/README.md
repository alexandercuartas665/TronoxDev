# Infraestructura local de TRONOX SGDEA

Pila Docker Compose para desarrollo local. **Motor de base de datos UNICO: PostgreSQL 16**
(no hay SQL Server ni DAL dual). Ademas: Redis (cache), RabbitMQ (colas), MinIO (object
storage S3) y Adminer (consola web de BD).

Proyecto compose `tronox`, red `tronox-net`, prefijo `tronox-` en contenedores y volumenes.
Los puertos son un **bloque DEDICADO** verificado libre contra los ~30 contenedores de los
stacks hermanos de la maquina. Estan parametrizados en `.env` (no versionado).

## Puertos asignados (host)

| Servicio | Puerto host | Contenedor | Acceso |
|----------|-------------|------------|--------|
| PostgreSQL 16 | **5443** | `tronox-postgres` | `Host=localhost;Port=5443;Database=tronox_dev;Username=tronox;Password=...` |
| Redis 7 | 6390 | `tronox-redis` | `localhost:6390` (con password) |
| RabbitMQ 3.13 (AMQP) | 5683 | `tronox-rabbitmq` | `amqp://tronox:...@localhost:5683` |
| RabbitMQ Management UI | 15683 | `tronox-rabbitmq` | http://localhost:15683 |
| MinIO (S3 API) | 9004 | `tronox-minio` | http://localhost:9004 |
| MinIO Console | 9005 | `tronox-minio` | http://localhost:9005 |
| Adminer | 8093 | `tronox-adminer` | http://localhost:8093 |

> Los puertos de la APP (API 8094, Web 8095) los usa `dotnet run`, no el compose: en desarrollo
> las apps corren FUERA de docker y se conectan a estos servicios por `localhost`.

## 1. Preparar el `.env` (una sola vez)

```powershell
cd deploy\docker
Copy-Item .env.example .env
# Edita .env y cambia las claves 'cambia-esta-clave' por valores propios.
# IMPORTANTE: el POSTGRES_PASSWORD del .env debe coincidir con el Password de
# apps/backend/src/Tronox.Web/appsettings.Development.local.json (ver docs/ONBOARDING.md).
```

El `.env` NO se versiona (repo publico). Si mueves un puerto, cambialo aqui y vuelve a correr
`preflight.ps1`; no hace falta tocar el `docker-compose.yml`.

## 2. Levantar la pila (preflight SIEMPRE primero)

```powershell
cd deploy\docker
.\preflight.ps1                         # valida docker vivo, puertos libres, sin choques con vecinos
docker compose --env-file .env up -d
docker compose ps                       # los 5 contenedores 'healthy'
```

> **Regla de convivencia:** la maquina corre ~30 contenedores de stacks hermanos. Compara
> `docker ps` antes y despues del `up`: el numero de contenedores vecinos NO debe bajar.

## 3. Operacion

```powershell
docker compose -p tronox ps                        # estado
docker compose -p tronox down                       # bajar (MANTIENE los datos en los volumenes)
docker compose -p tronox --env-file .env up -d      # volver a subir
docker compose -p tronox down -v                    # bajar y BORRAR datos (empezar de cero)
```

## 4. Validar conectividad

```powershell
docker exec tronox-postgres pg_isready -U tronox -d tronox_dev
docker exec tronox-redis redis-cli -a $env:REDIS_PASSWORD ping
docker exec tronox-rabbitmq rabbitmq-diagnostics ping
```

## Notas

- Las contrasenas reales viven en `deploy/docker/.env` (ignorado por git). La plantilla es `.env.example`.
- Los datos persisten en volumenes nombrados `tronox_postgres-data`, `tronox_redis-data`,
  `tronox_rabbitmq-data`, `tronox_minio-data`.
- Con la infra arriba, aplica migraciones y siembra un usuario: ver **`docs/ONBOARDING.md`** (raiz del repo).
