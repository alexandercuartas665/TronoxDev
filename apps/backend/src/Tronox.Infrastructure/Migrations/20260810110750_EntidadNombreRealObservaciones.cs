using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntidadNombreRealObservaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "nombre_real",
                table: "entidades",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observaciones",
                table: "entidades",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "nombre_real",
                table: "entidades");

            migrationBuilder.DropColumn(
                name: "observaciones",
                table: "entidades");
        }
    }
}
