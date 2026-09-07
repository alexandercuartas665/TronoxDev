using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FirmaCircuitos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "circuito_id",
                table: "firmas",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "firma_circuitos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    documento_id = table.Column<long>(type: "bigint", nullable: false),
                    modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_firmantes = table.Column<int>(type: "integer", nullable: false),
                    firmantes_completados = table.Column<int>(type: "integer", nullable: false),
                    tipo_firma_mixto = table.Column<bool>(type: "boolean", nullable: false),
                    otp_requerido = table.Column<bool>(type: "boolean", nullable: false),
                    solicitante_platform_user_id = table.Column<long>(type: "bigint", nullable: false),
                    solicitante_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    motivo_cancelacion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_firma_circuitos", x => x.id);
                    table.ForeignKey(
                        name: "fk_firma_circuitos_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "firma_circuito_firmantes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    circuito_id = table.Column<long>(type: "bigint", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    firmante_platform_user_id = table.Column<long>(type: "bigint", nullable: false),
                    nombre_firmante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cargo_firmante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tipo_firma = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    firma_id = table.Column<long>(type: "bigint", nullable: true),
                    timestamp_firma = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_firma_circuito_firmantes", x => x.id);
                    table.ForeignKey(
                        name: "fk_firma_circuito_firmantes_firma_circuitos_circuito_id",
                        column: x => x.circuito_id,
                        principalTable: "firma_circuitos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_firma_circuito_firmantes_circuito_id_orden",
                table: "firma_circuito_firmantes",
                columns: new[] { "circuito_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "ix_firma_circuito_firmantes_tenant_id_firmante_platform_user_i",
                table: "firma_circuito_firmantes",
                columns: new[] { "tenant_id", "firmante_platform_user_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_firma_circuitos_documento_id",
                table: "firma_circuitos",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_firma_circuitos_tenant_id_solicitante_platform_user_id_esta",
                table: "firma_circuitos",
                columns: new[] { "tenant_id", "solicitante_platform_user_id", "estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "firma_circuito_firmantes");

            migrationBuilder.DropTable(
                name: "firma_circuitos");

            migrationBuilder.DropColumn(
                name: "circuito_id",
                table: "firmas");
        }
    }
}
