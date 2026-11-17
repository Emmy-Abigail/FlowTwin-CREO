using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Avance2_Zonas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Zona",
                schema: "gem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PisoId = table.Column<int>(type: "int", nullable: false),
                    TipoZonaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    X = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Y = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    AnchoM = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    FondoM = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Geometria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Aforo = table.Column<short>(type: "smallint", nullable: true),
                    UmbralMediaPct = table.Column<byte>(type: "tinyint", nullable: true),
                    UmbralAltaPct = table.Column<byte>(type: "tinyint", nullable: true),
                    EnBorrador = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zona", x => x.Id);
                    table.CheckConstraint("CK_Zona_AforoRN13", "([TipoZonaId] IN (1, 2, 3) AND [Aforo] >= 1 AND [UmbralMediaPct] > 0 AND [UmbralMediaPct] <= [UmbralAltaPct] AND [UmbralAltaPct] <= 100) OR ([TipoZonaId] IN (4, 5) AND [Aforo] IS NULL AND [UmbralMediaPct] IS NULL AND [UmbralAltaPct] IS NULL)");
                    table.CheckConstraint("CK_Zona_Geometria", "[Geometria] IS NULL OR ISJSON([Geometria]) = 1");
                    table.CheckConstraint("CK_Zona_Posicion", "[X] >= 0 AND [Y] >= 0");
                    table.CheckConstraint("CK_Zona_Tamano", "[AnchoM] > 0 AND [FondoM] > 0");
                    table.ForeignKey(
                        name: "FK_Zona_Piso_PisoId",
                        column: x => x.PisoId,
                        principalSchema: "gem",
                        principalTable: "Piso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Zona_TipoZona_TipoZonaId",
                        column: x => x.TipoZonaId,
                        principalSchema: "cat",
                        principalTable: "TipoZona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "gem",
                table: "Zona",
                columns: new[] { "Id", "Aforo", "AnchoM", "EnBorrador", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "FondoM", "Geometria", "Nombre", "PisoId", "TipoZonaId", "UmbralAltaPct", "UmbralMediaPct", "UsuarioActualizacionId", "UsuarioRegistroId", "X", "Y" },
                values: new object[,]
                {
                    { 1, (short)6, 2.1m, false, true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), 5.1m, null, "Admisión y caja", 1, 1, (byte)85, (byte)60, null, 1, 35.0m, 8.7m },
                    { 2, null, 6.2m, false, true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), 4.8m, null, "Hall principal", 1, 4, null, null, null, 1, 28.5m, 10.2m },
                    { 3, (short)24, 5.7m, false, true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), 5.3m, null, "Sala de espera", 1, 2, (byte)85, (byte)60, null, 1, 19.3m, 15.0m },
                    { 4, (short)3, 3.1m, false, true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), 4.8m, null, "Triaje", 1, 3, (byte)85, (byte)60, null, 1, 25.2m, 13.9m },
                    { 5, (short)3, 3.1m, false, true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), 4.7m, null, "Tópico de enfermería", 1, 3, (byte)85, (byte)60, null, 1, 11.2m, 11.9m },
                    { 6, (short)4, 5.0m, false, true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), 3.7m, null, "Tópico urgencia", 1, 3, (byte)85, (byte)60, null, 1, 9.8m, 16.9m },
                    { 7, (short)6, 7.4m, false, true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), 5.2m, null, "Módulo 2 (consultorio)", 1, 3, (byte)85, (byte)60, null, 1, 2.2m, 15.1m },
                    { 8, (short)4, 3.2m, false, true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), 6.5m, null, "Observación", 1, 3, (byte)85, (byte)60, null, 1, 16.6m, 13.9m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Zona_PisoId_Nombre",
                schema: "gem",
                table: "Zona",
                columns: new[] { "PisoId", "Nombre" },
                unique: true,
                filter: "[EstadoRegistro] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Zona_TipoZonaId",
                schema: "gem",
                table: "Zona",
                column: "TipoZonaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Zona",
                schema: "gem");
        }
    }
}
