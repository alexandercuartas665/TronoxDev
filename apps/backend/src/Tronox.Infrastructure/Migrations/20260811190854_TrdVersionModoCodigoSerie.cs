using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrdVersionModoCodigoSerie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "modo_codigo_serie",
                table: "trd_versiones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "CalcularCodigo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "modo_codigo_serie",
                table: "trd_versiones");
        }
    }
}
