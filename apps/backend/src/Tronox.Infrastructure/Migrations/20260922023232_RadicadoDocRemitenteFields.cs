using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RadicadoDocRemitenteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_documento",
                table: "radicados",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observaciones",
                table: "radicados",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remitente_municipio",
                table: "radicados",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fecha_documento",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "observaciones",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "remitente_municipio",
                table: "radicados");
        }
    }
}
