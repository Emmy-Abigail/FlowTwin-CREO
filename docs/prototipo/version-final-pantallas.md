# CREO FlowTwin: versión final de pantallas y flujos

Versión consolidada al 12/10/2026, con las decisiones de Emmy y las mejoras de [feedback-ux.md](feedback-ux.md). Aquí no hay diseño visual: solo **qué pantallas existen, quién las ve y cómo se pasa de una a otra**.

**Regla de Admisión (confirmada):** no hay ticket. En Admisión se entrega al paciente una **tarjeta BLE con su QR impreso**. Al escanearla empieza el recorrido y el sistema crea una atención con un ID anónimo (A-0143). Al devolverla y escanearla otra vez, el recorrido termina.

---

## 1. Resumen en números

- **11 pantallas en total**: 2 comunes, 5 de operación y 4 de administración.
- **6 ventanas emergentes** (modales y paneles) que viven dentro de esas pantallas.

| Rol | Pantallas que ve | Pantalla de inicio |
|---|---|---|
| **Operador** | 7 (2 comunes + 5) | Admisión |
| **Gerencia** | 5 (2 comunes + 3, en solo lectura) | Gemelo en vivo |
| **Administrador** | 9 (2 comunes + 3 de consulta + 4 de administración) | Gemelo en vivo |

Varias pantallas son **la misma para varios roles**, pero muestran u ocultan acciones según el rol. Por ejemplo, el Gemelo en vivo es uno solo: el Operador ve sus pisos asignados, y Gerencia y Admin ven todos.

---

## 2. Inventario de pantallas

### Comunes (todos los roles)
| # | Pantalla | Qué hace |
|---|---|---|
| P1 | **Iniciar sesión** | Correo y contraseña. Lleva a la pantalla de inicio según el rol. Se bloquea tras 5 intentos fallidos. |
| P2 | **Cambiar contraseña** | Obligatoria en el primer ingreso, porque el Admin entrega una contraseña temporal. También se abre desde el menú del usuario. |

### Operación
| # | Pantalla | Operador | Gerencia | Admin | Qué hace |
|---|---|:-:|:-:|:-:|---|
| P3 | **Admisión** | ✓ | — | — | Un solo campo para escanear la tarjeta. El sistema deduce si es entrega o devolución. Contadores de tarjetas: Disponibles · En uso · Por desinfectar · Sin devolver. Lista "Pendientes" y "Últimas operaciones". |
| P4 | **Gemelo en vivo** (dashboard) | ✓ sus pisos | ✓ todos | ✓ todos | 4 KPI arriba. Gemelo 3D/2D del piso con un ícono de persona por zona coloreado por concentración y su número (8/20), más la leyenda. Hover o clic sobre una zona muestra su detalle. Selector de piso (oculto si hay uno solo). Feed en vivo a la derecha con chips Activos · Atendidos · Todos. |
| P5 | **Historial y reportes** | ✓ sus pisos | ✓ lectura | ✓ | Filtros: fecha, piso, zona. Gráfico de aforo histórico por hora, mapa día × hora, tabla de atenciones y búsqueda por ID anónimo. Botones **Exportar CSV** y **Exportar PDF**. |
| P6 | **Detalle de atención** | ✓ | ✓ lectura | ✓ | Línea de tiempo de una atención (Admisión → Sala → Consultorio → Salida) con los minutos de cada tramo. Se llega desde el feed (P4) o desde el historial (P5). |
| P7 | **Seguimiento** | ✓ | — | — | Las zonas asignadas con interruptor **Activo / Pausado** y motivo (RF09). Debajo, el estado de cada nodo ESP32 de esas zonas: en línea o sin señal, y el último latido. |

### Administración (solo Admin)
| # | Pantalla | Qué hace |
|---|---|---|
| P8 | **Construcción del gemelo** | Árbol a la izquierda (Sede › Piso › Zonas y nodos), plano al centro con herramientas de dibujo, propiedades a la derecha y vista previa 3D. Botones **Guardar borrador** y **Publicar cambios**. |
| P9 | **Dispositivos** | Dos pestañas. **Nodos ESP32**: registrar nodo (nombre, MAC, zona) y ver su estado (en línea / sin señal, último latido). **Tarjetas BLE**: registrar tarjeta, estado y dar de baja. La calibración RSSI no está aquí: la hace el equipo técnico al instalar (ver diseno-sistema.md). |
| P10 | **Usuarios** | Tabla de usuarios: crear, editar, asignar pisos y zonas, restablecer contraseña y desactivar. |
| P11 | **Auditoría** | Bitácora de solo lectura: quién hizo qué, cuándo, y el antes y después. Con filtros y opción de exportar. |

