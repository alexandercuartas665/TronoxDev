using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FirmaElectronica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "firmas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    documento_id = table.Column<long>(type: "bigint", nullable: false),
                    firmante_user_id = table.Column<long>(type: "bigint", nullable: false),
                    nombre_firmante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cargo_firmante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    dependencia_firmante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tipo_firma = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hash_documento = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    timestamp_firma = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ip_firma = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sesion_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    otp_requerido = table.Column<bool>(type: "boolean", nullable: false),
                    solicitado_por = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_firmas", x => x.id);
                    table.ForeignKey(
                        name: "fk_firmas_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_firmas_documento_id",
                table: "firmas",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_firmas_tenant_id_firmante_user_id_estado",
                table: "firmas",
                columns: new[] { "tenant_id", "firmante_user_id", "estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "firmas");
        }
    }
}
