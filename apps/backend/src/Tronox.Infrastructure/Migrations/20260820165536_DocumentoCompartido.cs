using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentoCompartido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documentos_compartidos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    documento_id = table.Column<long>(type: "bigint", nullable: false),
                    beneficiario_platform_user_id = table.Column<long>(type: "bigint", nullable: false),
                    puede_ver = table.Column<bool>(type: "boolean", nullable: false),
                    puede_editar_metadatos = table.Column<bool>(type: "boolean", nullable: false),
                    puede_descargar = table.Column<bool>(type: "boolean", nullable: false),
                    origen_rol_id = table.Column<long>(type: "bigint", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    revocado_por = table.Column<long>(type: "bigint", nullable: true),
                    fecha_revocado = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_compartidos", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_compartidos_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_compartidos_documento_id_beneficiario_platform_u",
                table: "documentos_compartidos",
                columns: new[] { "documento_id", "beneficiario_platform_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documentos_compartidos_tenant_id_beneficiario_platform_user",
                table: "documentos_compartidos",
                columns: new[] { "tenant_id", "beneficiario_platform_user_id", "activo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documentos_compartidos");
        }
    }
}
