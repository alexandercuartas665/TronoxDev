using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VersionesTrdRf01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trd_versiones",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    acto_administrativo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha_vigencia_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_aprobacion = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_convalidacion = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trd_versiones", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trd_versiones_tenant_id_codigo_version",
                table: "trd_versiones",
                columns: new[] { "tenant_id", "codigo_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trd_versiones_tenant_id_estado",
                table: "trd_versiones",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ux_trd_versiones_una_vigente_por_tenant",
                table: "trd_versiones",
                column: "tenant_id",
                unique: true,
                filter: "estado = 'Vigente'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trd_versiones");
        }
    }
}
