# ADR-28. La maqueta 3D de cada piso la entrega la API desde una carpeta del servidor

**Estado:** Aceptada · **Fecha:** 16/11/2026 · **Completa:** ADR-27

## Contexto

La maqueta 3D (.glb, generada desde los planos de la clínica) estaba en `public/models/` del frontend y la
tabla de pisos con maqueta (`MAQUETAS`) estaba escrita en el código. Dos problemas:

- **Seguridad:** lo que está en `public/` se descarga **sin iniciar sesión**. La documentación de ASP.NET Core
  («Static file authorization») indica guardar los archivos que requieren permiso fuera de las carpetas
  públicas y entregarlos desde un endpoint que revise la autorización.
- **Mantenibilidad:** con Configuración (ADR-27) se agregan pisos sin programar, pero su maqueta 3D sí
  obligaba a programar. En los sistemas investigados (Mappedin, Pozyx, Navigine) el equipo técnico instala el
  mapa en el servidor del sitio y la aplicación lo pide con la sesión del usuario.

## Decisión

1. Carpeta `Maquetas:Ruta`, fuera del repositorio, con `piso-{id}/maqueta.glb`, `ambientes.json` y
   `zonas.json` (zonaId → rótulos del plano). **Instalar un piso = copiar su carpeta.**
2. Endpoints con el mismo permiso que la geometría (piso publicado y asignado, RN-24):
   `GET /pisos/{id}/maqueta` (mapa de zonas; 404 `SIN_MAQUETA` si no hay), `…/maqueta/modelo.glb` y
   `…/maqueta/ambientes.json`. Nombres de archivo fijos e id entero: no se puede pedir otro archivo.
3. **ETag** (tamaño + fecha) y `Cache-Control: private, no-cache`: el navegador reusa su copia y solo
   vuelve a bajar el .glb si cambió; ningún proxy compartido lo guarda.
4. El frontend lo descarga con `HttpClient` (pasa por el interceptor de sesión) y lo arma con
   `GLTFLoader.parseAsync`. Se elimina `MAQUETAS` del código.
5. El disco queda detrás de `IAlmacenMaquetas`: llevarlo a un almacenamiento en la nube cambia solo esa pieza.

## Alternativas

| Alternativa | Por qué no |
|---|---|
| Dejarlo en `public/` | Cualquiera lo descarga sin sesión; cada piso nuevo obliga a programar |
| Guardar el .glb en la base de datos | 5 MB por piso en SQL Server, respaldos más pesados y sin ventaja: no se consulta |
| Subir la maqueta desde Configuración | La genera el equipo técnico desde CAD (ADR-27); un formulario de carga no aporta al Administrador |

## Consecuencias

- ✅ Agregar la maqueta de un piso no requiere programar ni subir nada a git.
- ✅ El modelo de la clínica ya no es público.
- ⚠️ `zonas.json` lo escribe una persona: si está mal, la API responde `MAQUETA_ZONAS_INVALIDAS` y la pantalla
  lo dice; si un rótulo no existe, la consola lo avisa.
- ⚠️ En producción hay que configurar `Maquetas:Ruta` y copiar la carpeta en el servidor (y en los respaldos).
