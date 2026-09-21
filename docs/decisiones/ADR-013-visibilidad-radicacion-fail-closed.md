# ADR-013: Visibilidad de radicados fail-closed (reconciliacion RF11-8 vs invariante 10)

- Estado: Aceptada
- Fecha: 2026-08-06
- Contexto: RQ09 (Radicacion) - port de la bandeja rad_bandeja y del detalle rad_detalle desde el
  legacy VB.NET.

## Contexto

El legacy (`RadicacionVisibilidadHelper`) acota que radicados ve cada usuario por un NIVEL guardado
en `RAD_PERMISOS_VISIBILIDAD`:

- **Propios**: los que radico / le asignaron / origino.
- **Dependencia**: los de su dependencia (subiendo el arbol organico) mas los propios.
- **Todos**: todos los de la sucursal.

Ese helper es **FAIL-OPEN**: si la tabla de permisos no existe, no hay fila, o la consulta falla,
degrada a **Todos** (ve todo). Ademas, en el legacy la visibilidad es la UNICA seguridad de datos:
no hay chequeo de permiso por modulo. Esto contradice el invariante 10 de TRONOX (fail-closed:
"un usuario sin rol, o una resolucion de permisos que falla, resuelve a SIN PERMISOS"), critico con
niveles Reservado/Clasificado.

## Decision

Se separan dos planos que el legacy mezclaba:

1. **Gate de acceso al modulo = el permiso** `Perm:modulo/radicacion:View` (y las acciones, sus
   propios permisos). Esto es fail-closed por politica de autorizacion (invariante 10): sin permiso,
   la pagina no carga. Los claims se leen del `AuthenticationState`, no del `IHttpContextAccessor`.

2. **Visibilidad RF11-8 = tightening ADITIVO** sobre ese gate, no un gate en si mismo:
   - **Sin fila** de visibilidad para el usuario -> **Todos DENTRO del tenant** (que ya esta aislado
     por el filtro global de EF sobre `ITenantScoped`, invariante 1). Es un default usable mientras
     no exista la pantalla de configuracion de visibilidad.
   - **Con fila** -> se aplica el nivel configurado (Propios / Dependencia / Todos).
   - **Error de resolucion** (excepcion) -> se cae al nivel MAS restrictivo (**Propios**), NUNCA a
     Todos. Aqui SI se invierte el fail-open del legacy.

La resolucion de la dependencia del usuario (CTE recursiva legacy sobre `DOC_ENTREVISTAS_ORG`) se
traduce a subir el arbol `OrgUnit` por `ParentId` desde el `CargoOrgUnitId` del `TenantUser` hasta el
nodo classifier `Dependencia` mas cercano. El funcionario se referencia por FK a `TenantUser` (no por
nombre de texto libre como el legacy).

## Justificacion

- Un fail-closed ESTRICTO en la visibilidad (sin fila -> no ve nada) dejaria la bandeja vacia para
  todos hasta construir la pantalla de configuracion de visibilidad, que aun no existe. Eso no aporta
  seguridad real: el gate verdadero (el permiso del modulo) ya es fail-closed.
- La regla innegociable del invariante 10 -que una resolucion que FALLA no otorgue acceso- se respeta:
  ante error, Propios; nunca Todos.
- El diseno deja lista la estructura (`RadicadoVisibilidadPermiso` + `RadicacionVisibilidadService`
  con la parte pura testeable) para endurecer el default a Propios/Dependencia cuando exista la
  configuracion por usuario, sin cambios de dominio.

## Consecuencias

- Con el permiso del modulo, un usuario ve por defecto todos los radicados de su tenant. Al configurar
  su nivel se restringe.
- Cuando se porte la administracion de visibilidad (o se decida por politica), se puede cambiar el
  default de "sin fila" de Todos a Propios en un solo punto (`RadicacionVisibilidadService`).
- No se replica el fail-open del legacy en ningun caso de error.

## Alternativas descartadas

- **Replicar el fail-open legacy** (sin fila / error -> Todos): descartado, contradice el invariante 10.
- **Fail-closed estricto (sin fila -> nada)**: correcto en teoria pero inutilizable sin la pantalla de
  configuracion; se pospone hasta que exista, documentando el punto unico de cambio.
