# ADR-23. El aforo de una zona puede quedar «por definir»

**Estado:** Aceptada · **Fecha:** 15/11/2026 · **Modifica:** RN-13 (documento v2)

## Contexto

La RN-13 exigía que toda zona de Ingreso, Espera o Servicio tuviera aforo y umbrales. Como la clínica
todavía no los definió, la semilla traía números de ejemplo (6, 24, 3…) y el gemelo pintaba un
semáforo con un aforo que nadie aprobó: un «5/24, concentración baja» que parece un dato real y no lo es.

## Decisión

- En Ingreso, Espera y Servicio el aforo puede estar **por definir**: aforo y umbrales nulos, los tres
  juntos. Si se define, va completo (aforo ≥ 1 y umbrales 0 < media ≤ alta ≤ 100). Tránsito y Salida
  siguen sin aforo nunca.
- Con el aforo por definir, la zona muestra su ocupación sin semáforo (nivel `NINGUNO`); en el gemelo
  se lee «5/—». El Administrador lo configurará desde Construcción del gemelo (P8).
- «Sin datos» va antes que «por definir»: si el nodo de la zona no tiene señal, se muestra «Sin datos».
- La restricción `CK_Zona_AforoRN13` lo aplica también en la base. Sus `IS NOT NULL` son necesarios:
  un CHECK acepta la fila cuando la condición da `UNKNOWN`, y `NULL > 0` da `UNKNOWN`.
- En el gemelo, una zona sin nodo instalado muestra solo su nombre: todavía no se monitorea.

## Alternativas

| Alternativa | Por qué no |
|---|---|
| Dejar los aforos de ejemplo | Un semáforo con números no aprobados se lee como dato real |
| Aforo 0 como «sin definir» | Un número mágico; 0 de aforo dividiría y pintaría todo en rojo |

## Consecuencias

- ✅ El gemelo no muestra un semáforo inventado.
- ✅ Instalar un nodo en una zona nueva la hace aparecer con datos, sin tocar código.
- ⚠️ Hasta que se configure el aforo no hay alertas de aforo alto en esas zonas.
