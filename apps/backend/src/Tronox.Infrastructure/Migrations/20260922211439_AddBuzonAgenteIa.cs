using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBuzonAgenteIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "agente_ia_id",
                table: "buzones_correo",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_buzones_correo_agente_ia_id",
                table: "buzones_correo",
                column: "agente_ia_id");

            migrationBuilder.AddForeignKey(
                name: "fk_buzones_correo_ai_agents_agente_ia_id",
                table: "buzones_correo",
                column: "agente_ia_id",
                principalTable: "ai_agents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_buzones_correo_ai_agents_agente_ia_id",
                table: "buzones_correo");

            migrationBuilder.DropIndex(
                name: "ix_buzones_correo_agente_ia_id",
                table: "buzones_correo");

            migrationBuilder.DropColumn(
                name: "agente_ia_id",
                table: "buzones_correo");
        }
    }
}
