# ADR-29. Gerencia ve todos los pisos; solo el Operador tiene pisos asignados

**Estado:** Aceptada · **Fecha:** 16/11/2026 · **Precisa:** RN-24

## Contexto

RN-24 decía que el Administrador ve todos los pisos y que Operador y Gerencia ven solo los asignados. Para
Gerencia no tiene sentido: su trabajo es mirar el conjunto (indicadores, historial, comparar pisos) y no puede
modificar nada. Pedirle al Administrador que le asigne cada piso nuevo es trabajo de más y, si se olvida, Gerencia
no ve un piso que existe.

## Decisión

1. **Administrador y Gerencia ven todos los pisos publicados; el Operador, solo los que se le asignan.** La regla
   sigue en un solo lugar (`AccesoPisos.VeTodosLosPisos` y `PuedeVerPiso`, incluida la versión del hub SignalR).
2. **Gerencia es de solo lectura.** Ver todos los pisos no le da acciones: cada acción conserva su propio permiso
   (por ejemplo, alertas y poner una zona fuera de servicio son del Operador y del Administrador).
3. **Solo el Operador guarda pisos** y necesita al menos uno (`RN24_PISO_REQUERIDO`). Al crear o editar un
   Gerente o un Administrador no se guarda ninguno; si alguien pasa de Operador a Gerencia, sus pisos se dan de
   baja (RN-28), no se borran.

## Consecuencias

- ✅ Un piso nuevo aparece solo en Gerencia y Administrador; solo el Operador requiere asignación.
- ✅ La pantalla Usuarios muestra el selector de pisos únicamente para el Operador.
- ⚠️ El texto de RN-24 vive en el documento de requisitos (fuera del repositorio): se actualiza en el cambio de
  versión v1.4, junto con RF09.
