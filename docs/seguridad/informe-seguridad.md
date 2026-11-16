# Informe de seguridad · CREO FlowTwin

**Fecha:** 16/11/2026 · **Versión revisada:** rama `fix/auditoria-dia` (backend .NET 10, frontend Angular 22.2.6)
**Referencia:** OWASP ASVS 4.0.3, nivel 1 · **Alcance:** API, frontend y sus dependencias. El despliegue (HTTPS,
TLS de MQTT, respaldos) se revisa al desplegar: ver «Pendientes».

Ningún sistema es «100 % seguro». Este informe dice **qué se probó, con qué herramienta, qué salió y qué queda
abierto**, para que cualquiera pueda repetirlo y verificarlo.

## 1. Resumen

| Prueba | Herramienta | Resultado |
|---|---|---|
| Ataques automáticos a toda la API, con sesión de Administrador | OWASP ZAP 2.17.0 (API scan) | **0 fallos, 0 advertencias, 120 reglas aprobadas**, 213 URL, 11 226 peticiones, **ningún error 500** |
| Avalancha de peticiones de un usuario abusivo | k6 2.4.0 | 1 025 927 peticiones en 30 s: el abusador recibió 429 en el 99,97 %; **el usuario normal fue atendido el 100 % de las veces, p95 = 9,9 ms**; la API siguió en pie |
| Adivinar contraseñas a toda velocidad | k6 2.4.0 | de ~174 000 intentos, **solo 18 llegaron a comprobarse** (límite de 20 por minuto); todos quedaron en la bitácora |
| Dependencias con vulnerabilidades conocidas | `dotnet list package --vulnerable`, `npm audit` | **0** en .NET; **0** en npm (se actualizó Angular a 22.2.6 / herramientas 22.2.7 para cerrar 2 que afectaban solo al servidor de desarrollo) |
| Pruebas automáticas | xUnit, Vitest | 223 unitarias, 18 de arquitectura, 119 de integración y 195 del frontend, en verde |

**Lo que falta** (sección 4): la regla de contraseñas no cumple ASVS; HTTPS, TLS de MQTT y respaldos dependen del despliegue.

## 2. Controles, capítulo por capítulo (ASVS 4.0.3, nivel 1)

✅ cumple · ⚠️ cumple en parte · ❌ no cumple · ⏳ depende del despliegue

### V2 · Autenticación
| Control | Estado | Evidencia |
|---|---|---|
| Contraseñas guardadas con una función lenta y con sal | ✅ | PBKDF2-HMAC-SHA512, 100 000 iteraciones, sal aleatoria (`PasswordHasher`, ModuloIdentidad) |
| Longitud mínima de 12 caracteres | ❌ | Hoy son 8 (`PoliticaContrasena.LongitudMinima`). Ver pendiente 4.1 |
| Sin reglas de composición obligatoria | ❌ | Hoy se exige mayúscula, minúscula, número y símbolo. ASVS y NIST 800-63B lo desaconsejan |
| Comprobar contra contraseñas filtradas | ❌ | No se hace. Ver pendiente 4.1 |
| Frenar ataques automáticos | ✅ | Bloqueo de 15 min tras 5 intentos; 20 intentos por minuto por IP; prueba k6 (sección 1) |
| Mensaje genérico ante un fallo | ✅ | Siempre «correo o contraseña incorrectos»; el motivo solo va a la bitácora (ADR-30) |
| Contraseña temporal de un solo uso que obliga a cambiarla | ✅ | 12 caracteres aleatorios (`RandomNumberGenerator`), vence en 24 h, cambio obligatorio (F-02) |
| No repetir las últimas contraseñas | ✅ | Las 3 últimas (`Contrasenas:HistorialNoRepetir`) |

### V3 · Sesiones
| Control | Estado | Evidencia |
|---|---|---|
| Token de sesión fuera del alcance de scripts | ✅ | El de refresco va en cookie `HttpOnly`, `Secure`, `SameSite=Strict`, solo a `/api/v1/auth`; el de acceso vive en memoria (15 min) |
| Cierre de sesión que invalida en el servidor | ✅ | `/auth/logout` revoca el token de refresco; desactivar o restablecer revoca todas las sesiones |
| Rotación del token de refresco | ✅ | Cada renovación revoca el anterior (F-03) |
| Expiración por inactividad y absoluta | ✅ | 30 min sin uso y 12 h como máximo |
| Token en la URL | ⚠️ | Solo para el WebSocket de `/hubs` (el navegador no permite cabeceras ahí); el registro de peticiones no guarda la consulta |

