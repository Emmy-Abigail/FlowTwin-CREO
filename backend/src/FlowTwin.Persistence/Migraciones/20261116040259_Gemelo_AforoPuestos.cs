using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Gemelo_AforoPuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona",
                sql: "[Aforo] IS NULL OR ([TipoZonaId] IN (1, 2, 3, 6) AND [Aforo] >= 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Zona_AforoRN13",
                schema: "gem",
                table: "Zona",
                sql: "[Aforo] IS NULL OR ([TipoZonaId] IN (1, 2) AND [Aforo] >= 1)");
        }
    }
}
