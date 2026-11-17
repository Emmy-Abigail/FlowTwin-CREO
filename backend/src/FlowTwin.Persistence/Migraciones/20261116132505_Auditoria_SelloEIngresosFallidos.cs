using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Auditoria_SelloEIngresosFallidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sello",
                schema: "aud",
                columns: table => new
                {
                    AuditoriaId = table.Column<long>(type: "bigint", nullable: false),
                    Hash = table.Column<byte[]>(type: "varbinary(32)", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioRegistroId = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sello", x => x.AuditoriaId);
                    table.CheckConstraint("CK_Sello_Hash", "DATALENGTH([Hash]) = 32");
                    table.ForeignKey(
                        name: "FK_Sello_Auditoria_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalSchema: "aud",
                        principalTable: "Auditoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "cat",
                table: "AccionAuditoria",
                columns: new[] { "Id", "Codigo", "EstadoRegistro", "FechaActualizacion", "FechaRegistro", "Nombre", "UsuarioActualizacionId", "UsuarioRegistroId" },
                values: new object[,]
                {
                    { 21, "INICIO_FALLIDO", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Intento de ingreso fallido", null, 1 },
                    { 22, "CUENTA_BLOQUEADA", true, null, new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc), "Cuenta bloqueada", null, 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sello",
                schema: "aud");

            migrationBuilder.DeleteData(
                schema: "cat",
                table: "AccionAuditoria",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                schema: "cat",
                table: "AccionAuditoria",
                keyColumn: "Id",
                keyValue: 22);
        }
    }
}