### V4 · Control de acceso
| Control | Estado | Evidencia |
|---|---|---|
| Permisos comprobados en el servidor, en cada endpoint | ✅ | Políticas por rol; ZAP con sesión de Administrador recibió 494 respuestas 403 en endpoints que no son de su rol |
| Acceso por piso o sede (no ver datos ajenos) | ✅ | RN-24 en un solo lugar (`AccesoPisos`), incluido el hub; pruebas de integración |
| Gerencia de solo lectura | ✅ | ADR-29; prueba: ve los pisos pero recibe 403 en acciones del Operador |
| Último Administrador y autodesactivación | ✅ | RN-23 (`RN23_ULTIMO_ADMIN`, `RN23_AUTODESACTIVACION`) |

### V5 · Validación y codificación
| Control | Estado | Evidencia |
|---|---|---|
| Validación de entradas en el servidor | ✅ | FluentValidation y reglas de dominio; ZAP: 9 530 respuestas 400 a sus ataques |
| Inyección SQL | ✅ | EF Core parametriza; búsquedas con `LIKE` escapado; ZAP: reglas de inyección SQL aprobadas |
| XSS | ✅ | Angular escapa todo lo que muestra; la API responde JSON con `CSP: default-src 'none'` |
| Inyección en CSV | ✅ | `CsvSeguro.Campo` antepone `'` a `= + - @` (Historial y Auditoría) |
| Inyección en registros | ✅ | La búsqueda de la bitácora quita caracteres de control; Serilog escribe propiedades estructuradas |

### V7 · Errores y registros
| Control | Estado | Evidencia |
|---|---|---|
| Los errores no revelan detalles internos | ✅ | `ManejadorErrores`: mensaje genérico + código técnico; ZAP: ninguna fuga de información, ningún 500 |
| Registro de éxitos y fallos de autenticación | ✅ | `INICIAR_SESION`, `INICIO_FALLIDO` (con motivo) y `CUENTA_BLOQUEADA` (ADR-30) |
| Registros sin secretos | ✅ | Contraseñas, claves y tokens nunca se escriben; el token del hub no queda en la ruta registrada |
| Integridad de la bitácora | ✅ | Cadena de sellos HMAC-SHA256 con clave fuera de la base y «Verificar integridad» (ADR-30); prueba que edita una fila en SQL y la detecta |

### V8 · Protección de datos
| Control | Estado | Evidencia |
|---|---|---|
| Datos personales de pacientes | ✅ | No hay: cada paciente es un código anónimo (Ley 29733) |
| Respuestas sin caché | ✅ | `Cache-Control: no-store` en la API (las maquetas usan ETag y `private`) |

### V9 · Comunicaciones
| Control | Estado | Evidencia |
|---|---|---|
| HTTPS en todo el tráfico | ⏳ | Lo pone el despliegue (Render en la demo; Caddy en el servidor de la clínica). HSTS ya activo fuera de desarrollo |
| TLS entre los nodos y el broker | ⏳ | Puerto 8883 con certificado propio, al desplegar en la clínica (el firmware lleva la CA) |

### V10 · Código malicioso y dependencias
| Control | Estado | Evidencia |
|---|---|---|
| Dependencias sin vulnerabilidades conocidas | ✅ | Sección 1 (16/11/2026). Ver pendiente 4.4 para que se revise solo |
| Versiones fijas | ✅ | `package-lock.json`, `global.json`, imágenes Docker con versión |

### V12 · Archivos
| Control | Estado | Evidencia |
|---|---|---|
| Sin subida de archivos | ✅ | La API no recibe archivos; cuerpo máximo 1 MB (413 si se pasa, probado en Kestrel real) |
| Descarga sin acceso a rutas arbitrarias | ✅ | Maquetas con nombres fijos e id entero (ADR-28) |

