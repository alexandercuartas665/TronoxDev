using Microsoft.EntityFrameworkCore;
using Tronox.Domain.Entities;

namespace Tronox.Infrastructure.Persistence;

/// <summary>
/// Siembra de los catalogos territoriales DIVIPOLA (pendiente P-02 de RQ01, resuelto por
/// PRECARGA EN BASE DE DATOS y no por integracion en vivo con una API del DANE).
///
/// ALCANCE REAL DE ESTA SIEMBRA - leer antes de usarla como si fuera completa:
///
/// - Paises: SOLO Colombia. TRONOX es un SGDEA para entidades publicas colombianas; el resto
///   de ISO 3166-1 se agrega cuando exista un caso que lo pida.
/// - Departamentos: los 33 de la division politico-administrativa (32 departamentos + Bogota
///   D.C.), con su codigo DANE de 2 digitos. Esto SI esta completo.
/// - Municipios: NO estan los ~1.100 del DANE. Se siembra un subconjunto DOCUMENTADO: las 32
///   capitales departamentales (cuyo codigo DIVIPOLA es siempre {DD}001 por convencion DANE)
///   mas cinco municipios de Cundinamarca, porque Cundinamarca es el unico departamento cuya
///   capital (Bogota D.C.) NO le pertenece y se quedaria sin ninguna opcion en el selector.
///
/// Cargar el listado completo del DANE es trabajo aparte (importacion del archivo oficial); no
/// se inventan codigos aqui para aparentar cobertura.
/// </summary>
internal static class DivipolaSeed
{
    /// <summary>
    /// Marca de tiempo FIJA de la siembra. HasData exige valores constantes: usar DateTimeOffset.
    /// UtcNow haria que cada "migrations add" detectara un cambio inexistente.
    /// </summary>
    private static readonly DateTimeOffset SeedAt = new(2026, 7, 24, 0, 0, 0, TimeSpan.Zero);

    private const long ColombiaId = 1;

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pais>().HasData(new Pais
        {
            Id = ColombiaId,
            CodigoIso2 = "CO",
            CodigoIso3 = "COL",
            Nombre = "Colombia",
            Activo = true,
            CreatedAt = SeedAt
        });

        modelBuilder.Entity<Departamento>().HasData(
            Departamentos.Select(d => new Departamento
            {
                Id = d.Id,
                PaisId = ColombiaId,
                CodigoDane = d.Codigo,
                Nombre = d.Nombre,
                Activo = true,
                CreatedAt = SeedAt
            }));

        modelBuilder.Entity<Municipio>().HasData(
            Municipios.Select(m => new Municipio
            {
                Id = m.Id,
                DepartamentoId = m.DepartamentoId,
                CodigoDivipola = m.Codigo,
                Nombre = m.Nombre,
                EsCapital = m.EsCapital,
                Activo = true,
                CreatedAt = SeedAt
            }));
    }

    private sealed record DepSeed(long Id, string Codigo, string Nombre);
    private sealed record MunSeed(long Id, long DepartamentoId, string Codigo, string Nombre, bool EsCapital);

    // Los 33 territorios de primer nivel. El Id es estable y lo reutilizan los municipios.
    // Nombres con tilde escritos con escapes \uXXXX: los .cs del proyecto son ASCII.
    private static readonly DepSeed[] Departamentos =
    [
        new(1,  "05", "Antioquia"),
        new(2,  "08", "Atl\u00e1ntico"),
        new(3,  "11", "Bogot\u00e1, D.C."),
        new(4,  "13", "Bol\u00edvar"),
        new(5,  "15", "Boyac\u00e1"),
        new(6,  "17", "Caldas"),
        new(7,  "18", "Caquet\u00e1"),
        new(8,  "19", "Cauca"),
        new(9,  "20", "Cesar"),
        new(10, "23", "C\u00f3rdoba"),
        new(11, "25", "Cundinamarca"),
        new(12, "27", "Choc\u00f3"),
        new(13, "41", "Huila"),
        new(14, "44", "La Guajira"),
        new(15, "47", "Magdalena"),
        new(16, "50", "Meta"),
        new(17, "52", "Nari\u00f1o"),
        new(18, "54", "Norte de Santander"),
        new(19, "63", "Quind\u00edo"),
        new(20, "66", "Risaralda"),
        new(21, "68", "Santander"),
        new(22, "70", "Sucre"),
        new(23, "73", "Tolima"),
        new(24, "76", "Valle del Cauca"),
        new(25, "81", "Arauca"),
        new(26, "85", "Casanare"),
        new(27, "86", "Putumayo"),
        new(28, "88", "Archipi\u00e9lago de San Andr\u00e9s, Providencia y Santa Catalina"),
        new(29, "91", "Amazonas"),
        new(30, "94", "Guain\u00eda"),
        new(31, "95", "Guaviare"),
        new(32, "97", "Vaup\u00e9s"),
        new(33, "99", "Vichada")
    ];

    // Capitales departamentales (codigo {DD}001) + cinco municipios de Cundinamarca.
    // SUBCONJUNTO DECLARADO: no es el listado completo del DANE.
    private static readonly MunSeed[] Municipios =
    [
        new(1,  1,  "05001", "Medell\u00edn", true),
        new(2,  2,  "08001", "Barranquilla", true),
        new(3,  3,  "11001", "Bogot\u00e1, D.C.", true),
        new(4,  4,  "13001", "Cartagena de Indias", true),
        new(5,  5,  "15001", "Tunja", true),
        new(6,  6,  "17001", "Manizales", true),
        new(7,  7,  "18001", "Florencia", true),
        new(8,  8,  "19001", "Popay\u00e1n", true),
        new(9,  9,  "20001", "Valledupar", true),
        new(10, 10, "23001", "Monter\u00eda", true),
        new(11, 12, "27001", "Quibd\u00f3", true),
        new(12, 13, "41001", "Neiva", true),
        new(13, 14, "44001", "Riohacha", true),
        new(14, 15, "47001", "Santa Marta", true),
        new(15, 16, "50001", "Villavicencio", true),
        new(16, 17, "52001", "Pasto", true),
        new(17, 18, "54001", "C\u00facuta", true),
        new(18, 19, "63001", "Armenia", true),
        new(19, 20, "66001", "Pereira", true),
        new(20, 21, "68001", "Bucaramanga", true),
        new(21, 22, "70001", "Sincelejo", true),
        new(22, 23, "73001", "Ibagu\u00e9", true),
        new(23, 24, "76001", "Cali", true),
        new(24, 25, "81001", "Arauca", true),
        new(25, 26, "85001", "Yopal", true),
        new(26, 27, "86001", "Mocoa", true),
        new(27, 28, "88001", "San Andr\u00e9s", true),
        new(28, 29, "91001", "Leticia", true),
        new(29, 30, "94001", "In\u00edrida", true),
        new(30, 31, "95001", "San Jos\u00e9 del Guaviare", true),
        new(31, 32, "97001", "Mit\u00fa", true),
        new(32, 33, "99001", "Puerto Carre\u00f1o", true),
        // Cundinamarca (11): su capital es Bogota D.C., que pertenece al departamento 11 y no
        // al 25. Sin estas cinco filas el selector de Cundinamarca saldria vacio.
        new(33, 11, "25754", "Soacha", false),
        new(34, 11, "25899", "Zipaquir\u00e1", false),
        new(35, 11, "25175", "Ch\u00eda", false),
        new(36, 11, "25290", "Fusagasug\u00e1", false),
        new(37, 11, "25307", "Girardot", false)
    ];
}
