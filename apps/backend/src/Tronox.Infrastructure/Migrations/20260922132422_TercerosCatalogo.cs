using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TercerosCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "terceros",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    subtipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    numero_documento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    digito_verificador = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    razon_social = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    apellidos = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    nombre_comercial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    municipio_id = table.Column<long>(type: "bigint", nullable: true),
                    pais_id = table.Column<long>(type: "bigint", nullable: true),
                    sitio_web = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sector_economico = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    regimen_tributario = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sector_administrativo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    orden_entidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    naturaleza_juridica = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    representante_legal_id = table.Column<long>(type: "bigint", nullable: true),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    perfil_completo = table.Column<bool>(type: "boolean", nullable: false),
                    observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    motivo_inactivacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    origen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terceros", x => x.id);
                    table.ForeignKey(
                        name: "fk_terceros_municipios_municipio_id",
                        column: x => x.municipio_id,
                        principalTable: "municipios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_terceros_paises_pais_id",
                        column: x => x.pais_id,
                        principalTable: "paises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_terceros_terceros_representante_legal_id",
                        column: x => x.representante_legal_id,
                        principalTable: "terceros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_terceros_municipio_id",
                table: "terceros",
                column: "municipio_id");

            migrationBuilder.CreateIndex(
                name: "ix_terceros_pais_id",
                table: "terceros",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_terceros_representante_legal_id",
                table: "terceros",
                column: "representante_legal_id");

            migrationBuilder.CreateIndex(
                name: "ix_terceros_tenant_id_estado",
                table: "terceros",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_terceros_tenant_id_tipo_documento_numero_documento",
                table: "terceros",
                columns: new[] { "tenant_id", "tipo_documento", "numero_documento" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terceros");
        }
    }
}
