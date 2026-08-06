namespace Tronox.Application.Radicacion;

/// <summary>
/// Calculo PURO de los festivos de Colombia para un anio (Ley 51 de 1983 - "Ley Emiliani" - que
/// traslada varios festivos al lunes siguiente, mas los festivos moviles derivados de la Pascua).
/// Sin IO ni EF: testeable sin base de datos. Se usa para sembrar el calendario habil por tenant.
/// </summary>
public static class FestivosColombia
{
    /// <summary>Festivos (fecha, nombre) del anio dado.</summary>
    public static IReadOnlyList<(DateOnly Fecha, string Nombre)> Calcular(int anio)
    {
        var lista = new List<(DateOnly, string)>();

        // Fijos (no se trasladan).
        lista.Add((new DateOnly(anio, 1, 1), "Ano Nuevo"));
        lista.Add((new DateOnly(anio, 5, 1), "Dia del Trabajo"));
        lista.Add((new DateOnly(anio, 7, 20), "Dia de la Independencia"));
        lista.Add((new DateOnly(anio, 8, 7), "Batalla de Boyaca"));
        lista.Add((new DateOnly(anio, 12, 8), "Inmaculada Concepcion"));
        lista.Add((new DateOnly(anio, 12, 25), "Navidad"));

        // Trasladables al lunes siguiente (Ley Emiliani), fecha base.
        lista.Add((TrasladarLunes(new DateOnly(anio, 1, 6)), "Reyes Magos"));
        lista.Add((TrasladarLunes(new DateOnly(anio, 3, 19)), "San Jose"));
        lista.Add((TrasladarLunes(new DateOnly(anio, 6, 29)), "San Pedro y San Pablo"));
        lista.Add((TrasladarLunes(new DateOnly(anio, 8, 15)), "Asuncion de la Virgen"));
        lista.Add((TrasladarLunes(new DateOnly(anio, 10, 12)), "Dia de la Raza"));
        lista.Add((TrasladarLunes(new DateOnly(anio, 11, 1)), "Todos los Santos"));
        lista.Add((TrasladarLunes(new DateOnly(anio, 11, 11)), "Independencia de Cartagena"));

        // Moviles derivados de la Pascua (Domingo de Resurreccion).
        var pascua = DomingoPascua(anio);
        lista.Add((pascua.AddDays(-3), "Jueves Santo"));
        lista.Add((pascua.AddDays(-2), "Viernes Santo"));
        // Estos se trasladan al lunes siguiente al numero de dias despues de Pascua.
        lista.Add((TrasladarLunes(pascua.AddDays(43)), "Ascension del Senor"));      // +40, trasladado
        lista.Add((TrasladarLunes(pascua.AddDays(64)), "Corpus Christi"));           // +60, trasladado
        lista.Add((TrasladarLunes(pascua.AddDays(71)), "Sagrado Corazon"));          // +68, trasladado

        return lista.OrderBy(x => x.Item1).ToList();
    }

    // Si la fecha no es lunes, se traslada al lunes siguiente (Ley Emiliani).
    private static DateOnly TrasladarLunes(DateOnly f)
    {
        var dow = (int)f.DayOfWeek; // Sunday=0 ... Saturday=6
        if (dow == 1) { return f; }
        var diasHastaLunes = (8 - dow) % 7; // dias hasta el proximo lunes
        if (diasHastaLunes == 0) { diasHastaLunes = 1; }
        return f.AddDays(diasHastaLunes);
    }

    // Domingo de Pascua por el algoritmo de Gauss/Butcher (Computus, calendario gregoriano).
    private static DateOnly DomingoPascua(int y)
    {
        int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4;
        int f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int mes = (h + l - 7 * m + 114) / 31;
        int dia = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(y, mes, dia);
    }
}
