namespace FlowTwin.SharedKernel;

/// <summary>Catálogo cat.AccionAuditoria: qué se hizo en cada fila de la auditoría.</summary>
public enum AccionAuditoria
{
    [Catalogo("INICIAR_SESION", "Inició sesión")] IniciarSesion = 1,
    [Catalogo("CERRAR_SESION", "Cerró sesión")] CerrarSesion = 2,
    [Catalogo("CREAR", "Creó")] Crear = 3,
    [Catalogo("EDITAR", "Editó")] Editar = 4,
    [Catalogo("DESACTIVAR", "Desactivó")] Desactivar = 5,
    [Catalogo("ACTIVAR", "Activó")] Activar = 6,
    [Catalogo("RESTABLECER_CONTRASENA", "Restableció la contraseña")] RestablecerContrasena = 7,
    [Catalogo("CAMBIAR_CONTRASENA", "Cambió su contraseña")] CambiarContrasena = 8,
    [Catalogo("PUBLICAR", "Publicó")] Publicar = 9,
    [Catalogo("ENTREGAR", "Entregó tarjeta")] Entregar = 10,
    [Catalogo("RECIBIR", "Recibió tarjeta")] Recibir = 11,
    [Catalogo("DESHACER", "Deshizo la entrega")] Deshacer = 12,
    [Catalogo("PAUSAR", "Pausó zona")] Pausar = 13,
    [Catalogo("REANUDAR", "Reanudó zona")] Reanudar = 14,
    [Catalogo("EXPORTAR", "Exportó reporte")] Exportar = 15,
    [Catalogo("DAR_BAJA", "Dio de baja")] DarBaja = 16,
    [Catalogo("DESINFECTAR", "Marcó desinfectada")] Desinfectar = 17,
    [Catalogo("EXTRAVIAR", "Marcó extraviada")] Extraviar = 18,
    [Catalogo("RECUPERAR", "Recuperó tarjeta")] Recuperar = 19,
}
