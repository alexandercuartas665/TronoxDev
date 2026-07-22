using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EstructuraOrganizacional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                table: "org_units");

            migrationBuilder.AddColumn<long>(
                name: "cargo_org_unit_id",
                table: "tenant_users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "org_units",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AddColumn<string>(
                name: "codigo",
                table: "org_units",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "codigo_cargo",
                table: "org_units",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "codigo_dafp",
                table: "org_units",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "fondo_id",
                table: "org_units",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nivel_jerarquico",
                table: "org_units",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "sucesora_id",
                table: "org_units",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "vigente_desde",
                table: "org_units",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "vigente_hasta",
                table: "org_units",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_cargo_org_unit_id",
                table: "tenant_users",
                column: "cargo_org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_units_fondo_id",
                table: "org_units",
                column: "fondo_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_units_sucesora_id",
                table: "org_units",
                column: "sucesora_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_units_tenant_id_classifier",
                table: "org_units",
                columns: new[] { "tenant_id", "classifier" });

            migrationBuilder.CreateIndex(
                name: "ix_org_units_tenant_id_codigo",
                table: "org_units",
                columns: new[] { "tenant_id", "codigo" },
                unique: true,
                filter: "codigo IS NOT NULL AND parent_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_org_units_tenant_id_parent_id_codigo",
                table: "org_units",
                columns: new[] { "tenant_id", "parent_id", "codigo" },
                unique: true,
                filter: "codigo IS NOT NULL AND parent_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_org_units_fondos_fondo_id",
                table: "org_units",
                column: "fondo_id",
                principalTable: "fondos",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_org_units_org_units_sucesora_id",
                table: "org_units",
                column: "sucesora_id",
                principalTable: "org_units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tenant_users_org_units_cargo_org_unit_id",
                table: "tenant_users",
                column: "cargo_org_unit_id",
                principalTable: "org_units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_org_units_fondos_fondo_id",
                table: "org_units");

            migrationBuilder.DropForeignKey(
                name: "fk_org_units_org_units_sucesora_id",
                table: "org_units");

            migrationBuilder.DropForeignKey(
                name: "fk_tenant_users_org_units_cargo_org_unit_id",
                table: "tenant_users");

            migrationBuilder.DropIndex(
                name: "ix_tenant_users_cargo_org_unit_id",
                table: "tenant_users");

            migrationBuilder.DropIndex(
                name: "ix_org_units_fondo_id",
                table: "org_units");

            migrationBuilder.DropIndex(
                name: "ix_org_units_sucesora_id",
                table: "org_units");

            migrationBuilder.DropIndex(
                name: "ix_org_units_tenant_id_classifier",
                table: "org_units");

            migrationBuilder.DropIndex(
                name: "ix_org_units_tenant_id_codigo",
                table: "org_units");

            migrationBuilder.DropIndex(
                name: "ix_org_units_tenant_id_parent_id_codigo",
                table: "org_units");

            migrationBuilder.DropColumn(
                name: "cargo_org_unit_id",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "codigo",
                table: "org_units");

            migrationBuilder.DropColumn(
                name: "codigo_cargo",
                table: "org_units");

            migrationBuilder.DropColumn(
                name: "codigo_dafp",
                table: "org_units");

            migrationBuilder.DropColumn(
                name: "fondo_id",
                table: "org_units");

            migrationBuilder.DropColumn(
                name: "nivel_jerarquico",
                table: "org_units");

            migrationBuilder.DropColumn(
                name: "sucesora_id",
                table: "org_units");

            migrationBuilder.DropColumn(
                name: "vigente_desde",
                table: "org_units");

            migrationBuilder.DropColumn(
                name: "vigente_hasta",
                table: "org_units");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "org_units",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "org_units",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }
    }
}
