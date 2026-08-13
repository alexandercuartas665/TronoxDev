using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CalendarioHabilConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo",
                table: "dias_festivos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Nacional");

            // Los festivos propios ya existentes (agregados por la entidad) no son nacionales: su tipo por
            // defecto correcto es "Local", no el default de columna "Nacional".
            migrationBuilder.Sql("UPDATE dias_festivos SET tipo = 'Local' WHERE es_nacional = false;");

            migrationBuilder.CreateTable(
                name: "calendarios_habiles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lunes = table.Column<bool>(type: "boolean", nullable: false),
                    martes = table.Column<bool>(type: "boolean", nullable: false),
                    miercoles = table.Column<bool>(type: "boolean", nullable: false),
                    jueves = table.Column<bool>(type: "boolean", nullable: false),
                    viernes = table.Column<bool>(type: "boolean", nullable: false),
                    sabado = table.Column<bool>(type: "boolean", nullable: false),
                    domingo = table.Column<bool>(type: "boolean", nullable: false),
                    jornada_inicio = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    jornada_fin = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendarios_habiles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendarios_habiles_tenant_id",
                table: "calendarios_habiles",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendarios_habiles");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "dias_festivos");
        }
    }
}
