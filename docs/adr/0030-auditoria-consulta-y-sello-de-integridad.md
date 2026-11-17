# ADR-30. Auditoría: consulta por categorías, ingresos fallidos y sello de integridad

**Estado:** Aceptada · **Fecha:** 16/11/2026 · **Completa:** RN-27

## Contexto

La bitácora (`aud.Auditoria`) ya registraba quién, cuándo, qué y el antes y después de cada cambio, pero no se podía
consultar (P11). Para uso real en la clínica faltaban además tres cosas:

- **Los ingresos fallidos no quedaban registrados.** OWASP (Logging Cheat Sheet) los considera esenciales: revelan
  intentos de adivinar contraseñas.
- **Nada probaba que la bitácora no se hubiera alterado.** La aplicación solo inserta, pero quien tenga acceso a SQL
  Server podría editar o borrar filas. OWASP recomienda validar la integridad de cada entrada con una función hash
  criptográfica. HIPAA 164.312(b) pide registrar y *examinar* la actividad.
- Operación (entregas y devoluciones de tarjetas, pausas) es casi todo el volumen y tapaba los cambios importantes.

## Decisión

1. **P11** (solo Administrador): período de días de Lima (máximo 366), usuario, categoría y texto; paginada en el
   servidor; detalle con el antes y el después campo por campo; exportación CSV del filtro (máximo 10 000 filas,
   queda registrada en la propia bitácora).
2. **Categorías:** Accesos, Usuarios, Configuración, Dispositivos y Operación. La regla existe en C# (para rotular) y
   como expresión para SQL (para filtrar); una prueba comprueba que coinciden para cada acción y entidad.
3. **Ingresos fallidos:** `INICIO_FALLIDO` (con el motivo: correo inexistente, contraseña incorrecta, cuenta
   bloqueada o temporal vencida) y `CUENTA_BLOQUEADA`. El motivo solo queda en la bitácora: a quien intenta entrar se
   le responde el mismo mensaje genérico. El límite de ritmo de `auth` impide llenar la tabla.
4. **Sello de integridad:** `aud.Sello` guarda, por fila, un HMAC-SHA256 del contenido de la fila más el sello
   anterior (una cadena). La clave **no está en la base**: `Auditoria:ClaveSello` en user-secrets o variable de
   entorno (base64, mínimo 32 bytes). Una tarea sella cada 30 s, en orden de Id, solo filas con más de 60 s (para
   no saltarse una transacción que se confirma tarde). Es una tabla aparte y solo de inserción: la bitácora sigue
   siendo «solo se inserta». «Verificar integridad» recalcula la cadena del período y señala la primera fila alterada.
5. **Nombres de quien actuó:** contrato `INombresDeUsuarios` declarado por Auditoría e implementado por Identidad
   (como `INodosDeZonas`): Auditoría no lee tablas de otro módulo.

## Qué detecta y qué no

| Detecta | No detecta |
|---|---|
| Una fila sellada editada en SQL | Lo que alguien con la **clave** y acceso a SQL reescriba entero (por eso la clave va aparte de la base) |
| Una fila sellada borrada (rompe la cadena siguiente) | Filas borradas al final de la cadena si también se borran sus sellos: se nota porque el último sello es más viejo de lo normal |
| Filas insertadas por fuera de la aplicación (quedan «sin sello») | Cambios en los 60 s previos al sellado |

## Retención

La aplicación **no borra** la bitácora. HIPAA no fija un plazo para estos registros (la práctica común es conservar
6 años, por la norma de documentación); la clínica debe fijar el suyo según la normativa peruana de historias
clínicas y datos personales (Ley 29733). Mientras tanto, se conserva todo y entra en los respaldos.

## Consecuencias

- ✅ El Administrador responde «¿quién cambió esto y cuándo?» y entrega un CSV a un auditor.
- ✅ Una alteración directa en la base se puede demostrar.
- ⚠️ **Producción:** configurar `Auditoria__ClaveSello` (`openssl rand -base64 48`), guardarla fuera del servidor de
  base de datos y en un lugar seguro: si se pierde, la cadena existente no se puede verificar. Sin clave, la API
  arranca, avisa en el registro y la pantalla dice «Sello no configurado».
