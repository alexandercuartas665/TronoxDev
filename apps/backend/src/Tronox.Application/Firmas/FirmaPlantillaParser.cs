using System.Text.RegularExpressions;

namespace Tronox.Application.Firmas;

/// <summary>Un token {{firma...}} encontrado en el contenido de un documento (RF13).</summary>
public sealed class FirmaTokenPlantilla
{
    public string Destino { get; set; } = "";   // rol/cargo o correo; "" = generica
    public int Orden { get; set; }
    public bool Opcional { get; set; }
    public bool EsCorreo { get; set; }
    public string Raw { get; set; } = "";
}

/// <summary>
/// Parser de la variable {{firma}} en el contenido de un documento (RQ05 - RF13 3.13.1), port de
/// FirmaPlantillaParser. Reconoce: {{firma}} (generica), {{firma:Director}} (rol/cargo),
/// {{firma:correo@x}} (usuario), {{firma:Rol | orden:N | opcional}}. Solo parsea; la resolucion a
/// usuarios vive en el servicio.
/// </summary>
public static partial class FirmaPlantillaParser
{
    [GeneratedRegex(@"\{\{\s*firma\b([^}]*)\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();

    public static List<FirmaTokenPlantilla> Parse(string? html)
    {
        var lista = new List<FirmaTokenPlantilla>();
        if (string.IsNullOrEmpty(html)) { return lista; }

        var aparicion = 0;
        foreach (Match m in TokenRegex().Matches(html))
        {
            aparicion++;
            var t = new FirmaTokenPlantilla { Raw = m.Value };

            var interior = m.Groups[1].Value.Trim();
            if (interior.StartsWith(':')) { interior = interior[1..]; }

            var segs = interior.Split('|');
            if (segs.Length > 0) { t.Destino = segs[0].Trim(); }
            for (var i = 1; i < segs.Length; i++)
            {
                var s = segs[i].Trim();
                if (s.StartsWith("orden:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(s[6..].Trim(), out var n) && n > 0) { t.Orden = n; }
                }
                else if (s.Equals("opcional", StringComparison.OrdinalIgnoreCase))
                {
                    t.Opcional = true;
                }
            }

            t.EsCorreo = t.Destino.Contains('@');
            if (t.Orden <= 0) { t.Orden = aparicion; }
            lista.Add(t);
        }
        return lista;
    }
}
