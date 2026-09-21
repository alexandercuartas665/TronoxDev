using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpedienteCierreUbicacionVinculos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expediente_cierres",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expediente_id = table.Column<long>(type: "bigint", nullable: false),
                    numero_cierre = table.Column<int>(type: "integer", nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    firma_digital_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    justificacion_reapertura = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expediente_cierres", x => x.id);
                    table.ForeignKey(
                        name: "fk_expediente_cierres_expedientes_expediente_id",
                        column: x => x.expediente_id,
                        principalTable: "expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expediente_ubicaciones",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expediente_id = table.Column<long>(type: "bigint", nullable: false),
                    topografia_elemento_id = table.Column<long>(type: "bigint", nullable: false),
                    fase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expediente_ubicaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_expediente_ubicaciones_expedientes_expediente_id",
                        column: x => x.expediente_id,
                        principalTable: "expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expediente_ubicaciones_topografia_elementos_topografia_elem",
                        column: x => x.topografia_elemento_id,
                        principalTable: "topografia_elementos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expediente_vinculos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expediente_origen_id = table.Column<long>(type: "bigint", nullable: false),
                    expediente_destino_id = table.Column<long>(type: "bigint", nullable: false),
                    observacion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expediente_vinculos", x => x.id);
                    table.ForeignKey(
                        name: "fk_expediente_vinculos_expedientes_expediente_destino_id",
                        column: x => x.expediente_destino_id,
                        principalTable: "expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expediente_vinculos_expedientes_expediente_origen_id",
                        column: x => x.expediente_origen_id,
                        principalTable: "expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expediente_cierres_expediente_id_numero_cierre",
                table: "expediente_cierres",
                columns: new[] { "expediente_id", "numero_cierre" });

            migrationBuilder.CreateIndex(
                name: "ix_expediente_ubicaciones_expediente_id_created_at",
                table: "expediente_ubicaciones",
                columns: new[] { "expediente_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_expediente_ubicaciones_topografia_elemento_id",
                table: "expediente_ubicaciones",
                column: "topografia_elemento_id");

            migrationBuilder.CreateIndex(
                name: "ix_expediente_vinculos_expediente_destino_id",
                table: "expediente_vinculos",
                column: "expediente_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_expediente_vinculos_expediente_origen_id",
                table: "expediente_vinculos",
                column: "expediente_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_expediente_vinculos_tenant_id_activo",
                table: "expediente_vinculos",
                columns: new[] { "tenant_id", "activo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expediente_cierres");

            migrationBuilder.DropTable(
                name: "expediente_ubicaciones");

            migrationBuilder.DropTable(
                name: "expediente_vinculos");
        }
    }
}