### Ventanas emergentes (no cuentan como pantallas)
| # | Ventana | Dentro de |
|---|---|---|
| M1 | Confirmación de entrega o devolución (ID grande para leer en voz alta y resumen de tiempos) | P3 |
| M2 | Detalle de zona (al hacer clic en la zona del gemelo) | P4 |
| M3 | Nueva sede | P8 |
| M4 | Asistente "Nuevo piso" (4 pasos) | P8 |
| M5 | Crear / editar usuario | P10 |
| M6 | Pausar zona (con motivo) | P7 |

---

## 3. Menú de cada rol

```
OPERADOR                    GERENCIA                    ADMINISTRADOR
├─ Admisión        (inicio) ├─ Gemelo en vivo  (inicio) ├─ Gemelo en vivo        (inicio)
├─ Gemelo en vivo           └─ Historial y reportes     ├─ Historial y reportes
├─ Historial y reportes                                 ├─ Construcción del gemelo
└─ Seguimiento                                          ├─ Dispositivos
                                                        ├─ Usuarios
                                                        └─ Auditoría
```

El Detalle de atención (P6) no está en el menú: se abre desde el feed o desde el historial. Cambiar contraseña (P2) está en el menú del usuario, arriba a la derecha.

---

## 4. Mapa de navegación

```
                         P1 Iniciar sesión
                               │
                    ¿primer ingreso? ──sí──> P2 Cambiar contraseña
                               │
          ┌────────────────────┼────────────────────────┐
       Operador             Gerencia                   Admin
          │                    │                         │
     P3 Admisión          P4 Gemelo en vivo        P4 Gemelo en vivo
          │                    │                         │
     P4 Gemelo ──clic tarjeta del feed──> P6 Detalle de atención <──clic fila── P5 Historial
          │                                                                     
     P7 Seguimiento                                P8 Construcción ── P9 Dispositivos
                                                   P10 Usuarios ──── P11 Auditoría
```

---

## 5. Flujos principales

### F1. Inicio de sesión (todos)
1. P1: correo + contraseña.
2. Si es el primer ingreso → P2, cambia la contraseña temporal.
3. Entra a su pantalla de inicio según el rol.
4. Por inactividad (ej. 15 min) la sesión se cierra y vuelve a P1.

### F2. Entregar tarjeta: empieza el recorrido (Operador)
1. El paciente llega a Admisión. El recepcionista toma una tarjeta **Disponible**.
2. Escanea el QR de la tarjeta en P3.
3. El sistema ve que la tarjeta está Disponible → **crea la atención A-0143** → M1 muestra en grande "A-0143 · Tarjeta T-07 entregada", con sonido de éxito.
4. La tarjeta pasa a **En uso**. Aquí empieza a contar el **tiempo de espera**.
5. Cuando el ESP32 la detecta en la sala, aparece en el feed de P4 y suma en la zona del gemelo.

### F3. Devolver tarjeta: termina el recorrido (Operador)
1. El paciente sale y entrega la tarjeta en Admisión.
2. El recepcionista la escanea en P3.
3. El sistema ve que está **En uso** → cierra la atención → M1 muestra "A-0143 · Espera 23 min · Consulta 14 min · Total 41 min".
4. La tarjeta pasa a **Por desinfectar**.
5. Después de limpiarla con alcohol al 70%, el recepcionista pulsa **"Marcar desinfectada"** (o la escanea en modo desinfección) → vuelve a **Disponible**.

### F4. Casos de error en Admisión
| Se escanea… | El sistema responde |
|---|---|
| Una tarjeta **Por desinfectar** | "Esta tarjeta aún no está desinfectada", con el botón "Marcar desinfectada". No se entrega. |
| Una tarjeta **Extraviada** que apareció | "Tarjeta recuperada" → pasa a Por desinfectar. |
| Un código que no existe | "Tarjeta no registrada. Pide al Administrador que la registre." |
| La misma tarjeta dos veces seguidas por error | "Deshacer" disponible durante 30 s. |

### F5. Tarjeta no devuelta (automático)
1. Si una tarjeta En uso no se detecta durante X min (configurable), la atención **se cierra sola** con el motivo "sin lecturas".
2. La tarjeta aparece en P3 › **Pendientes de devolución** y en la campana de alertas.
3. El Operador puede **"Marcar extraviada"**. Si luego aparece, se escanea y pasa a Por desinfectar (F4).

### F6. Monitorear en vivo (Operador, Gerencia, Admin)
1. P4: ve los KPI y el gemelo del piso. Si tiene varios pisos, elige uno en el selector.
2. Pasa el cursor sobre una zona → "Sala de espera · 8/20 pacientes · espera prom. 23 min".
3. Hace clic en la zona → M2 con las atenciones de esa zona y el estado de su nodo.
4. En el feed filtra **Activos / Atendidos**. Al hacer clic en una tarjeta va a P6 con esa atención abierta, y el botón "Volver al gemelo" lo regresa.

