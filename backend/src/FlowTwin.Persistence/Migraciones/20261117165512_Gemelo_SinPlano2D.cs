using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Gemelo_SinPlano2D : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElementoPlano",
                schema: "gem");

            migrationBuilder.DropTable(
                name: "TipoElementoPlano",
                schema: "cat");

            migrationBuilder.DropColumn(
                name: "AltoMuroM",
                schema: "gem",
                table: "Piso");

            migrationBuilder.DropColumn(
                name: "PlanoRuta",
                schema: "gem",
                table: "Piso");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AltoMuroM",
                schema: "gem",
                table: "Piso",
                type: "decimal(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 2.70m);

            migrationBuilder.AddColumn<string>(
                name: "PlanoRuta",
                schema: "gem",
                table: "Piso",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TipoElementoPlano",
                schema: "cat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", nullable: false),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoElementoPlano", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElementoPlano",
                schema: "gem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AltoM = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    EnBorrador = table.Column<bool>(type: "bit", nullable: false),
                    EstadoRegistro = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Geometria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PisoId = table.Column<int>(type: "int", nullable: false),
                    TipoElementoPlanoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioActualizacionId = table.Column<int>(type: "int", nullable: true),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    X1 = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    X2 = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Y1 = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Y2 = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementoPlano", x => x.Id);
                    table.CheckConstraint("CK_ElementoPlano_AltoM", "[AltoM] IS NULL OR [AltoM] > 0");
                    table.CheckConstraint("CK_ElementoPlano_Geometria", "[Geometria] IS NULL OR ISJSON([Geometria]) = 1");
                    table.CheckConstraint("CK_ElementoPlano_NombreAmbiente", "[TipoElementoPlanoId] <> 4 OR [Nombre] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_ElementoPlano_Piso_PisoId",
                        column: x => x.PisoId,
                        principalSchema: "gem",
                        principalTable: "Piso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElementoPlano_TipoElementoPlano_TipoElementoPlanoId",
                        column: x => x.TipoElementoPlanoId,
                        principalSchema: "cat",
                        principalTable: "TipoElementoPlano",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                schema: "gem",
                table: "Piso",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AltoMuroM", "PlanoRuta" },
                values: new object[] { 2.70m, "planos/piso1.jpg" });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "TipoElementoPlano",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[,]
                {
                    { 1, "MURO", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Muro", null, 1 },
                    { 2, "PUERTA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Puerta", null, 1 },
                    { 3, "VENTANA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Ventana", null, 1 },
                    { 4, "AMBIENTE", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Ambiente", null, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElementoPlano_PisoId",
                schema: "gem",
                table: "ElementoPlano",
                column: "PisoId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementoPlano_TipoElementoPlanoId",
                schema: "gem",
                table: "ElementoPlano",
                column: "TipoElementoPlanoId");

            migrationBuilder.CreateIndex(
                name: "IX_TipoElementoPlano_Codigo",
                schema: "cat",
                table: "TipoElementoPlano",
                column: "Codigo",
                unique: true);
        }
    }
}
