using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Avance2_Nodos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Nodo",
                schema: "dis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "varchar(10)", nullable: false),
                    Mac = table.Column<string>(type: "char(17)", nullable: false),
                    ZonaId = table.Column<int>(type: "int", nullable: false),
                    PosX = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    PosY = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    ClaveApiHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    EstadoNodoId = table.Column<int>(type: "int", nullable: false),
                    UltimoLatidoEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    VersionFirmware = table.Column<string>(type: "varchar(20)", nullable: true),
                    AjusteRssiDb = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodo", x => x.Id);
                    table.CheckConstraint("CK_Nodo_AjusteRssiDb", "[AjusteRssiDb] BETWEEN -20 AND 20");
                    table.CheckConstraint("CK_Nodo_Mac", "[Mac] COLLATE Latin1_General_BIN2 LIKE '[0-9A-F][0-9A-F]:[0-9A-F][0-9A-F]:[0-9A-F][0-9A-F]:[0-9A-F][0-9A-F]:[0-9A-F][0-9A-F]:[0-9A-F][0-9A-F]'");
                    table.ForeignKey(
                        name: "FK_Nodo_EstadoNodo_EstadoNodoId",
                        column: x => x.EstadoNodoId,
                        principalSchema: "cat",
                        principalTable: "EstadoNodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Nodo_Zona_ZonaId",
                        column: x => x.ZonaId,
                        principalSchema: "gem",
                        principalTable: "Zona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventoNodo",
                schema: "dis",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NodoId = table.Column<int>(type: "int", nullable: false),
                    EstadoNodoId = table.Column<int>(type: "int", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoNodo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventoNodo_EstadoNodo_EstadoNodoId",
                        column: x => x.EstadoNodoId,
                        principalSchema: "cat",
                        principalTable: "EstadoNodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventoNodo_Nodo_NodoId",
                        column: x => x.NodoId,
                        principalSchema: "dis",
                        principalTable: "Nodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "AccionAuditoria",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[] { 20, "REGENERAR_CLAVE", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Regeneró la clave", null, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_EventoNodo_EstadoNodoId",
                schema: "dis",
                table: "EventoNodo",
                column: "EstadoNodoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventoNodo_NodoId_Id",
                schema: "dis",
                table: "EventoNodo",
                columns: new[] { "NodoId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Nodo_Codigo",
                schema: "dis",
                table: "Nodo",
                column: "Codigo",
                unique: true,
                filter: "[EstadoRegistro] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Nodo_EstadoNodoId",
                schema: "dis",
                table: "Nodo",
                column: "EstadoNodoId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodo_Mac",
                schema: "dis",
                table: "Nodo",
                column: "Mac",
                unique: true,
                filter: "[EstadoRegistro] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Nodo_ZonaId",
                schema: "dis",
                table: "Nodo",
                column: "ZonaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventoNodo",
                schema: "dis");

            migrationBuilder.DropTable(
                name: "Nodo",
                schema: "dis");

            migrationBuilder.DeleteData(
                schema: "cat",
                table: "AccionAuditoria",
                keyColumn: "Id",
                keyValue: 20);
        }
    }
}
