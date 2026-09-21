using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RadConfigSiglaIncluirAnio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "incluir_anio",
                table: "radicacion_configs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "sigla_radicacion",
                table: "radicacion_configs",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "incluir_anio",
                table: "radicacion_configs");

            migrationBuilder.DropColumn(
                name: "sigla_radicacion",
                table: "radicacion_configs");
        }
    }
}
