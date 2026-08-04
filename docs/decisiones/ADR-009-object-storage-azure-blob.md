# ADR-009 - Object storage = Azure Blob Storage (no S3/MinIO)

Fecha: 2026-08-04
Estado: Aceptado
Contexto: RQ04 - Gestion Integral de Documentos (binarios de documentos). Invariante #9 (binarios en
object storage, nunca BLOB en base de datos).

## Contexto

CLAUDE.md fija el stack de object storage como "S3 (MinIO en local)" y el `docker-compose.yml` de
desarrollo ya levanta un contenedor `tronox-minio`. Al empezar la migracion del modulo de Documentos
(RQ04) hubo que elegir el proveedor de almacenamiento de binarios del sistema nuevo.

El sistema legacy (VB.NET) guarda los binarios de documentos en **Azure Blob Storage**
(`Funciones.AzureBlobStorage`, cuenta `AZUREBLOBSTORAGE`), referenciados por GUID en
`EXP_DOCUMENTOS.RUTA_ALMACENAMIENTO`, con un registro global en `GEN_ARCHIVOS`.

## Decision

El object storage de TRONOX sera **Azure Blob Storage** (paquete `Azure.Storage.Blobs`), NO S3/MinIO.
Decidido por el usuario (2026-08-04) por paridad con el legacy y la probable cuenta Azure en produccion.

Se implementa detras de una abstraccion `IObjectStorage` (en Application/Common) para que el proveedor
sea intercambiable: si en el futuro se vuelve a S3/MinIO, se agrega otra implementacion sin tocar los
casos de uso.

- **Local (dev):** emulador **Azurite** (imagen oficial de Microsoft
  `mcr.microsoft.com/azure-storage/azurite`) en el bloque de puertos dedicado de TRONOX. Connection
  string de desarrollo (`UseDevelopmentStorage=true` apuntando a Azurite), en `.env` / configuracion,
  NUNCA en el repo.
- **Produccion:** cuenta Azure Storage real; connection string cifrada fuera del repo (`/opt/tronox/.env`
  con permisos 600, igual que el resto de secretos).
- **Modelo de datos:** el documento guarda la KEY del blob (GUID) en `documentos.ruta_almacenamiento`,
  mas `hash_sha256` (integridad), `tamano_bytes`, `formato` y `tiene_binario`. El binario jamas entra a
  la base de datos (invariante #9 intacto: Azure Blob ES object storage).

## Consecuencias

- **Contradice** el stack documentado en CLAUDE.md ("Object storage S3 (MinIO en local)"). Se registra
  aqui y se avisa para actualizar el vault Obsidian y CLAUDE.md (el stack pasa a listar Azure Blob).
- El contenedor `tronox-minio` del compose queda sin uso por ahora; se conserva (no se rompe nada) y
  se puede retirar en una limpieza posterior, o reutilizar si se agrega una implementacion S3 de
  `IObjectStorage`.
- Se agrega el contenedor `tronox-azurite` al compose (puertos del bloque TRONOX) y el paquete
  `Azure.Storage.Blobs` a Tronox.Infrastructure.
- La abstraccion `IObjectStorage` mantiene los casos de uso (Application) agnosticos del proveedor.

## Alternativas descartadas

- **Usar el MinIO ya provisionado (S3, AWSSDK.S3):** cero contenedor nuevo y alineado con CLAUDE.md,
  pero se aparta del legacy y de la infraestructura Azure probable de produccion. Descartado por
  decision explicita del usuario.
- **Mantener ambos y elegir por config:** sobre-ingenieria para el estado actual; basta la abstraccion
  `IObjectStorage` con una implementacion Azure. Una implementacion S3 se agrega si hace falta.
