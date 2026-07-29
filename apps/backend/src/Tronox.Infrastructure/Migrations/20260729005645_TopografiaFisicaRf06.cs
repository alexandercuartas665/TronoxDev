using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TopografiaFisicaRf06 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "topografia_niveles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre_nivel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sigla_base = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    controla_capacidad = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topografia_niveles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "topografia_elementos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nivel_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sigla = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    capacidad = table.Column<int>(type: "integer", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topografia_elementos", x => x.id);
                    table.ForeignKey(
                        name: "fk_topografia_elementos_topografia_elementos_parent_id",
                        column: x => x.parent_id,
                        principalTable: "topografia_elementos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_topografia_elementos_topografia_niveles_nivel_id",
                        column: x => x.nivel_id,
                        principalTable: "topografia_niveles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_topografia_elementos_nivel_id",
                table: "topografia_elementos",
                column: "nivel_id");

            migrationBuilder.CreateIndex(
                name: "ix_topografia_elementos_parent_id",
                table: "topografia_elementos",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_topografia_elementos_tenant_id_parent_id",
                table: "topografia_elementos",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_topografia_elementos_tenant_id_parent_id_sigla",
                table: "topografia_elementos",
                columns: new[] { "tenant_id", "parent_id", "sigla" },
                unique: true,
                filter: "parent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_topografia_elementos_tenant_id_sigla",
                table: "topografia_elementos",
                columns: new[] { "tenant_id", "sigla" },
                unique: true,
                filter: "parent_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_topografia_niveles_tenant_id_nombre_nivel",
                table: "topografia_niveles",
                columns: new[] { "tenant_id", "nombre_nivel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_topografia_niveles_tenant_id_orden",
                table: "topografia_niveles",
                columns: new[] { "tenant_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_topografia_niveles_tenant_id_sigla_base",
                table: "topografia_niveles",
                columns: new[] { "tenant_id", "sigla_base" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "topografia_elementos");

            migrationBuilder.DropTable(
                name: "topografia_niveles");
        }
    }
}
