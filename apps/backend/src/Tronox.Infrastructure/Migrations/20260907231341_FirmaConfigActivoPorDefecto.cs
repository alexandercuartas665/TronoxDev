using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FirmaConfigActivoPorDefecto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalizacion una sola vez: el modulo de firma y la firma masiva pasan a estar ACTIVOS por
            // defecto (antes el default de la entidad era false, sin consumo). Las filas existentes se
            // crearon con ese default no-intencional; se ponen en true para no bloquear firma al empezar
            // a consumir la config (RF01). El admin puede apagarlos luego desde Datos de la Entidad.
            migrationBuilder.Sql("UPDATE firma_configs SET modulo_firma_activo = true, firma_masiva_activa = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
