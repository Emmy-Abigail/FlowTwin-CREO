using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Atenciones_PausaZona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PausaZona",
                schema: "ope",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZonaId = table.Column<int>(type: "int", nullable: false),
                    MotivoPausaId = table.Column<int>(type: "int", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InicioEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    FinEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PausaZona", x => x.Id);
                    table.CheckConstraint("CK_PausaZona_Fin", "[FinEn] IS NULL OR [FinEn] >= [InicioEn]");
                    table.ForeignKey(
                        name: "FK_PausaZona_MotivoPausa_MotivoPausaId",
                        column: x => x.MotivoPausaId,
                        principalSchema: "cat",
                        principalTable: "MotivoPausa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PausaZona_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "seg",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PausaZona_Zona_ZonaId",
                        column: x => x.ZonaId,
                        principalSchema: "gem",
                        principalTable: "Zona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PausaZona_MotivoPausaId",
                schema: "ope",
                table: "PausaZona",
                column: "MotivoPausaId");

            migrationBuilder.CreateIndex(
                name: "IX_PausaZona_UsuarioId",
                schema: "ope",
                table: "PausaZona",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PausaZona_ZonaId_InicioEn",
                schema: "ope",
                table: "PausaZona",
                columns: new[] { "ZonaId", "InicioEn" });

            migrationBuilder.CreateIndex(
                name: "UX_PausaZona_Abierta",
                schema: "ope",
                table: "PausaZona",
                column: "ZonaId",
                unique: true,
                filter: "[FinEn] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PausaZona",
                schema: "ope");
        }
    }
}
