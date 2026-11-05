using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Avance2_Ingesta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LecturaBle",
                schema: "ope",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NodoId = table.Column<int>(type: "int", nullable: false),
                    UuidBle = table.Column<string>(type: "varchar(64)", nullable: false),
                    Rssi = table.Column<short>(type: "smallint", nullable: false),
                    LeidaEn = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LecturaBle", x => x.Id);
                    table.CheckConstraint("CK_LecturaBle_Rssi", "[Rssi] BETWEEN -127 AND 0");
                    table.ForeignKey(
                        name: "FK_LecturaBle_Nodo_NodoId",
                        column: x => x.NodoId,
                        principalSchema: "dis",
                        principalTable: "Nodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LecturaBle_LeidaEn",
                schema: "ope",
                table: "LecturaBle",
                column: "LeidaEn");

            migrationBuilder.CreateIndex(
                name: "IX_LecturaBle_NodoId",
                schema: "ope",
                table: "LecturaBle",
                column: "NodoId");

            migrationBuilder.CreateIndex(
                name: "IX_LecturaBle_UuidBle_LeidaEn",
                schema: "ope",
                table: "LecturaBle",
                columns: new[] { "UuidBle", "LeidaEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LecturaBle",
                schema: "ope");
        }
    }
}
