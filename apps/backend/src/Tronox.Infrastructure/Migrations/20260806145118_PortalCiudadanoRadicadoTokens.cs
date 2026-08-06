using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PortalCiudadanoRadicadoTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_respuesta_publica",
                table: "radicados",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "portal_token",
                table: "radicados",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "respuesta_publica",
                table: "radicados",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "es_respuesta_publica",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "portal_token",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "respuesta_publica",
                table: "radicados");
        }
    }
}
