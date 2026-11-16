using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Gemelo_TipoTriaje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona");

            migrationBuilder.InsertData(
                schema: "cat",
                table: "TipoZona",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[] { 6, "TRIAJE", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Triaje", null, 1 });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 4,
                column: "TipoZonaId",
                value: 6);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona",
                sql: "([Aforo] IS NULL AND [UmbralMediaPct] IS NULL AND [UmbralAltaPct] IS NULL) OR ([TipoZonaId] IN (1, 2, 3, 6) AND [Aforo] >= 1 AND [UmbralMediaPct] IS NOT NULL AND [UmbralAltaPct] IS NOT NULL AND [UmbralMediaPct] > 0 AND [UmbralMediaPct] <= [UmbralAltaPct] AND [UmbralAltaPct] <= 100)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona");

            migrationBuilder.DeleteData(
                schema: "cat",
                table: "TipoZona",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 4,
                column: "TipoZonaId",
                value: 3);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona",
                sql: "([Aforo] IS NULL AND [UmbralMediaPct] IS NULL AND [UmbralAltaPct] IS NULL) OR ([TipoZonaId] IN (1, 2, 3) AND [Aforo] >= 1 AND [UmbralMediaPct] IS NOT NULL AND [UmbralAltaPct] IS NOT NULL AND [UmbralMediaPct] > 0 AND [UmbralMediaPct] <= [UmbralAltaPct] AND [UmbralAltaPct] <= 100)");
        }
    }
}
