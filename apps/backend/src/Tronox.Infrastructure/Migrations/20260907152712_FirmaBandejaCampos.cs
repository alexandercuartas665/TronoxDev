using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FirmaBandejaCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "comentario_rechazo",
                table: "firmas",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_limite",
                table: "firmas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instrucciones",
                table: "firmas",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prioridad",
                table: "firmas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Media");

            migrationBuilder.AddColumn<string>(
                name: "tag",
                table: "firmas",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_firmas_tenant_id_solicitado_por_estado",
                table: "firmas",
                columns: new[] { "tenant_id", "solicitado_por", "estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_firmas_tenant_id_solicitado_por_estado",
                table: "firmas");

            migrationBuilder.DropColumn(
                name: "comentario_rechazo",
                table: "firmas");

            migrationBuilder.DropColumn(
                name: "fecha_limite",
                table: "firmas");

            migrationBuilder.DropColumn(
                name: "instrucciones",
                table: "firmas");

            migrationBuilder.DropColumn(
                name: "prioridad",
                table: "firmas");

            migrationBuilder.DropColumn(
                name: "tag",
                table: "firmas");
        }
    }
}
