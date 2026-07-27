using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoSeriesRf02 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "series_documentales",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series_documentales", x => x.id);
                    table.ForeignKey(
                        name: "fk_series_documentales_series_documentales_parent_id",
                        column: x => x.parent_id,
                        principalTable: "series_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_series_documentales_parent_id",
                table: "series_documentales",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_series_documentales_tenant_id_codigo",
                table: "series_documentales",
                columns: new[] { "tenant_id", "codigo" },
                unique: true,
                filter: "parent_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_series_documentales_tenant_id_estado",
                table: "series_documentales",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_series_documentales_tenant_id_nombre",
                table: "series_documentales",
                columns: new[] { "tenant_id", "nombre" },
                unique: true,
                filter: "parent_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_series_documentales_tenant_id_parent_id",
                table: "series_documentales",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_series_documentales_tenant_id_parent_id_codigo",
                table: "series_documentales",
                columns: new[] { "tenant_id", "parent_id", "codigo" },
                unique: true,
                filter: "parent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_series_documentales_tenant_id_parent_id_nombre",
                table: "series_documentales",
                columns: new[] { "tenant_id", "parent_id", "nombre" },
                unique: true,
                filter: "parent_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "series_documentales");
        }
    }
}