### V13 · API
| Control | Estado | Evidencia |
|---|---|---|
| Límite de peticiones en toda la API | ✅ | Por usuario (o IP sin sesión), ráfaga de 100 y 300 por minuto; prueba k6 |
| Sin CORS abierto | ✅ | No se habilita CORS: web y API en el mismo origen |

### V14 · Configuración
| Control | Estado | Evidencia |
|---|---|---|
| Secretos fuera del código | ✅ | user-secrets en desarrollo, variables de entorno en el servidor |
| Sin secretos, no arranca (fuera de desarrollo) | ✅ | `Proteccion.ExigirSecretos`: conexión, sello de auditoría y MQTT; JWT ya lo exigía |
| Cabeceras de seguridad | ✅ | `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`, COOP, CORP, CSP, HSTS |
| El servidor no se anuncia | ✅ | Sin cabecera `Server` (comprobado contra Kestrel real) |
| Documentación de la API solo en desarrollo | ✅ | `/scalar` y `/openapi` no existen en producción |

## 3. Qué se probó exactamente

- **ZAP:** `tools/seguridad/zap.sh`, contra una API en Development sobre una **base desechable**
  (`FlowTwin_Seguridad`), con sesión de Administrador y límites altos para que el escáner llegue a todo. Primera
  pasada: 1 advertencia (`Cross-Origin-Resource-Policy`); se agregó la cabecera y la segunda pasada quedó limpia.
- **k6:** `tools/seguridad/avalancha.js`, contra una API con sus **límites por defecto**, durante 30 s: 50 conexiones
  de un usuario abusivo, 10 de alguien sin cuenta probando contraseñas y un usuario normal pidiendo una pantalla por
  segundo. Umbrales: el usuario normal atendido el 100 % y p95 < 500 ms. Ambos se cumplieron.

## 4. Pendientes

1. **Contraseñas (❌, decisión de negocio).** Propuesta: mínimo 12 caracteres, sin composición obligatoria (frases
   largas son más seguras y más fáciles de recordar) y rechazar contraseñas conocidas por filtraciones (lista local,
   sin enviar nada a internet). Afecta a los usuarios cuando cambien su contraseña, no antes.
2. **HTTPS y TLS (⏳).** Se configuran y prueban en el despliegue (demo en Render; luego la nube de la clínica).
3. **Respaldos (⏳).** Respaldo diario, copia fuera del servidor y prueba de restauración, al desplegar.
4. **Revisión continua.** Agregar a la CI `dotnet list package --vulnerable` y `npm audit --omit=dev`, para que una
   vulnerabilidad nueva en una dependencia se note en el siguiente pull request.
5. **Ataques masivos distribuidos (DDoS).** Se frenan en la plataforma (Render, la nube de la clínica), no en el
   código. Lo que sí le toca al código —que un usuario o un bot no sature la API— está probado.
6. **Segundo factor (MFA).** ASVS nivel 1 no lo exige; es recomendable para el Administrador si el sistema se expone a
   internet de forma permanente.

## 5. Cómo repetir

```bash
# Dependencias
cd backend && dotnet list FlowTwin.slnx package --vulnerable --include-transitive
cd frontend/flowtwin-web && npm audit --omit=dev

# Carga (API con sus límites por defecto, usuarios de demo)
docker run --rm --network host -v "$PWD/tools/seguridad":/scripts:ro -e BASE=http://127.0.0.1:5080 grafana/k6 run /scripts/avalancha.js

# ZAP: SOLO contra una base desechable (ataca de verdad). Ejemplo de API para escanear:
#   ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5091 BaseDatos__Nombre=FlowTwin_Seguridad \
#   Mqtt__Activo=false Contrasenas__ExigirCambioPrimerIngreso=false Jwt__MinutosAcceso=120 \
#   LimitePeticiones__ApiPorMinuto=100000 LimitePeticiones__ApiRafaga=100000 LimitePeticiones__AuthPorMinuto=100000 \
#   dotnet run --project backend/src/FlowTwin.Api
API=http://127.0.0.1:5091 CORREO=admin@creo.pe CLAVE=demo123 ./tools/seguridad/zap.sh zap-salida/
```
