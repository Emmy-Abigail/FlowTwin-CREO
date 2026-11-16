# ADR-27. Configuración por datos en vez de «Construcción del gemelo»; aforo donde el paciente permanece

**Estado:** Aceptada (revisada el 16/11/2026, punto 3) · **Fecha:** 16/11/2026 · **Reemplaza:** P8 Construcción del gemelo · **Precisa:** RN-13

## Contexto

P8 suponía un editor donde el Administrador dibuja el piso (muros, zonas) y ubica los nodos en el plano.
Desde que el gemelo usa una **maqueta 3D generada desde los planos CAD**, un dibujo hecho en el navegador
no se usaría. La investigación (Pozyx, Navigine, Fortinet, TEKTELIC, Mappedin) muestra que: la jerarquía
Sede → Piso la maneja el administrador en su portal; los mapas 3D desde CAD los produce un **servicio técnico**
y los entrega instalados; la capacidad se configura **por espacio**, con niveles (Occuspace, Spacewell, Kontakt.io).

Además, cada zona guardaba sus propios umbrales de semáforo, y los umbrales de las alertas y la hora del cierre
de jornada estaban fijos en `appsettings`, aunque la pantalla decía que los cambia el Administrador.

## Decisión

1. **Pantalla «Configuración»** (última opción del menú del Administrador), sin dibujo:
   - **Sedes** (nombre, dirección, letra única de sus códigos de atención) y **pisos** (nombre, nivel). Un piso
     nace en **borrador** y se **publica** cuando tiene zonas (RN-15).
   - **Zonas** como datos: nombre, tipo y aforo. El aforo se ve en la tabla y se cambia solo con ⋯ → Editar
     (una sola forma, sin cambios por accidente). Se dan de baja (RN-28), no si tienen nodos.
   - **Parámetros** en la base (`gem.ParametrosSistema`, una fila): alerta y alerta crítica de espera, minutos
     sin lecturas, hora del cierre de jornada y el ámbar del semáforo.
2. **La maqueta 3D no se gestiona aquí**: la instala el equipo técnico (como el servicio profesional de los
   sistemas investigados).
3. **Aforo en toda zona donde el paciente permanece** (RN-13 precisada); Tránsito y Salida nunca. Empieza
   «por definir» y lo carga el Administrador.
   - En **Ingreso y Espera** es cuántas personas caben (el aforo oficial).
   - En **Triaje y Consulta** es el **número de puestos de atención** (1 médico, 4 camillas): la capacidad del
     servicio. En teoría de colas, el cuello de botella es el servicio que trabaja más cerca de su capacidad;
     sin este número el gemelo no puede mostrar que un servicio está saturado.
   - Con el mismo semáforo, un consultorio de 1 puesto se lee solo: 0/1 verde (libre), 1/1 ámbar (sin
     espacio), 2/1 rojo (más pacientes que puestos). No hace falta un estado «libre/ocupado» aparte.
   - *Revisión:* la primera versión dejaba el aforo solo en Ingreso y Espera («en un consultorio un % no
     sirve»). Se corrigió porque ocultaba justo el cuello de botella que el sistema debe mostrar.
4. **Semáforo con umbral general**: verde por debajo del ámbar (80 % por defecto, editable), ámbar hasta el
   aforo y **rojo al superarlo**. El aforo que se carga es el oficial (en Perú, el del certificado ITSE), así
   que el rojo marca un límite real. El 80 % es criterio, no un estándar clínico: por eso es editable.
5. Se quitan los umbrales por zona (`UmbralMediaPct`, `UmbralAltaPct`).

## Alternativas

| Alternativa | Por qué no |
|---|---|
| Editor de planos en el navegador (P8) | El gemelo es una maqueta 3D desde CAD; un dibujo en la app no se usaría |
| Umbrales por zona | Más trabajo para el Administrador sin un beneficio claro; un valor general basta |
| Ámbar al 50 % (Google, salas de reuniones) | En una sala de espera estar a la mitad es lo normal: ruido visual |
| Parámetros en `appsettings` | Solo los cambia quien administra el servidor, no el Administrador |

## Consecuencias

- ✅ Agregar una sede, un piso o una zona no requiere programar.
- ✅ Los «valores por defecto» que mostraba la app ahora sí los cambia el Administrador.
- ⚠️ Las zonas nuevas guardan un rectángulo mínimo del plano 2D anterior, que el 3D no usa; esas columnas se
  quitan en la limpieza de muros y `planoUrl`.
- ✅ El gemelo muestra la saturación de consultorios, triaje y tópicos con la misma leyenda de siempre.
- ⚠️ Pendiente: que la maqueta de cada piso la entregue el servidor desde su carpeta (sin escribirla en el código).
