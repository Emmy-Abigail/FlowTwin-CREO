# ADR-31. La Simulación cubre todo el sistema, para presentarlo como si estuviera en uso

**Estado:** Aceptada · **Fecha:** 17/11/2026 · **Modifica:** ADR-22 · **Retiro previsto:** el mismo del ADR-22

## Contexto

Con el ADR-22 solo el Gemelo, el Historial y las alertas se simulaban; Admisión, Dispositivos, Auditoría,
Usuarios y Configuración seguían mostrando datos reales. Al presentarle el sistema al patrocinador, esas
pantallas quedaban quietas o vacías mientras el gemelo se movía, y la clínica simulada arrancaba a las
10:30 ya llena (33 pacientes de golpe) o tardaba ~17 s en mostrar el primer paciente.

La Simulación es una herramienta de presentación: no va a la clínica (`Demo:SimulacionDisponible` es falso
en producción) y la usan tanto el Administrador como el Operador para mostrar su trabajo.

## Decisión

- **Al activar la Simulación, todo el sistema funciona sobre la clínica simulada**, en memoria:
  - Admisión: escanear, deshacer, desinfectar, extraviar y recuperar, sobre tarjetas ficticias (escanear
    «T-05» hace aparecer a ese paciente en el gemelo).
  - Dispositivos: nodos y tarjetas simulados; registrar, cambiar de zona, regenerar y dar de baja (la clave
    que se muestra es ficticia).
  - Auditoría: la bitácora simulada, con filtros, detalle «antes y después» y CSV. «Verificar integridad»
    avisa que la bitácora simulada no lleva sello.
  - Usuarios: el personal ficticio, con las reglas del servidor (correo único, pisos del Operador, un último
    Administrador). Las contraseñas temporales son ficticias.
  - Configuración: sede, pisos, zonas y parámetros ficticios. Cambiar un aforo o el ámbar del semáforo se ve
    al instante en el gemelo simulado.
  - Historial: abre en «Hoy», para que se vea moverse.
- **Cada pantalla conserva su código real**: elige la fuente según el modo (la API o un módulo
  `simulacion-*.ts` que responde con el mismo formato que la API). Ninguna pantalla se duplica.
- **Arranque inmediato y gradual:** la clínica simulada abre a las 07:00 vacía; el primer minuto corre al
  instante y la fila de 4 pacientes que espera en la puerta entra en los primeros minutos. Luego llegan según
  la hora (1 minuto simulado cada 5 s). 1 de cada 10 pacientes tiene una espera larga, así las alertas
  aparecen solas.
- **«Reiniciar simulación»** (en la franja) vuelve todo a las 07:00: pacientes, tarjetas, usuarios,
  configuración y bitácora. Salir y volver a entrar retoma donde quedó.
- **Los colores siguen la regla real** (RN-13): ámbar desde el 80 % del aforo, rojo con más pacientes que el
  aforo.
- Sigue valiendo lo del ADR-22: franja y marco violetas siempre visibles, nada se escribe en la base y al
  recargar se vuelve a los datos reales.

## Alternativas

| Alternativa | Por qué no |
|---|---|
| Mantener Admisión y Administración reales durante la Simulación | El patrocinador ve pantallas quietas; en la demo no hay datos reales que mostrar |
| Simular en el backend con una base de demostración | Los datos ficticios tendrían que vivir en una base; con la Simulación en el navegador nada llega a ninguna |
| Una aplicación aparte para la demo | Dos versiones que mantener y que se desalinean |

## Consecuencias

- ✅ El patrocinador puede usar cualquier pantalla, con cualquier rol, como si la clínica estuviera operando.
- ✅ Nada de lo hecho en la Simulación llega a la base ni a los reportes reales.
- ⚠️ Más código que retirar el día que se quite la Simulación: los `simulacion-*.ts`, `usuarios-simulados.ts`,
  `configuracion-simulada.ts` y la rama simulada de cada pantalla. Cada uno está aislado para que el retiro sea
  borrar archivos y esas ramas.
- ⚠️ Cada pantalla nueva debe ofrecer su versión simulada.
