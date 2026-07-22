using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RolesYPermisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tenant_users_roles_rol_id",
                table: "tenant_users");

            migrationBuilder.DropIndex(
                name: "ix_tenant_users_rol_id",
                table: "tenant_users");

            migrationBuilder.DropIndex(
                name: "ix_roles_tenant_id_is_active",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_rol_permisos_rol_id_module_key",
                table: "rol_permisos");

            migrationBuilder.DropColumn(
                name: "rol_id",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "can_create",
                table: "rol_permisos");

            migrationBuilder.DropColumn(
                name: "can_delete",
                table: "rol_permisos");

            migrationBuilder.DropColumn(
                name: "can_edit",
                table: "rol_permisos");

            // OJO - correccion manual sobre lo que scaffolde EF.
            //
            // EF propuso RENOMBRAR tres columnas, y las tres son semanticamente distintas de su
            // supuesto origen. Un rename habria conservado los valores viejos bajo un nombre
            // nuevo que significa otra cosa:
            //   roles.is_active (bool "rol activo")      -> allow_rename ("rol de sistema
            //       renombrable"): no tienen NADA que ver; el estado del rol pasa a la columna
            //       'estado' (enum Activo/Inactivo) que se agrega mas abajo.
            //   rol_permisos.module_key                  -> modulo: mismo dato, pero la tabla
            //       cambia de forma (de 4 columnas booleanas a UNA FILA POR ACCION), asi que sus
            //       filas viejas no son convertibles fila a fila.
            //   rol_permisos.can_view (bool "puede ver") -> permitido ("la accion de ESTA fila
            //       esta concedida"): renombrarlo dejaria permitido=false en toda fila cuyo
            //       permiso no fuera 'ver', perdiendo el resto de la matriz.
            //
            // Por eso se DROPEAN y se crean limpias. Es viable porque TRONOX es green field (no
            // hay datos productivos que migrar) y porque los roles se re-siembran desde el alta
            // del tenant (RolProvisioningService, idempotente).
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "roles");

            // La matriz vieja no es convertible a la nueva forma fila-por-accion: se vacia y el
            // tenant vuelve a guardarla (o la recibe de la siembra, en los roles de gobierno).
            migrationBuilder.Sql("DELETE FROM rol_permisos;");

            migrationBuilder.DropColumn(
                name: "module_key",
                table: "rol_permisos");

            migrationBuilder.DropColumn(
                name: "can_view",
                table: "rol_permisos");

            migrationBuilder.AddColumn<bool>(
                name: "allow_rename",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "modulo",
                table: "rol_permisos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "permitido",
                table: "rol_permisos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "roles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "roles",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(600)",
                oldMaxLength: 600,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "codigo_sistema",
                table: "roles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            // "" no es un RolEstado valido: el enum se persiste como texto, asi que el valor por
            // defecto tiene que ser un miembro real o cualquier fila preexistente quedaria en un
            // estado que el modelo no sabe leer.
            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "roles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Activo");

            migrationBuilder.AddColumn<long>(
                name: "nivel_acceso_maximo_id",
                table: "roles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "accion",
                table: "rol_permisos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "usuarios_roles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_user_id = table.Column<long>(type: "bigint", nullable: false),
                    rol_id = table.Column<long>(type: "bigint", nullable: false),
                    vigente_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    vigente_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuarios_roles_roles_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usuarios_roles_tenant_users_tenant_user_id",
                        column: x => x.tenant_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_roles_nivel_acceso_maximo_id",
                table: "roles",
                column: "nivel_acceso_maximo_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id_codigo_sistema",
                table: "roles",
                columns: new[] { "tenant_id", "codigo_sistema" },
                unique: true,
                filter: "codigo_sistema IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id_estado",
                table: "roles",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_rol_permisos_rol_id",
                table: "rol_permisos",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "ix_rol_permisos_tenant_id_rol_id_modulo_accion",
                table: "rol_permisos",
                columns: new[] { "tenant_id", "rol_id", "modulo", "accion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_roles_rol_id",
                table: "usuarios_roles",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_roles_tenant_id_tenant_user_id",
                table: "usuarios_roles",
                columns: new[] { "tenant_id", "tenant_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_roles_tenant_id_tenant_user_id_rol_id",
                table: "usuarios_roles",
                columns: new[] { "tenant_id", "tenant_user_id", "rol_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_roles_tenant_user_id",
                table: "usuarios_roles",
                column: "tenant_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_roles_vigente_hasta",
                table: "usuarios_roles",
                column: "vigente_hasta");

            migrationBuilder.AddForeignKey(
                name: "fk_roles_niveles_clasificacion_nivel_acceso_maximo_id",
                table: "roles",
                column: "nivel_acceso_maximo_id",
                principalTable: "niveles_clasificacion",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_roles_niveles_clasificacion_nivel_acceso_maximo_id",
                table: "roles");

            migrationBuilder.DropTable(
                name: "usuarios_roles");

            migrationBuilder.DropIndex(
                name: "ix_roles_nivel_acceso_maximo_id",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_roles_tenant_id_codigo_sistema",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_roles_tenant_id_estado",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_rol_permisos_rol_id",
                table: "rol_permisos");

            migrationBuilder.DropIndex(
                name: "ix_rol_permisos_tenant_id_rol_id_modulo_accion",
                table: "rol_permisos");

            migrationBuilder.DropColumn(
                name: "codigo_sistema",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "nivel_acceso_maximo_id",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "accion",
                table: "rol_permisos");

            // Reverso simetrico de la correccion manual del Up: se dropean las columnas nuevas y
            // se recrean las viejas vacias (el rename inverso reintroduciria la misma confusion
            // semantica). La matriz vieja no se puede reconstruir: se vacia.
            migrationBuilder.Sql("DELETE FROM rol_permisos;");

            migrationBuilder.DropColumn(
                name: "allow_rename",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "permitido",
                table: "rol_permisos");

            migrationBuilder.DropColumn(
                name: "modulo",
                table: "rol_permisos");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "can_view",
                table: "rol_permisos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "module_key",
                table: "rol_permisos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "rol_id",
                table: "tenant_users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "roles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "roles",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "can_create",
                table: "rol_permisos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "can_delete",
                table: "rol_permisos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "can_edit",
                table: "rol_permisos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_rol_id",
                table: "tenant_users",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id_is_active",
                table: "roles",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_rol_permisos_rol_id_module_key",
                table: "rol_permisos",
                columns: new[] { "rol_id", "module_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_tenant_users_roles_rol_id",
                table: "tenant_users",
                column: "rol_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
