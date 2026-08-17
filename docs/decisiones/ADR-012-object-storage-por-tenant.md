# ADR-012 - Object storage configurable por entidad (Azure Blob por tenant)

Fecha: 2026-08-13
Estado: Aceptado
Contexto: RQ01 (Datos de la Entidad) + RQ04 (binarios de documentos). Extiende ADR-009.

## Contexto

ADR-009 fijo Azure Blob Storage como object storage de TRONOX, configurado de forma GLOBAL por
`ObjectStorage__ConnectionString` (env/.env), con Azurite en local. Al avanzar en la migracion de
Expedientes/Documentos, el usuario pidio (2026-08-13) que **cada entidad** pueda apuntar a su propia
cuenta de Azure Blob, y que esa configuracion viva en **Datos de la Entidad** (no en variables de
entorno de plataforma). Motivos: cada entidad publica puede exigir residencia/propiedad de sus
binarios en su propia cuenta, y para migrar los documentos del legacy hay que apuntar a su cuenta.

## Decision

Se agrega `AlmacenamientoConfig` (una fila por tenant) con la cadena de conexion **cifrada AES-256**
(via `ISecretProtector`), contenedor y prefijo opcional. La configuracion se edita en Datos de la
Entidad (seccion "Almacenamiento de Documentos (Azure Blob)").

`AzureBlobObjectStorage` pasa de **singleton** a **scoped** y resuelve la cuenta del tenant de la
peticion: si hay `AlmacenamientoConfig` activo, usa su cadena/contenedor/prefijo (descifrando el
secreto); si no, **cae al proveedor global** de ADR-009 (Azurite/env). Asi el cambio es retro-compatible
y los procesos sin tenant (workers) siguen usando el global.

- La cadena de conexion NUNCA se devuelve en claro por la API/UI (solo se informa "Configurado").
- La prueba de conexion vive en `IBlobConnectionTester` (Infrastructure), no expone el secreto al dominio.
- Invariante #9 intacto (binarios en object storage, nunca en BD). Seccion 5 de CLAUDE.md: secreto
  por tenant cifrado en BD.

## Consecuencias

- Aislamiento real de binarios por entidad; base para migrar los documentos del legacy (que viven en
  su propia cuenta Azure Blob, referenciados por GUID en `EXP_DOCUMENTOS.RUTA_ALMACENAMIENTO`).
- `IObjectStorage` ahora es scoped: cualquier consumidor nuevo debe ser scoped o mas corto (ya lo son
  los servicios de aplicacion). Un consumidor singleton tendria que resolver un scope por operacion.
- Pendiente: cablear la carga/lectura real de los documentos del legacy usando esta config.
