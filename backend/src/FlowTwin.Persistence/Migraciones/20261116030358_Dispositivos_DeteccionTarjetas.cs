using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowTwin.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Dispositivos_DeteccionTarjetas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tarjeta_SedeId",
                schema: "dis",
                table: "Tarjeta");

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaDeteccionEn",
                schema: "dis",
                table: "Tarjeta",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UltimoNodoId",
                schema: "dis",
                table: "Tarjeta",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tarjeta_SedeId_EstadoTarjetaId",
                schema: "dis",
                table: "Tarjeta",
                columns: new[] { "SedeId", "EstadoTarjetaId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tarjeta_UltimoNodoId",
                schema: "dis",
                table: "Tarjeta",
                column: "UltimoNodoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tarjeta_Nodo_UltimoNodoId",
                schema: "dis",
                table: "Tarjeta",
                column: "UltimoNodoId",
                principalSchema: "dis",
                principalTable: "Nodo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tarjeta_Nodo_UltimoNodoId",
                schema: "dis",
                table: "Tarjeta");

            migrationBuilder.DropIndex(
                name: "IX_Tarjeta_SedeId_EstadoTarjetaId",
                schema: "dis",
                table: "Tarjeta");

            migrationBuilder.DropIndex(
                name: "IX_Tarjeta_UltimoNodoId",
                schema: "dis",
                table: "Tarjeta");

            migrationBuilder.DropColumn(
                name: "UltimaDeteccionEn",
                schema: "dis",
                table: "Tarjeta");

            migrationBuilder.DropColumn(
                name: "UltimoNodoId",
                schema: "dis",
                table: "Tarjeta");

            migrationBuilder.CreateIndex(
                name: "IX_Tarjeta_SedeId",
                schema: "dis",
                table: "Tarjeta",
                column: "SedeId");
        }
    }
}
