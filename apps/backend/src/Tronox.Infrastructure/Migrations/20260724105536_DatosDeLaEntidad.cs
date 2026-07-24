using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DatosDeLaEntidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "paises",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo_iso2 = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    codigo_iso3 = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paises", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "departamentos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pais_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo_dane = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departamentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_departamentos_paises_pais_id",
                        column: x => x.pais_id,
                        principalTable: "paises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "municipios",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    departamento_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo_divipola = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    es_capital = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipios", x => x.id);
                    table.ForeignKey(
                        name: "fk_municipios_departamentos_departamento_id",
                        column: x => x.departamento_id,
                        principalTable: "departamentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entidades",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nit = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    digito_verificacion = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sigla = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tipo_entidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    naturaleza_juridica = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    codigo_divipola = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    pais_id = table.Column<long>(type: "bigint", nullable: true),
                    departamento_id = table.Column<long>(type: "bigint", nullable: true),
                    ciudad_id = table.Column<long>(type: "bigint", nullable: true),
                    direccion_principal = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    correo_institucional = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    pagina_web = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    representante_legal = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    codigo_fondo_agn = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    zona_horaria = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    idioma_defecto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entidades", x => x.id);
                    table.ForeignKey(
                        name: "fk_entidades_departamentos_departamento_id",
                        column: x => x.departamento_id,
                        principalTable: "departamentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entidades_municipios_ciudad_id",
                        column: x => x.ciudad_id,
                        principalTable: "municipios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entidades_paises_pais_id",
                        column: x => x.pais_id,
                        principalTable: "paises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "paises",
                columns: new[] { "id", "activo", "codigo_iso2", "codigo_iso3", "created_at", "created_by", "nombre", "updated_at", "updated_by" },
                values: new object[] { 1L, true, "CO", "COL", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Colombia", null, null });

            migrationBuilder.InsertData(
                table: "departamentos",
                columns: new[] { "id", "activo", "codigo_dane", "created_at", "created_by", "nombre", "pais_id", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { 1L, true, "05", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Antioquia", 1L, null, null },
                    { 2L, true, "08", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Atl\u00e1ntico", 1L, null, null },
                    { 3L, true, "11", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Bogot\u00e1, D.C.", 1L, null, null },
                    { 4L, true, "13", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Bol\u00edvar", 1L, null, null },
                    { 5L, true, "15", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Boyac\u00e1", 1L, null, null },
                    { 6L, true, "17", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Caldas", 1L, null, null },
                    { 7L, true, "18", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Caquet\u00e1", 1L, null, null },
                    { 8L, true, "19", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Cauca", 1L, null, null },
                    { 9L, true, "20", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Cesar", 1L, null, null },
                    { 10L, true, "23", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "C\u00f3rdoba", 1L, null, null },
                    { 11L, true, "25", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Cundinamarca", 1L, null, null },
                    { 12L, true, "27", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Choc\u00f3", 1L, null, null },
                    { 13L, true, "41", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Huila", 1L, null, null },
                    { 14L, true, "44", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "La Guajira", 1L, null, null },
                    { 15L, true, "47", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Magdalena", 1L, null, null },
                    { 16L, true, "50", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Meta", 1L, null, null },
                    { 17L, true, "52", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Nari\u00f1o", 1L, null, null },
                    { 18L, true, "54", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Norte de Santander", 1L, null, null },
                    { 19L, true, "63", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Quind\u00edo", 1L, null, null },
                    { 20L, true, "66", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Risaralda", 1L, null, null },
                    { 21L, true, "68", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Santander", 1L, null, null },
                    { 22L, true, "70", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Sucre", 1L, null, null },
                    { 23L, true, "73", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Tolima", 1L, null, null },
                    { 24L, true, "76", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Valle del Cauca", 1L, null, null },
                    { 25L, true, "81", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Arauca", 1L, null, null },
                    { 26L, true, "85", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Casanare", 1L, null, null },
                    { 27L, true, "86", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Putumayo", 1L, null, null },
                    { 28L, true, "88", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Archipi\u00e9lago de San Andr\u00e9s, Providencia y Santa Catalina", 1L, null, null },
                    { 29L, true, "91", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Amazonas", 1L, null, null },
                    { 30L, true, "94", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Guain\u00eda", 1L, null, null },
                    { 31L, true, "95", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Guaviare", 1L, null, null },
                    { 32L, true, "97", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Vaup\u00e9s", 1L, null, null },
                    { 33L, true, "99", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Vichada", 1L, null, null }
                });

            migrationBuilder.InsertData(
                table: "municipios",
                columns: new[] { "id", "activo", "codigo_divipola", "created_at", "created_by", "departamento_id", "es_capital", "nombre", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { 1L, true, "05001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1L, true, "Medell\u00edn", null, null },
                    { 2L, true, "08001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 2L, true, "Barranquilla", null, null },
                    { 3L, true, "11001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 3L, true, "Bogot\u00e1, D.C.", null, null },
                    { 4L, true, "13001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 4L, true, "Cartagena de Indias", null, null },
                    { 5L, true, "15001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 5L, true, "Tunja", null, null },
                    { 6L, true, "17001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 6L, true, "Manizales", null, null },
                    { 7L, true, "18001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 7L, true, "Florencia", null, null },
                    { 8L, true, "19001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 8L, true, "Popay\u00e1n", null, null },
                    { 9L, true, "20001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 9L, true, "Valledupar", null, null },
                    { 10L, true, "23001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 10L, true, "Monter\u00eda", null, null },
                    { 11L, true, "27001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 12L, true, "Quibd\u00f3", null, null },
                    { 12L, true, "41001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 13L, true, "Neiva", null, null },
                    { 13L, true, "44001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 14L, true, "Riohacha", null, null },
                    { 14L, true, "47001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 15L, true, "Santa Marta", null, null },
                    { 15L, true, "50001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 16L, true, "Villavicencio", null, null },
                    { 16L, true, "52001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 17L, true, "Pasto", null, null },
                    { 17L, true, "54001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 18L, true, "C\u00facuta", null, null },
                    { 18L, true, "63001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 19L, true, "Armenia", null, null },
                    { 19L, true, "66001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 20L, true, "Pereira", null, null },
                    { 20L, true, "68001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 21L, true, "Bucaramanga", null, null },
                    { 21L, true, "70001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 22L, true, "Sincelejo", null, null },
                    { 22L, true, "73001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 23L, true, "Ibagu\u00e9", null, null },
                    { 23L, true, "76001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 24L, true, "Cali", null, null },
                    { 24L, true, "81001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 25L, true, "Arauca", null, null },
                    { 25L, true, "85001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 26L, true, "Yopal", null, null },
                    { 26L, true, "86001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 27L, true, "Mocoa", null, null },
                    { 27L, true, "88001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 28L, true, "San Andr\u00e9s", null, null },
                    { 28L, true, "91001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 29L, true, "Leticia", null, null },
                    { 29L, true, "94001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 30L, true, "In\u00edrida", null, null },
                    { 30L, true, "95001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 31L, true, "San Jos\u00e9 del Guaviare", null, null },
                    { 31L, true, "97001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 32L, true, "Mit\u00fa", null, null },
                    { 32L, true, "99001", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 33L, true, "Puerto Carre\u00f1o", null, null },
                    { 33L, true, "25754", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 11L, false, "Soacha", null, null },
                    { 34L, true, "25899", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 11L, false, "Zipaquir\u00e1", null, null },
                    { 35L, true, "25175", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 11L, false, "Ch\u00eda", null, null },
                    { 36L, true, "25290", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 11L, false, "Fusagasug\u00e1", null, null },
                    { 37L, true, "25307", new DateTimeOffset(new DateTime(2026, 7, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 11L, false, "Girardot", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_sedes_ciudad_id",
                table: "sedes",
                column: "ciudad_id");

            migrationBuilder.CreateIndex(
                name: "ix_sedes_departamento_id",
                table: "sedes",
                column: "departamento_id");

            migrationBuilder.CreateIndex(
                name: "ix_sedes_pais_id",
                table: "sedes",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_departamentos_pais_id_codigo_dane",
                table: "departamentos",
                columns: new[] { "pais_id", "codigo_dane" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entidades_ciudad_id",
                table: "entidades",
                column: "ciudad_id");

            migrationBuilder.CreateIndex(
                name: "ix_entidades_departamento_id",
                table: "entidades",
                column: "departamento_id");

            migrationBuilder.CreateIndex(
                name: "ix_entidades_pais_id",
                table: "entidades",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_entidades_tenant_id",
                table: "entidades",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entidades_tenant_id_nit",
                table: "entidades",
                columns: new[] { "tenant_id", "nit" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_municipios_codigo_divipola",
                table: "municipios",
                column: "codigo_divipola",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_municipios_departamento_id",
                table: "municipios",
                column: "departamento_id");

            migrationBuilder.CreateIndex(
                name: "ix_paises_codigo_iso2",
                table: "paises",
                column: "codigo_iso2",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_sedes_departamentos_departamento_id",
                table: "sedes",
                column: "departamento_id",
                principalTable: "departamentos",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sedes_municipios_ciudad_id",
                table: "sedes",
                column: "ciudad_id",
                principalTable: "municipios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sedes_paises_pais_id",
                table: "sedes",
                column: "pais_id",
                principalTable: "paises",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sedes_departamentos_departamento_id",
                table: "sedes");

            migrationBuilder.DropForeignKey(
                name: "fk_sedes_municipios_ciudad_id",
                table: "sedes");

            migrationBuilder.DropForeignKey(
                name: "fk_sedes_paises_pais_id",
                table: "sedes");

            migrationBuilder.DropTable(
                name: "entidades");

            migrationBuilder.DropTable(
                name: "municipios");

            migrationBuilder.DropTable(
                name: "departamentos");

            migrationBuilder.DropTable(
                name: "paises");

            migrationBuilder.DropIndex(
                name: "ix_sedes_ciudad_id",
                table: "sedes");

            migrationBuilder.DropIndex(
                name: "ix_sedes_departamento_id",
                table: "sedes");

            migrationBuilder.DropIndex(
                name: "ix_sedes_pais_id",
                table: "sedes");
        }
    }
}
