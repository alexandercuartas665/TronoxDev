using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorreoRecibidoIaCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "clasificacion_json",
                table: "correos_recibidos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remitente_documento",
                table: "correos_recibidos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remitente_telefono",
                table: "correos_recibidos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tokens_ia",
                table: "correos_recibidos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "clasificacion_json",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "remitente_documento",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "remitente_telefono",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "tokens_ia",
                table: "correos_recibidos");
        }
    }
}
