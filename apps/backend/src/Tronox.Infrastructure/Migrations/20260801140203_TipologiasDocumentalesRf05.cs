using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TipologiasDocumentalesRf05 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "trd_tipologia_id",
                table: "trd_metadatos",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trd_tipologias",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trd_asignacion_id = table.Column<long>(type: "bigint", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    soporte = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    formato = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    obligatorio_en_expediente = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trd_tipologias", x => x.id);
                    table.ForeignKey(
                        name: "fk_trd_tipologias_trd_asignaciones_trd_asignacion_id",
                        column: x => x.trd_asignacion_id,
                        principalTable: "trd_asignaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trd_metadatos_trd_tipologia_id",
                table: "trd_metadatos",
                column: "trd_tipologia_id");

            migrationBuilder.CreateIndex(
                name: "ix_trd_tipologias_trd_asignacion_id_nombre",
                table: "trd_tipologias",
                columns: new[] { "trd_asignacion_id", "nombre" });

            migrationBuilder.AddForeignKey(
                name: "fk_trd_metadatos_trd_tipologias_trd_tipologia_id",
                table: "trd_metadatos",
                column: "trd_tipologia_id",
                principalTable: "trd_tipologias",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_trd_metadatos_trd_tipologias_trd_tipologia_id",
                table: "trd_metadatos");

            migrationBuilder.DropTable(
                name: "trd_tipologias");

            migrationBuilder.DropIndex(
                name: "ix_trd_metadatos_trd_tipologia_id",
                table: "trd_metadatos");

            migrationBuilder.DropColumn(
                name: "trd_tipologia_id",
                table: "trd_metadatos");
        }
    }
}
