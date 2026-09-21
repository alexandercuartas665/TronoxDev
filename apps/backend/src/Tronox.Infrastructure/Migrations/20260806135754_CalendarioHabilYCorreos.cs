using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CalendarioHabilYCorreos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "confianza",
                table: "correos_recibidos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "cuerpo_tratado",
                table: "correos_recibidos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "duplicado_numero",
                table: "correos_recibidos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "in_reply_to",
                table: "correos_recibidos",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "message_id",
                table: "correos_recibidos",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modo",
                table: "correos_recibidos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "num_adjuntos",
                table: "correos_recibidos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "radica_en",
                table: "correos_recibidos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "radicado_numero",
                table: "correos_recibidos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "radicado_ref",
                table: "correos_recibidos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remitente_email",
                table: "correos_recibidos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "tipo_detectado_id",
                table: "correos_recibidos",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "correos_descartados",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    correo_recibido_id = table.Column<long>(type: "bigint", nullable: false),
                    usuario_id = table.Column<long>(type: "bigint", nullable: true),
                    causal = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recuperado = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_recupera = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_recupera_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_correos_descartados", x => x.id);
                    table.ForeignKey(
                        name: "fk_correos_descartados_correos_recibidos_correo_recibido_id",
                        column: x => x.correo_recibido_id,
                        principalTable: "correos_recibidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "correos_recibidos_adjuntos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    correo_recibido_id = table.Column<long>(type: "bigint", nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    extension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mime_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    es_cuerpo_html = table.Column<bool>(type: "boolean", nullable: false),
                    es_hilo = table.Column<bool>(type: "boolean", nullable: false),
                    storage_bucket = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    storage_key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_correos_recibidos_adjuntos", x => x.id);
                    table.ForeignKey(
                        name: "fk_correos_recibidos_adjuntos_correos_recibidos_correo_recibid",
                        column: x => x.correo_recibido_id,
                        principalTable: "correos_recibidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dias_festivos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    es_nacional = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dias_festivos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_correos_recibidos_tenant_id_message_id",
                table: "correos_recibidos",
                columns: new[] { "tenant_id", "message_id" });

            migrationBuilder.CreateIndex(
                name: "ix_correos_recibidos_tipo_detectado_id",
                table: "correos_recibidos",
                column: "tipo_detectado_id");

            migrationBuilder.CreateIndex(
                name: "ix_correos_descartados_correo_recibido_id",
                table: "correos_descartados",
                column: "correo_recibido_id");

            migrationBuilder.CreateIndex(
                name: "ix_correos_descartados_tenant_id_correo_recibido_id",
                table: "correos_descartados",
                columns: new[] { "tenant_id", "correo_recibido_id" });

            migrationBuilder.CreateIndex(
                name: "ix_correos_recibidos_adjuntos_correo_recibido_id",
                table: "correos_recibidos_adjuntos",
                column: "correo_recibido_id");

            migrationBuilder.CreateIndex(
                name: "ix_correos_recibidos_adjuntos_tenant_id_correo_recibido_id",
                table: "correos_recibidos_adjuntos",
                columns: new[] { "tenant_id", "correo_recibido_id" });

            migrationBuilder.CreateIndex(
                name: "ix_dias_festivos_tenant_id_fecha",
                table: "dias_festivos",
                columns: new[] { "tenant_id", "fecha" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_correos_recibidos_tipos_comunicacion_tipo_detectado_id",
                table: "correos_recibidos",
                column: "tipo_detectado_id",
                principalTable: "tipos_comunicacion",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_correos_recibidos_tipos_comunicacion_tipo_detectado_id",
                table: "correos_recibidos");

            migrationBuilder.DropTable(
                name: "correos_descartados");

            migrationBuilder.DropTable(
                name: "correos_recibidos_adjuntos");

            migrationBuilder.DropTable(
                name: "dias_festivos");

            migrationBuilder.DropIndex(
                name: "ix_correos_recibidos_tenant_id_message_id",
                table: "correos_recibidos");

            migrationBuilder.DropIndex(
                name: "ix_correos_recibidos_tipo_detectado_id",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "confianza",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "cuerpo_tratado",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "duplicado_numero",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "in_reply_to",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "message_id",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "modo",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "num_adjuntos",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "radica_en",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "radicado_numero",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "radicado_ref",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "remitente_email",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "tipo_detectado_id",
                table: "correos_recibidos");
        }
    }
}
