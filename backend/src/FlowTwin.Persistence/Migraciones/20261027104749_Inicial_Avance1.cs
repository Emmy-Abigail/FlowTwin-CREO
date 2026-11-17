using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial_Avance1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cat");

            migrationBuilder.EnsureSchema(
                name: "ope");

            migrationBuilder.EnsureSchema(
                name: "aud");

            migrationBuilder.EnsureSchema(
                name: "seg");

            migrationBuilder.EnsureSchema(
                name: "dis");

            migrationBuilder.EnsureSchema(
                name: "gem");

            migrationBuilder.CreateTable(
                name: "AccionAuditoria",
                schema: "cat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccionAuditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EstadoNodo",
                schema: "cat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadoNodo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EstadoTarjeta",
                schema: "cat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadoTarjeta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MotivoCierreAtencion",
                schema: "cat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivoCierreAtencion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MotivoPausa",
                schema: "cat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivoPausa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rol",
                schema: "cat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rol", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sede",
                schema: "gem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PrefijoAtencion = table.Column<string>(type: "char(1)", nullable: false, defaultValue: "A"),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sede", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipoAlerta",
                schema: "cat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoAlerta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipoElementoPlano",
                schema: "cat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoElementoPlano", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipoZona",
                schema: "cat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoZona", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Auditoria",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccionAuditoriaId = table.Column<int>(type: "int", nullable: false),
                    Entidad = table.Column<string>(type: "varchar(60)", nullable: false),
                    EntidadId = table.Column<string>(type: "varchar(40)", nullable: true),
                    ValorAntes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorDespues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ip = table.Column<string>(type: "varchar(45)", nullable: true),
                    TraceId = table.Column<string>(type: "varchar(64)", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                    table.CheckConstraint("CK_Auditoria_Antes", "[ValorAntes] IS NULL OR ISJSON([ValorAntes]) = 1");
                    table.CheckConstraint("CK_Auditoria_Despues", "[ValorDespues] IS NULL OR ISJSON([ValorDespues]) = 1");
                    table.ForeignKey(
                        name: "FK_Auditoria_AccionAuditoria_AccionAuditoriaId",
                        column: x => x.AccionAuditoriaId,
                        principalSchema: "cat",
                        principalTable: "AccionAuditoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Usuario",
                schema: "seg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Correo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Nombres = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Apellidos = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RolId = table.Column<int>(type: "int", nullable: false),
                    ContrasenaHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DebeCambiarContrasena = table.Column<bool>(type: "bit", nullable: false),
                    ContrasenaTemporalExpiraEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ContrasenaCambiadaEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    IntentosFallidos = table.Column<byte>(type: "tinyint", nullable: false),
                    BloqueadoHasta = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UltimoIngresoEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                    table.CheckConstraint("CK_Usuario_IntentosFallidos", "[IntentosFallidos] >= 0");
                    table.ForeignKey(
                        name: "FK_Usuario_Rol_RolId",
                        column: x => x.RolId,
                        principalSchema: "cat",
                        principalTable: "Rol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Piso",
                schema: "gem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SedeId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Nivel = table.Column<short>(type: "smallint", nullable: false),
                    PlanoRuta = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    AnchoM = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    FondoM = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    AltoMuroM = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false, defaultValue: 2.70m),
                    Publicado = table.Column<bool>(type: "bit", nullable: false),
                    PublicadoEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Piso", x => x.Id);
                    table.CheckConstraint("CK_Piso_AnchoM", "[AnchoM] > 0");
                    table.CheckConstraint("CK_Piso_FondoM", "[FondoM] > 0");
                    table.ForeignKey(
                        name: "FK_Piso_Sede_SedeId",
                        column: x => x.SedeId,
                        principalSchema: "gem",
                        principalTable: "Sede",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tarjeta",
                schema: "dis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "varchar(10)", nullable: false),
                    UuidBle = table.Column<string>(type: "varchar(64)", nullable: false),
                    CodigoQr = table.Column<string>(type: "varchar(64)", nullable: false),
                    SedeId = table.Column<int>(type: "int", nullable: false),
                    EstadoTarjetaId = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarjeta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tarjeta_EstadoTarjeta_EstadoTarjetaId",
                        column: x => x.EstadoTarjetaId,
                        principalSchema: "cat",
                        principalTable: "EstadoTarjeta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tarjeta_Sede_SedeId",
                        column: x => x.SedeId,
                        principalSchema: "gem",
                        principalTable: "Sede",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialContrasena",
                schema: "seg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    ContrasenaHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialContrasena", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialContrasena_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "seg",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SesionUsuario",
                schema: "seg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    TokenRefrescoHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    UltimaActividadEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ExpiraEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    RevocadaEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    Ip = table.Column<string>(type: "varchar(45)", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionUsuario", x => x.Id);
                    table.CheckConstraint("CK_SesionUsuario_Expira", "[ExpiraEn] > [FechaRegistro]");
                    table.ForeignKey(
                        name: "FK_SesionUsuario_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "seg",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioPiso",
                schema: "seg",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    PisoId = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioPiso", x => new { x.UsuarioId, x.PisoId });
                    table.ForeignKey(
                        name: "FK_UsuarioPiso_Piso_PisoId",
                        column: x => x.PisoId,
                        principalSchema: "gem",
                        principalTable: "Piso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuarioPiso_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "seg",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Atencion",
                schema: "ope",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "varchar(10)", nullable: false),
                    FechaAtencion = table.Column<DateOnly>(type: "date", nullable: false),
                    SedeId = table.Column<int>(type: "int", nullable: false),
                    TarjetaId = table.Column<int>(type: "int", nullable: false),
                    InicioEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    FinEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    MotivoCierreId = table.Column<int>(type: "int", nullable: true),
                    RecibidaPor = table.Column<int>(type: "int", nullable: true),
                    UltimaLecturaEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Atencion", x => x.Id);
                    table.CheckConstraint("CK_Atencion_Fin", "[FinEn] IS NULL OR [FinEn] >= [InicioEn]");
                    table.CheckConstraint("CK_Atencion_Motivo", "([FinEn] IS NULL AND [MotivoCierreId] IS NULL) OR ([FinEn] IS NOT NULL AND [MotivoCierreId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Atencion_MotivoCierreAtencion_MotivoCierreId",
                        column: x => x.MotivoCierreId,
                        principalSchema: "cat",
                        principalTable: "MotivoCierreAtencion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Atencion_Sede_SedeId",
                        column: x => x.SedeId,
                        principalSchema: "gem",
                        principalTable: "Sede",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Atencion_Tarjeta_TarjetaId",
                        column: x => x.TarjetaId,
                        principalSchema: "dis",
                        principalTable: "Tarjeta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Atencion_Usuario_RecibidaPor",
                        column: x => x.RecibidaPor,
                        principalSchema: "seg",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientoTarjeta",
                schema: "dis",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TarjetaId = table.Column<int>(type: "int", nullable: false),
                    EstadoAnteriorId = table.Column<int>(type: "int", nullable: true),
                    EstadoNuevoId = table.Column<int>(type: "int", nullable: false),
                    AtencionId = table.Column<long>(type: "bigint", nullable: true),
                    Observacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientoTarjeta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientoTarjeta_Atencion_AtencionId",
                        column: x => x.AtencionId,
                        principalSchema: "ope",
                        principalTable: "Atencion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientoTarjeta_EstadoTarjeta_EstadoAnteriorId",
                        column: x => x.EstadoAnteriorId,
                        principalSchema: "cat",
                        principalTable: "EstadoTarjeta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientoTarjeta_EstadoTarjeta_EstadoNuevoId",
                        column: x => x.EstadoNuevoId,
                        principalSchema: "cat",
                        principalTable: "EstadoTarjeta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientoTarjeta_Tarjeta_TarjetaId",
                        column: x => x.TarjetaId,
                        principalSchema: "dis",
                        principalTable: "Tarjeta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "AccionAuditoria",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[,]
                {
                    { 1, "INICIAR_SESION", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Inició sesión", null, 1 },
                    { 2, "CERRAR_SESION", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Cerró sesión", null, 1 },
                    { 3, "CREAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Creó", null, 1 },
                    { 4, "EDITAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Editó", null, 1 },
                    { 5, "DESACTIVAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Desactivó", null, 1 },
                    { 6, "ACTIVAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Activó", null, 1 },
                    { 7, "RESTABLECER_CONTRASENA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Restableció la contraseña", null, 1 },
                    { 8, "CAMBIAR_CONTRASENA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Cambió su contraseña", null, 1 },
                    { 9, "PUBLICAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Publicó", null, 1 },
                    { 10, "ENTREGAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Entregó tarjeta", null, 1 },
                    { 11, "RECIBIR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Recibió tarjeta", null, 1 },
                    { 12, "DESHACER", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Deshizo la entrega", null, 1 },
                    { 13, "PAUSAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Pausó zona", null, 1 },
                    { 14, "REANUDAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Reanudó zona", null, 1 },
                    { 15, "EXPORTAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Exportó reporte", null, 1 },
                    { 16, "DAR_BAJA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Dio de baja", null, 1 },
                    { 17, "DESINFECTAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Marcó desinfectada", null, 1 },
                    { 18, "EXTRAVIAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Marcó extraviada", null, 1 },
                    { 19, "RECUPERAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Recuperó tarjeta", null, 1 }
                });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "EstadoNodo",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[,]
                {
                    { 1, "EN_LINEA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "En línea", null, 1 },
                    { 2, "SIN_SENAL", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Sin señal", null, 1 },
                    { 3, "BAJA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "De baja", null, 1 }
                });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "EstadoTarjeta",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[,]
                {
                    { 1, "DISPONIBLE", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Disponible", null, 1 },
                    { 2, "EN_USO", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "En uso", null, 1 },
                    { 3, "POR_DESINFECTAR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Por desinfectar", null, 1 },
                    { 4, "SIN_DEVOLVER", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Sin devolver", null, 1 },
                    { 5, "EXTRAVIADA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Extraviada", null, 1 },
                    { 6, "BAJA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "De baja", null, 1 }
                });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "MotivoCierreAtencion",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[,]
                {
                    { 1, "DEVUELTA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Devuelta", null, 1 },
                    { 2, "SIN_LECTURAS", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Sin lecturas", null, 1 },
                    { 3, "CIERRE_JORNADA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Cierre de jornada", null, 1 },
                    { 4, "ANULADA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Anulada", null, 1 }
                });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "MotivoPausa",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[,]
                {
                    { 1, "LIMPIEZA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Limpieza", null, 1 },
                    { 2, "MANTENIMIENTO", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Mantenimiento", null, 1 },
                    { 3, "SIN_ATENCION", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Sin atención", null, 1 },
                    { 4, "OTRO", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", null, 1 }
                });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "Rol",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[,]
                {
                    { 1, "ADMIN", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Administrador", null, 1 },
                    { 2, "OPERADOR", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Operador", null, 1 },
                    { 3, "GERENCIA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Gerencia", null, 1 }
                });

            migrationBuilder.InsertData(
                schema: "gem",
                table: "Sede",
                columns: new[] { "Id", "Direccion", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "PrefijoAtencion", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[] { 1, "Jr. Eloy Espinoza 700, San Martín de Porres", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Sede Principal", "A", null, 1 });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "TipoAlerta",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[,]
                {
                    { 1, "AFORO_ALTO", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Aforo alto", null, 1 },
                    { 2, "NODO_SIN_SENAL", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Nodo sin señal", null, 1 },
                    { 3, "TARJETA_SIN_DEVOLVER", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Tarjeta sin devolver", null, 1 },
                    { 4, "ESPERA_LARGA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Espera larga", null, 1 }
                });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "TipoElementoPlano",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[,]
                {
                    { 1, "MURO", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Muro", null, 1 },
                    { 2, "PUERTA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Puerta", null, 1 },
                    { 3, "VENTANA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Ventana", null, 1 },
                    { 4, "AMBIENTE", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Ambiente", null, 1 }
                });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "TipoZona",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[,]
                {
                    { 1, "INGRESO", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso", null, 1 },
                    { 2, "ESPERA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Espera", null, 1 },
                    { 3, "SERVICIO", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Servicio", null, 1 },
                    { 4, "TRANSITO", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Tránsito", null, 1 },
                    { 5, "SALIDA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Salida", null, 1 }
                });

            migrationBuilder.InsertData(
                schema: "gem",
                table: "Piso",
                columns: new[] { "Id", "AltoMuroM", "AnchoM", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "FondoM", "Nivel", "Nombre", "PlanoRuta", "Publicado", "PublicadoEn", "SedeId", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[] { 1, 2.70m, 40.12m, true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), 20.99m, (short)1, "Piso 1", "planos/piso1.jpg", true, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, 1 });

            migrationBuilder.InsertData(
                schema: "seg",
                table: "Usuario",
                columns: new[] { "Id", "Apellidos", "BloqueadoHasta", "ContrasenaCambiadaEn", "ContrasenaHash", "ContrasenaTemporalExpiraEn", "Correo", "DebeCambiarContrasena", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "IntentosFallidos", "Nombres", "RolId", "UltimoIngresoEn", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[] { 1, "FlowTwin", null, null, "!", null, "sistema@flowtwin.local", false, true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), (byte)0, "Sistema", 1, null, null, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_AccionAuditoria_Codigo",
                schema: "cat",
                table: "AccionAuditoria",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Atencion_MotivoCierreId",
                schema: "ope",
                table: "Atencion",
                column: "MotivoCierreId");

            migrationBuilder.CreateIndex(
                name: "IX_Atencion_RecibidaPor",
                schema: "ope",
                table: "Atencion",
                column: "RecibidaPor");

            migrationBuilder.CreateIndex(
                name: "IX_Atencion_SedeId_FechaAtencion_Codigo",
                schema: "ope",
                table: "Atencion",
                columns: new[] { "SedeId", "FechaAtencion", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Atencion_SedeId_InicioEn",
                schema: "ope",
                table: "Atencion",
                columns: new[] { "SedeId", "InicioEn" });

            migrationBuilder.CreateIndex(
                name: "UX_Atencion_TarjetaAbierta",
                schema: "ope",
                table: "Atencion",
                column: "TarjetaId",
                unique: true,
                filter: "[FinEn] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_AccionAuditoriaId",
                schema: "aud",
                table: "Auditoria",
                column: "AccionAuditoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_FechaRegistro",
                schema: "aud",
                table: "Auditoria",
                column: "FechaRegistro");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_UsuarioRegistroId_FechaRegistro",
                schema: "aud",
                table: "Auditoria",
                columns: new[] { "UsuarioRegistroId", "FechaRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_EstadoNodo_Codigo",
                schema: "cat",
                table: "EstadoNodo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstadoTarjeta_Codigo",
                schema: "cat",
                table: "EstadoTarjeta",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistorialContrasena_UsuarioId_Id",
                schema: "seg",
                table: "HistorialContrasena",
                columns: new[] { "UsuarioId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MotivoCierreAtencion_Codigo",
                schema: "cat",
                table: "MotivoCierreAtencion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MotivoPausa_Codigo",
                schema: "cat",
                table: "MotivoPausa",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoTarjeta_AtencionId",
                schema: "dis",
                table: "MovimientoTarjeta",
                column: "AtencionId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoTarjeta_EstadoAnteriorId",
                schema: "dis",
                table: "MovimientoTarjeta",
                column: "EstadoAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoTarjeta_EstadoNuevoId",
                schema: "dis",
                table: "MovimientoTarjeta",
                column: "EstadoNuevoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoTarjeta_TarjetaId_Id",
                schema: "dis",
                table: "MovimientoTarjeta",
                columns: new[] { "TarjetaId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Piso_SedeId_Nivel",
                schema: "gem",
                table: "Piso",
                columns: new[] { "SedeId", "Nivel" },
                unique: true,
                filter: "[EstadoRegistro] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Piso_SedeId_Nombre",
                schema: "gem",
                table: "Piso",
                columns: new[] { "SedeId", "Nombre" },
                unique: true,
                filter: "[EstadoRegistro] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Rol_Codigo",
                schema: "cat",
                table: "Rol",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sede_Nombre",
                schema: "gem",
                table: "Sede",
                column: "Nombre",
                unique: true,
                filter: "[EstadoRegistro] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SesionUsuario_TokenRefrescoHash",
                schema: "seg",
                table: "SesionUsuario",
                column: "TokenRefrescoHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesionUsuario_UsuarioId_RevocadaEn",
                schema: "seg",
                table: "SesionUsuario",
                columns: new[] { "UsuarioId", "RevocadaEn" });

            migrationBuilder.CreateIndex(
                name: "IX_Tarjeta_Codigo",
                schema: "dis",
                table: "Tarjeta",
                column: "Codigo",
                unique: true,
                filter: "[EstadoRegistro] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Tarjeta_CodigoQr",
                schema: "dis",
                table: "Tarjeta",
                column: "CodigoQr",
                unique: true,
                filter: "[EstadoRegistro] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Tarjeta_EstadoTarjetaId",
                schema: "dis",
                table: "Tarjeta",
                column: "EstadoTarjetaId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarjeta_SedeId",
                schema: "dis",
                table: "Tarjeta",
                column: "SedeId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarjeta_UuidBle",
                schema: "dis",
                table: "Tarjeta",
                column: "UuidBle",
                unique: true,
                filter: "[EstadoRegistro] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TipoAlerta_Codigo",
                schema: "cat",
                table: "TipoAlerta",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TipoElementoPlano_Codigo",
                schema: "cat",
                table: "TipoElementoPlano",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TipoZona_Codigo",
                schema: "cat",
                table: "TipoZona",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_Correo",
                schema: "seg",
                table: "Usuario",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_RolId",
                schema: "seg",
                table: "Usuario",
                column: "RolId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioPiso_PisoId",
                schema: "seg",
                table: "UsuarioPiso",
                column: "PisoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "EstadoNodo",
                schema: "cat");

            migrationBuilder.DropTable(
                name: "HistorialContrasena",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "MotivoPausa",
                schema: "cat");

            migrationBuilder.DropTable(
                name: "MovimientoTarjeta",
                schema: "dis");

            migrationBuilder.DropTable(
                name: "SesionUsuario",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "TipoAlerta",
                schema: "cat");

            migrationBuilder.DropTable(
                name: "TipoElementoPlano",
                schema: "cat");

            migrationBuilder.DropTable(
                name: "TipoZona",
                schema: "cat");

            migrationBuilder.DropTable(
                name: "UsuarioPiso",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "AccionAuditoria",
                schema: "cat");

            migrationBuilder.DropTable(
                name: "Atencion",
                schema: "ope");

            migrationBuilder.DropTable(
                name: "Piso",
                schema: "gem");

            migrationBuilder.DropTable(
                name: "MotivoCierreAtencion",
                schema: "cat");

            migrationBuilder.DropTable(
                name: "Tarjeta",
                schema: "dis");

            migrationBuilder.DropTable(
                name: "Usuario",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "EstadoTarjeta",
                schema: "cat");

            migrationBuilder.DropTable(
                name: "Sede",
                schema: "gem");

            migrationBuilder.DropTable(
                name: "Rol",
                schema: "cat");
        }
    }
}
