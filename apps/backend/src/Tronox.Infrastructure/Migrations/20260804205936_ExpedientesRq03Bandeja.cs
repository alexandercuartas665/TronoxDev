using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpedientesRq03Bandeja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expedientes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trd_asignacion_id = table.Column<long>(type: "bigint", nullable: false),
                    nivel_clasificacion_id = table.Column<long>(type: "bigint", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado_ubicacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_apertura = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_cierre = table.Column<DateOnly>(type: "date", nullable: true),
                    eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_eliminacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    eliminado_por_user_id = table.Column<long>(type: "bigint", nullable: true),
                    justificacion_eliminacion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expedientes", x => x.id);
                    table.ForeignKey(
                        name: "fk_expedientes_niveles_clasificacion_nivel_clasificacion_id",
                        column: x => x.nivel_clasificacion_id,
                        principalTable: "niveles_clasificacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expedientes_trd_asignaciones_trd_asignacion_id",
                        column: x => x.trd_asignacion_id,
                        principalTable: "trd_asignaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expediente_metadatos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expediente_id = table.Column<long>(type: "bigint", nullable: false),
                    trd_metadato_id = table.Column<long>(type: "bigint", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expediente_metadatos", x => x.id);
                    table.ForeignKey(
                        name: "fk_expediente_metadatos_expedientes_expediente_id",
                        column: x => x.expediente_id,
                        principalTable: "expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_expediente_metadatos_trd_metadatos_trd_metadato_id",
                        column: x => x.trd_metadato_id,
                        principalTable: "trd_metadatos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expediente_metadatos_expediente_id_trd_metadato_id",
                table: "expediente_metadatos",
                columns: new[] { "expediente_id", "trd_metadato_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expediente_metadatos_trd_metadato_id",
                table: "expediente_metadatos",
                column: "trd_metadato_id");

            migrationBuilder.CreateIndex(
                name: "ix_expedientes_nivel_clasificacion_id",
                table: "expedientes",
                column: "nivel_clasificacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_expedientes_tenant_id_codigo",
                table: "expedientes",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expedientes_tenant_id_estado",
                table: "expedientes",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_expedientes_tenant_id_fase",
                table: "expedientes",
                columns: new[] { "tenant_id", "fase" });

            migrationBuilder.CreateIndex(
                name: "ix_expedientes_trd_asignacion_id",
                table: "expedientes",
                column: "trd_asignacion_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expediente_metadatos");

            migrationBuilder.DropTable(
                name: "expedientes");
        }
    }
}
