---
type: ADR
status: accepted
date: 2026-07-21
---
# ADR-003: Estructura organizacional como arbol unico con clasificador

## Estatus

**Aceptado.** Contradice la especificacion vigente RQ01 -> RF03/RF04. **Requiere actualizar
el vault** antes de que la divergencia se consolide.

## Contexto

RQ01 especifica la estructura organizacional en dos requerimientos separados:

- **RF03 - Estructura Organica (Dependencias):** arbol jerarquico ilimitado con
  `dependencia_padre_id`, `fondo_id` obligatorio, vigencias (`vigente_desde` /
  `vigente_hasta`) y `dependencia_sucesora_id` para fusiones.
- **RF04 - Catalogo de Cargos:** catalogo **plano y global** por tenant. La spec es
  explicita en su regla de diseno: *"El cargo es un metadato organizacional, no un
  controlador de permisos. Es una lista maestra independiente, no amarrada a
  dependencias. La vinculacion cargo-dependencia ocurre en el Usuario (RF06)."*

El sistema hermano ECOREX resolvio el mismo problema con **un unico arbol `OrgUnit`** y un
clasificador por nodo (`Dependencia` / `Cargo` / `Funcionario`), sobre el que construyo la
asignacion por cargo y la expansion a candidatos (`OrgAssigneeTree`, `NodeAssigneeResolver`).

El PLAN DE ARRANQUE §2.2 dejo la eleccion explicitamente abierta como "decision pendiente
que bloquea el entregable 1.4".

## Decision

**Adoptar el modelo de arbol unico con clasificador de ECOREX.**

La estructura organizacional de TRONOX se modela como un solo arbol (`OrgUnit`, adjacency
list con `parent_id` autorreferencial) donde cada nodo declara su clasificador:

| Clasificador | Rol en el modelo |
|---|---|
| `Dependencia` | Unidad productora documental. Es la que cuelga de un fondo y la que hereda el principio de procedencia. |
| `Cargo` | Puesto. Puede colgar de una dependencia. |
| `Funcionario` | Ocupante de un cargo. |

Se conservan del backbone, sin discusion, los comportamientos ya probados:

1. `ON DELETE RESTRICT` en la FK autorreferencial: una unidad con hijos no se borra en cascada.
2. **Archivado en vez de borrado** (`is_archived`); archivar exige no tener sub-dependencias activas.
3. **Validacion de ciclos fail-closed**: camina la cadena de ancestros con un set de visitados,
   de modo que un arbol ya corrupto se reporta como ciclo en vez de colgar el listado.
4. **Raices tolerantes a huerfanos**: se trata como raiz la unidad sin padre *o* con padre fuera
   del conjunto visible, para que ninguna unidad visible quede invisible.
5. **Asignacion por cargo, no por persona**: si un funcionario cambia de puesto, se re-liga la
   persona al cargo sin tocar los flujos.
6. Sincronizacion responsable <-> miembro en una sola transaccion.

**Correccion obligatoria respecto de ECOREX:** su modelo arrastra `Kind` (Area/Team) *y*
`Classifier` superpuestos sobre el mismo `parent_id`, deuda tecnica que el propio ECOREX
declaro. TRONOX adopta **una sola** clasificacion desde el inicio; `Kind` no se replica.

## Justificacion

- Sostiene directamente el `ResponsableResolver` de RQ11 (observacion M07) y el rol
  `Lider de Dependencia` (DAT-05), que necesitan resolver "quien ocupa este cargo en esta
  dependencia" en tiempo real.
- Reutiliza codigo ya probado en produccion en un sistema hermano, con sus errores corregidos.
- Evita mantener dos jerarquias paralelas cuando el organigrama real es una sola.

## Consecuencias

- **Negativa principal:** contradice RF03/RF04 tal como estan escritos hoy. Mientras el vault
  no se actualice, codigo y spec divergen, que es exactamente el riesgo que este ADR debe
  hacer visible.
- El cargo deja de ser un catalogo plano global. La regla de RF04 de que **el cargo no controla
  permisos** SE MANTIENE INTACTA: los permisos siguen viniendo solo de la matriz de RF05. El
  cargo sigue siendo metadato documental; lo unico que cambia es donde vive.
- La UI debe dejar claro al administrador que esta editando un solo arbol con nodos de distinto
  tipo, no dos catalogos.
- Si mas adelante se quisiera volver a catalogos separados, la vuelta atras es costosa: habria
  que partir el arbol y remapear las referencias de usuarios.

## Addendum (2026-07-22): como se ancla el Usuario al arbol

La decision de arriba dejo un hueco que RF06 no cubre, porque fue escrito asumiendo catalogos
separados (`dependencia_id` + `cargo_id`). Resolucion aprobada:

**El usuario apunta a UN solo nodo del arbol: su Cargo.** La dependencia NO se almacena en el
usuario: se deriva subiendo por la cadena de padres hasta el primer nodo con clasificador
`Dependencia`.

Consecuencias que hay que sostener en la implementacion:

1. La **Capa 2 de permisos** (visibilidad por area documental, RQ01 seccion 3) se resuelve
   caminando el arbol, no leyendo un campo. Esa resolucion debe ser **logica pura y cacheable**,
   no una consulta por cada verificacion de acceso.
2. Mover un nodo Cargo de una dependencia a otra **cambia la visibilidad documental de todos
   sus ocupantes**, sin que nadie edite esos usuarios. Es potente y es peligroso: toda
   reubicacion de un Cargo debe quedar en pista de auditoria y avisar a quien la hace cuantos
   usuarios afecta.
3. Un usuario cuyo Cargo cuelgue directamente de la raiz (sin Dependencia por encima) no tiene
   area documental. Debe resolver **fail-closed**: sin visibilidad, no visibilidad total.
4. El **principio de procedencia** (RF03) exige que el expediente conserve la dependencia
   VIGENTE AL MOMENTO de producirlo. Como aqui la dependencia es derivada, el expediente debe
   copiar el `dependencia_id` resuelto en el instante de su creacion. No se recalcula nunca
   despues, igual que el `trd_detalle_id` (DAT-03).

El punto 4 es el que se olvida con este modelo: si el expediente derivara su dependencia en
tiempo de lectura, reorganizar el organigrama reescribiria la procedencia de expedientes
historicos, que es exactamente lo que la normativa archivistica prohibe.

## Accion pendiente sobre el vault

Actualizar en `OBSIDIAN.TRONOX`:

1. **RQ01 -> RF03 y RF04**: reescribir para reflejar el arbol unico con clasificador, y mover
   la regla "el cargo no controla permisos" a una nota explicita del modelo.
2. **PLAN DE ARRANQUE §2.2**: marcar la decision pendiente como resuelta, apuntando a este ADR.
3. **00 - INDICE**: registrar el cambio de version de RQ01.

## Referencias

- `RQ01_Configuracion_General_y_Organizacional_v3` - RF03, RF04, RF06
- `TRONOX_Observaciones_Tecnicas_v1` - M07 (resolver de responsables), DAT-05
- `PLAN DE ARRANQUE DESARROLLO` §2.2
- Codigo de referencia: `C:\desarrolloia\ecorex.tareas` - `OrgUnit.cs`, `OrgUnitMember.cs`,
  `Application/Organization/`
