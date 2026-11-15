/*
  Vuelve una base de DESARROLLO al estado en que la deja la semilla: sin atenciones, lecturas, eventos de
  zona ni movimientos de tarjeta; las tarjetas disponibles y los nodos sin señal. No toca usuarios,
  sesiones, zonas, configuración ni la auditoría (es de solo agregar: queda como registro de lo que pasó).

  Uso (desde backend/, con el contenedor de SQL Server levantado):
    set -a; . ./.env; set +a
    docker exec -i -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" flowtwin-sqlserver \
      /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d FlowTwin < tools/limpiar-datos-desarrollo.sql
  (-d FlowTwin_Simulador para la base del simulador).

  DESPUÉS, reinicia la API que use esa base: guarda en memoria dónde se vio cada tarjeta por última vez.
  Todo va en una transacción: o se limpia completo o no cambia nada.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
-- sqlcmd arranca con QUOTED_IDENTIFIER apagado y los índices filtrados lo exigen encendido
SET QUOTED_IDENTIFIER ON;

-- Solo en las bases de desarrollo. Nunca en la de la clínica.
DECLARE @Base sysname = DB_NAME();
IF @Base NOT IN (N'FlowTwin', N'FlowTwin_Simulador')
BEGIN
    RAISERROR(N'Base «%s»: este script solo limpia FlowTwin o FlowTwin_Simulador (desarrollo).', 16, 1, @Base);
    RETURN;
END;

DECLARE @Disponible int = (SELECT Id FROM cat.EstadoTarjeta WHERE Codigo = 'DISPONIBLE');
DECLARE @Baja int = (SELECT Id FROM cat.EstadoTarjeta WHERE Codigo = 'BAJA');
DECLARE @SinSenal int = (SELECT Id FROM cat.EstadoNodo WHERE Codigo = 'SIN_SENAL');

BEGIN TRANSACTION;

-- De las hijas a las madres (las claves foráneas no borran en cascada)
DELETE FROM ope.EventoZona;
DELETE FROM dis.MovimientoTarjeta;
DELETE FROM ope.Atencion;
DELETE FROM ope.LecturaBle;
-- De cada nodo se conserva solo su primer evento (el registro); se van los latidos y caídas
DELETE e FROM dis.EventoNodo e
WHERE e.Id <> (SELECT MIN(p.Id) FROM dis.EventoNodo p WHERE p.NodoId = e.NodoId);

-- Las tarjetas vuelven a estar disponibles, salvo las dadas de baja (eso lo decidió un Administrador)
UPDATE dis.Tarjeta SET EstadoTarjetaId = @Disponible WHERE EstadoTarjetaId <> @Baja;
-- Los nodos, como recién registrados: sin señal y sin latido ni firmware conocido
UPDATE dis.Nodo SET EstadoNodoId = @SinSenal, UltimoLatidoEn = NULL, VersionFirmware = NULL;

COMMIT TRANSACTION;

SELECT
    (SELECT COUNT(*) FROM ope.Atencion)          AS Atenciones,
    (SELECT COUNT(*) FROM ope.LecturaBle)        AS Lecturas,
    (SELECT COUNT(*) FROM dis.MovimientoTarjeta) AS Movimientos,
    (SELECT COUNT(*) FROM dis.Tarjeta WHERE EstadoTarjetaId = @Disponible) AS TarjetasDisponibles,
    (SELECT COUNT(*) FROM dis.Nodo WHERE EstadoNodoId = @SinSenal)         AS NodosSinSenal;
