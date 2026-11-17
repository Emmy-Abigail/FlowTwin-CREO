using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Atenciones_LotesNodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoteNodo",
                schema: "ope",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NodoId = table.Column<int>(type: "int", nullable: false),
                    Secuencia = table.Column<long>(type: "bigint", nullable: false),
                    Lecturas = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoteNodo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoteNodo_Nodo_NodoId",
                        column: x => x.NodoId,
                        principalSchema: "dis",
                        principalTable: "Nodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoteNodo_FechaRegistro",
                schema: "ope",
                table: "LoteNodo",
                column: "FechaRegistro");

            migrationBuilder.CreateIndex(
                name: "IX_LoteNodo_NodoId_Id",
                schema: "ope",
                table: "LoteNodo",
                columns: new[] { "NodoId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoteNodo",
                schema: "ope");
        }
    }
}
