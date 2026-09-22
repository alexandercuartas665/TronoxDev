using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RadicadoRemitenteTercero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "remitente_tercero_id",
                table: "radicados",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_radicados_remitente_tercero_id",
                table: "radicados",
                column: "remitente_tercero_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tenant_id_remitente_tercero_id",
                table: "radicados",
                columns: new[] { "tenant_id", "remitente_tercero_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_radicados_terceros_remitente_tercero_id",
                table: "radicados",
                column: "remitente_tercero_id",
                principalTable: "terceros",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_radicados_terceros_remitente_tercero_id",
                table: "radicados");

            migrationBuilder.DropIndex(
                name: "ix_radicados_remitente_tercero_id",
                table: "radicados");

            migrationBuilder.DropIndex(
                name: "ix_radicados_tenant_id_remitente_tercero_id",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "remitente_tercero_id",
                table: "radicados");
        }
    }
}
