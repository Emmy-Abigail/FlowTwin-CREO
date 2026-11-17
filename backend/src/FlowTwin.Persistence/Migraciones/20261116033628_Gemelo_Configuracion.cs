using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Gemelo_Configuracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona");

            migrationBuilder.DropColumn(
                name: "UmbralAltaPct",
                schema: "gem",
                table: "Zona");

            migrationBuilder.DropColumn(
                name: "UmbralMediaPct",
                schema: "gem",
                table: "Zona");

            migrationBuilder.CreateTable(
                name: "ParametrosSistema",
                schema: "gem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    EsperaAltaMin = table.Column<short>(type: "smallint", nullable: false),
                    EsperaCriticaMin = table.Column<short>(type: "smallint", nullable: false),
                    SinLecturasMin = table.Column<short>(type: "smallint", nullable: false),
                    HoraCierre = table.Column<byte>(type: "tinyint", nullable: false),
                    SemaforoAmbarPct = table.Column<byte>(type: "tinyint", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosSistema", x => x.Id);
                    table.CheckConstraint("CK_ParametrosSistema_Unica", "[Id] = 1");
                });

            migrationBuilder.InsertData(
                schema: "gem",
                table: "ParametrosSistema",
                columns: new[] { "Id", "EsperaAltaMin", "EsperaCriticaMin", "FechaActualizacion", "FechaRegistro", "HoraCierre", "SemaforoAmbarPct", "SinLecturasMin", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[] { 1, (short)40, (short)60, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), (byte)22, (byte)80, (short)30, null, 1 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona",
                sql: "[Aforo] IS NULL OR ([TipoZonaId] IN (1, 2) AND [Aforo] >= 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParametrosSistema",
                schema: "gem");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona");

            migrationBuilder.AddColumn<byte>(
                name: "UmbralAltaPct",
                schema: "gem",
                table: "Zona",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "UmbralMediaPct",
                schema: "gem",
                table: "Zona",
                type: "tinyint",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Zona",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "UmbralAltaPct", "UmbralMediaPct" },
                values: new object[] { null, null });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona",
                sql: "([Aforo] IS NULL AND [UmbralMediaPct] IS NULL AND [UmbralAltaPct] IS NULL) OR ([TipoZonaId] IN (1, 2, 3, 6) AND [Aforo] >= 1 AND [UmbralMediaPct] IS NOT NULL AND [UmbralAltaPct] IS NOT NULL AND [UmbralMediaPct] > 0 AND [UmbralMediaPct] <= [UmbralAltaPct] AND [UmbralAltaPct] <= 100)");
        }
    }
}