### F7. Consultar y exportar un reporte (Operador, Gerencia, Admin)
1. P5: elige fecha, piso y zona.
2. Ve el aforo histórico por hora (cuándo hay más y menos gente), el mapa día × hora y la tabla de atenciones.
3. Busca una atención por ID anónimo → P6.
4. Pulsa **Exportar CSV** o **Exportar PDF** (respeta los filtros).

### F8. Pausar el seguimiento de una zona (Operador)
1. P7: apaga el interruptor de "Consultorio piloto" → M6 pide el motivo (limpieza, mantenimiento, sin atención).
2. En el gemelo esa zona se ve **gris, "seguimiento pausado"**, y esos minutos no cuentan en las métricas.
3. Queda registrado en la auditoría. Al reactivarla vuelve a medir.

### F9. Construir el gemelo (Admin)
1. P8 → **"+ Nueva sede"** (M3): nombre y dirección → "Clínica CREO+".
2. Selecciona la sede → **"+ Nuevo piso"** abre el asistente M4:
   - Paso 1: nombre y nivel ("Piso 1", nivel 1).
   - Paso 2: sube la imagen o PDF del plano.
   - Paso 3: fija la escala marcando dos puntos y escribiendo la medida real ("esta puerta mide 0.90 m").
   - Paso 4: queda en el editor, listo para dibujar.
3. Dibuja cada zona (rectángulo o polígono) y en propiedades le pone nombre, **tipo** (Ingreso, Espera, Servicio, Tránsito, Salida), **aforo** y **umbrales** (baja 0–50%, media 51–80%, alta 81–100%).
4. Coloca los nodos ya registrados (F10) sobre el plano y los asigna a su zona.
5. **Guardar borrador** mientras trabaja. Con **"Publicar cambios"** el piso aparece en el Gemelo en vivo, en los selectores y en los reportes. Queda en la auditoría.

### F10. Registrar un nodo ESP32 (Admin)
1. P9 › Nodos → **"+ Registrar nodo"**: nombre (N-04), dirección MAC y zona que cubre.
2. El nodo aparece en el plano de esa zona en P8 (se puede arrastrar para ubicarlo mejor).
3. El estado pasa a **En línea** cuando llega su primer latido; si deja de enviar, pasa a **Sin señal** y su zona se ve gris en el gemelo.
4. El umbral RSSI y la histéresis **no los toca el Admin**: vienen con valores por defecto y los ajusta el equipo técnico en la instalación.

### F11. Registrar una tarjeta BLE (Admin)
1. P9 › Tarjetas → **"+ Registrar tarjeta"**.
2. Escanea el QR impreso de la tarjeta y la acerca al lector. El sistema detecta su identificador BLE y los vincula.
3. La tarjeta queda **Disponible** para Admisión.
4. Si se daña o se pierde definitivamente: **Dar de baja**.

### F12. Crear un usuario (Admin)
1. P10 → **"+ Nuevo usuario"** (M5): nombre, correo y rol.
2. Si el rol es **Operador**, aparece el paso "Asignar pisos y zonas".
3. El sistema genera una **contraseña temporal** que el Admin entrega a la persona.
4. Esa persona entra (F1) y la cambia en P2.
5. Para dar de baja a alguien: **Desactivar**. No se borra, para no perder la auditoría.

### F13. Auditoría (automático + Admin)
Todo lo que cambia algo deja una línea: inicios de sesión, entregas y devoluciones, pausas de zona, cambios del gemelo, nodos, tarjetas y usuarios. El Admin la consulta en P11 con filtros por usuario, acción y fecha.

---

## 6. Estados de una tarjeta BLE

```
             registrar
                │
                v
  ┌──────> DISPONIBLE ──escanear en Admisión──> EN USO ──escanear al salir──> POR DESINFECTAR
  │                                               │                                │
  │                                     sin lecturas X min                "Marcar desinfectada"
  │                                               v                                │
  │                                         SIN DEVOLVER ──"Marcar extraviada"──> EXTRAVIADA
  │                                               │                                │
  │                                       aparece y se escanea ──────> POR DESINFECTAR
  └────────────────────────────────────────────────────────────────────────────────┘
  (cualquier estado) ──"Dar de baja"──> BAJA
```

## 7. Estados de una atención

```
ABIERTA (escaneo de entrega) ──> EN ESPERA ──> EN CONSULTA ──> (puede volver a ESPERA) ──> CERRADA
                                                                                            │
                                    motivo: devuelta · sin lecturas · cierre de jornada ────┘
```
