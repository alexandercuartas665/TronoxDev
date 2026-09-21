using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FirmaOtpLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lote_id",
                table: "firma_otps",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_firma_otps_lote_id_verificado_at",
                table: "firma_otps",
                columns: new[] { "lote_id", "verificado_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_firma_otps_lote_id_verificado_at",
                table: "firma_otps");

            migrationBuilder.DropColumn(
                name: "lote_id",
                table: "firma_otps");
        }
    }
}
