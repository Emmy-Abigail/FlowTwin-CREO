using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Avance2_EventoZona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventoZona",
                schema: "ope",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AtencionId = table.Column<long>(type: "bigint", nullable: false),
                    ZonaId = table.Column<int>(type: "int", nullable: false),
                    EntradaEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    SalidaEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoZona", x => x.Id);
                    table.CheckConstraint("CK_EventoZona_Salida", "[SalidaEn] IS NULL OR [SalidaEn] >= [EntradaEn]");
                    table.ForeignKey(
                        name: "FK_EventoZona_Atencion_AtencionId",
                        column: x => x.AtencionId,
                        principalSchema: "ope",
                        principalTable: "Atencion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventoZona_Zona_ZonaId",
                        column: x => x.ZonaId,
                        principalSchema: "gem",
                        principalTable: "Zona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventoZona_AtencionId_EntradaEn",
                schema: "ope",
                table: "EventoZona",
                columns: new[] { "AtencionId", "EntradaEn" });

            migrationBuilder.CreateIndex(
                name: "IX_EventoZona_ZonaId_SalidaEn",
                schema: "ope",
                table: "EventoZona",
                columns: new[] { "ZonaId", "SalidaEn" });

            migrationBuilder.CreateIndex(
                name: "UX_EventoZona_AtencionAbierta",
                schema: "ope",
                table: "EventoZona",
                column: "AtencionId",
                unique: true,
                filter: "[SalidaEn] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventoZona",
                schema: "ope");
        }
    }
}
