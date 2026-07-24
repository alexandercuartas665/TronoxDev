using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FuncionariosRf06 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "apellidos",
                table: "tenant_users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_vinculacion",
                table: "tenant_users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "firma_digital_id",
                table: "tenant_users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "firma_imagen_path",
                table: "tenant_users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nombres",
                table: "tenant_users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_documento",
                table: "tenant_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "sede_id",
                table: "tenant_users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_documento",
                table: "tenant_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_sede_id",
                table: "tenant_users",
                column: "sede_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_tenant_id_numero_documento",
                table: "tenant_users",
                columns: new[] { "tenant_id", "numero_documento" },
                unique: true,
                filter: "numero_documento IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_tenant_users_sedes_sede_id",
                table: "tenant_users",
                column: "sede_id",
                principalTable: "sedes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tenant_users_sedes_sede_id",
                table: "tenant_users");

            migrationBuilder.DropIndex(
                name: "ix_tenant_users_sede_id",
                table: "tenant_users");

            migrationBuilder.DropIndex(
                name: "ix_tenant_users_tenant_id_numero_documento",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "apellidos",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "fecha_vinculacion",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "firma_digital_id",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "firma_imagen_path",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "nombres",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "numero_documento",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "sede_id",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "tipo_documento",
                table: "tenant_users");
        }
    }
}
