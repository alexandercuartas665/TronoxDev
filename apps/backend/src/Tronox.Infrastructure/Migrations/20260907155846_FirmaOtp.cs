using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FirmaOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "firma_otps",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    firma_id = table.Column<long>(type: "bigint", nullable: false),
                    usuario = table.Column<long>(type: "bigint", nullable: false),
                    codigo_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expira_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    verificado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_firma_otps", x => x.id);
                    table.ForeignKey(
                        name: "fk_firma_otps_firmas_firma_id",
                        column: x => x.firma_id,
                        principalTable: "firmas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_firma_otps_firma_id_verificado_at",
                table: "firma_otps",
                columns: new[] { "firma_id", "verificado_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "firma_otps");
        }
    }
}
