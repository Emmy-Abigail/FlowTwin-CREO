using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Gemelo_AforoPorDefinir : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona");

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null, null });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona",
                sql: "([Aforo] IS NULL AND [UmbralMediaPct] IS NULL AND [UmbralAltaPct] IS NULL) OR ([TipoZonaId] IN (1, 2, 3) AND [Aforo] >= 1 AND [UmbralMediaPct] IS NOT NULL AND [UmbralAltaPct] IS NOT NULL AND [UmbralMediaPct] > 0 AND [UmbralMediaPct] <= [UmbralAltaPct] AND [UmbralAltaPct] <= 100)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona");

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { (short)6, (byte)85, (byte)60 });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { (short)24, (byte)85, (byte)60 });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { (short)3, (byte)85, (byte)60 });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { (short)3, (byte)85, (byte)60 });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { (short)4, (byte)85, (byte)60 });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { (short)6, (byte)85, (byte)60 });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Aforo", "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { (short)4, (byte)85, (byte)60 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona",
                sql: "([TipoZonaId] IN (1, 2, 3) AND [Aforo] >= 1 AND [UmbralMediaPct] > 0 AND [UmbralMediaPct] <= [UmbralAltaPct] AND [UmbralAltaPct] <= 100) OR ([TipoZonaId] IN (4, 5) AND [Aforo] IS NULL AND [UmbralMediaPct] IS NULL AND [UmbralAltaPct] IS NULL)");
        }
    }
}
