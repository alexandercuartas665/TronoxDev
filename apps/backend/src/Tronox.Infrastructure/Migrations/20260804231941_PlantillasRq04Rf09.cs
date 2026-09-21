using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlantillasRq04Rf09 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plantillas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    contenido_html = table.Column<string>(type: "text", nullable: true),
                    trd_tipologia_id = table.Column<long>(type: "bigint", nullable: true),
                    formato_papel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    orientacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    margenes = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    encabezado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    pie_pagina = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    variables_num = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uso_contador = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plantillas", x => x.id);
                    table.ForeignKey(
                        name: "fk_plantillas_trd_tipologias_trd_tipologia_id",
                        column: x => x.trd_tipologia_id,
                        principalTable: "trd_tipologias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plantilla_tipos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plantilla_id = table.Column<long>(type: "bigint", nullable: false),
                    trd_tipologia_id = table.Column<long>(type: "bigint", nullable: false),
                    tipologia_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plantilla_tipos", x => x.id);
                    table.ForeignKey(
                        name: "fk_plantilla_tipos_plantillas_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "plantillas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_plantilla_tipos_trd_tipologias_trd_tipologia_id",
                        column: x => x.trd_tipologia_id,
                        principalTable: "trd_tipologias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_plantilla_tipos_plantilla_id_trd_tipologia_id",
                table: "plantilla_tipos",
                columns: new[] { "plantilla_id", "trd_tipologia_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plantilla_tipos_trd_tipologia_id",
                table: "plantilla_tipos",
                column: "trd_tipologia_id");

            migrationBuilder.CreateIndex(
                name: "ix_plantillas_tenant_id_estado",
                table: "plantillas",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_plantillas_trd_tipologia_id",
                table: "plantillas",
                column: "trd_tipologia_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plantilla_tipos");

            migrationBuilder.DropTable(
                name: "plantillas");
        }
    }
}
