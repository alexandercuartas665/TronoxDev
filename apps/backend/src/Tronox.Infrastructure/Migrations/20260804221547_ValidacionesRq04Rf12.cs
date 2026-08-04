using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ValidacionesRq04Rf12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documento_validaciones",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    documento_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    usuario_asignado_id = table.Column<long>(type: "bigint", nullable: false),
                    nombre_asignado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cargo_asignado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prioridad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_limite = table.Column<DateOnly>(type: "date", nullable: true),
                    instrucciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    comentarios = table.Column<string>(type: "text", nullable: true),
                    fecha_respuesta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documento_validaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_documento_validaciones_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_documento_validaciones_tenant_users_usuario_asignado_id",
                        column: x => x.usuario_asignado_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documento_validaciones_documento_id",
                table: "documento_validaciones",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_documento_validaciones_tenant_id_usuario_asignado_id_estado",
                table: "documento_validaciones",
                columns: new[] { "tenant_id", "usuario_asignado_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_documento_validaciones_usuario_asignado_id",
                table: "documento_validaciones",
                column: "usuario_asignado_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documento_validaciones");
        }
    }
}
