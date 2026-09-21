using System.Security.Cryptography;
using System.Text;
using Tronox.Application.Common;

namespace Tronox.Infrastructure.Pdf;

/// <summary>
/// Sellado XMP length-neutral con hash byte-range excluido (RQ05 - RF02/RF03), port de FirmaSelladoHelper.
/// Todo el trabajo es en espacio de BYTES: los marcadores se localizan con ISO-8859-1 (1 char = 1 byte) y
/// el bloque tronox: se inserta como bytes UTF-8, consumiendo el padding whitespace del xpacket para que
/// el tamano y los offsets del PDF no cambien. Best-effort: cualquier incoherencia devuelve null.
/// </summary>
public sealed class PdfXmpSealer : IPdfXmpSealer
{
    private const string NsTronox = "http://tronox.co/ns/firma/1.0/";
    private const string PlaceholderHash = "0000000000000000000000000000000000000000000000000000000000000000"; // 64 ceros
    private static readonly Encoding Lat = Encoding.GetEncoding("iso-8859-1");
    private const string HashOpen = "<tronox:hash>";

    public SelladoResultado? Sellar(byte[] pdfA, DatosSellado d)
    {
        if (pdfA is null || pdfA.Length == 0) { return null; }
        try
        {
            var s = Lat.GetString(pdfA);
            var posRDF = s.IndexOf("</rdf:RDF>", StringComparison.Ordinal);
            var posXEnd = s.IndexOf("<?xpacket end", StringComparison.Ordinal);
            if (posRDF < 0 || posXEnd < 0 || posXEnd <= posRDF) { return null; }

            var bloque = Encoding.UTF8.GetBytes(ConstruirBloqueXmp(d));
            var l = bloque.Length;

            // Consumir L bytes de padding whitespace justo antes de <?xpacket end (dejando 2 de respeto).
            var cut = posXEnd - 2 - l;
            if (cut <= posRDF) { return null; }
            for (var i = cut; i < cut + l; i++)
            {
                var by = pdfA[i];
                if (by != 0x20 && by != 0x0A && by != 0x0D && by != 0x09) { return null; } // no hay padding suficiente
            }

            // Reensamblar: insertar el bloque en posRDF y eliminar L bytes de whitespace en cut. Tamano neutro.
            byte[] outBytes;
            using (var ms = new MemoryStream(pdfA.Length))
            {
                ms.Write(pdfA, 0, posRDF);
                ms.Write(bloque, 0, l);
                ms.Write(pdfA, posRDF, cut - posRDF);
                ms.Write(pdfA, cut + l, pdfA.Length - cut - l);
                outBytes = ms.ToArray();
            }
            if (outBytes.Length != pdfA.Length) { return null; } // invariante length-neutral

            var hash = CalcularHashExcluyendoXmp(outBytes);
            if (hash is null) { return null; }
            if (!InsertarHash(outBytes, hash)) { return null; }

            return new SelladoResultado(outBytes, hash);
        }
        catch
        {
            return null;
        }
    }

    public string? RecalcularHash(byte[] pdf)
    {
        if (pdf is null || pdf.Length == 0) { return null; }
        try { return CalcularHashExcluyendoXmp(pdf); }
        catch { return null; }
    }

    private static string ConstruirBloqueXmp(DatosSellado d)
    {
        var sb = new StringBuilder();
        sb.Append("<rdf:Description rdf:about=\"\" xmlns:tronox=\"").Append(NsTronox).Append("\">");
        sb.Append("<tronox:hash>").Append(PlaceholderHash).Append("</tronox:hash>");
        sb.Append("<tronox:tenant>").Append(d.Tenant).Append("</tronox:tenant>");
        sb.Append("<tronox:documento_id>").Append(d.DocumentoId).Append("</tronox:documento_id>");
        sb.Append("<tronox:firmante_principal>").Append(Esc(d.FirmantePrincipal)).Append("</tronox:firmante_principal>");
        sb.Append("<tronox:total_firmantes>").Append(d.TotalFirmantes).Append("</tronox:total_firmantes>");
        sb.Append("<tronox:timestamp>").Append(d.Timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'")).Append("</tronox:timestamp>");
        sb.Append("<tronox:verificar_url>").Append(Esc(d.VerificarUrl)).Append("</tronox:verificar_url>");
        sb.Append("<tronox:tipo_firma>").Append(Esc(d.TipoFirma)).Append("</tronox:tipo_firma>");
        sb.Append("</rdf:Description>");
        return sb.ToString();
    }

    private static string Esc(string? v)
        => (v ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>SHA-256 del PDF excluyendo TODO el paquete XMP (de &lt;?xpacket begin al ?&gt; de cierre).</summary>
    private static string? CalcularHashExcluyendoXmp(byte[] pdf)
    {
        var s = Lat.GetString(pdf);
        var xs = s.IndexOf("<?xpacket begin", StringComparison.Ordinal);
        var xeMarker = s.IndexOf("<?xpacket end", StringComparison.Ordinal);
        if (xs < 0 || xeMarker < 0) { return null; }
        var xeClose = s.IndexOf("?>", xeMarker, StringComparison.Ordinal);
        if (xeClose < 0) { return null; }
        var xe = xeClose + 2;

        using var sha = SHA256.Create();
        using var ms = new MemoryStream(pdf.Length);
        ms.Write(pdf, 0, xs);                       // antes del <?xpacket begin
        ms.Write(pdf, xe, pdf.Length - xe);         // despues del ?> de cierre
        var h = sha.ComputeHash(ms.ToArray());
        var sb = new StringBuilder(64);
        foreach (var b in h) { sb.Append(b.ToString("x2")); }
        return sb.ToString();
    }

    /// <summary>Sobrescribe el placeholder de 64 ceros (tras &lt;tronox:hash&gt;) con el hash real (64 hex, neutro).</summary>
    private static bool InsertarHash(byte[] pdf, string hash)
    {
        if (hash.Length != 64) { return false; }
        var s = Lat.GetString(pdf);
        var idx = s.IndexOf(HashOpen, StringComparison.Ordinal);
        if (idx < 0) { return false; }
        var start = idx + HashOpen.Length;
        if (start + 64 > pdf.Length) { return false; }
        var hb = Encoding.ASCII.GetBytes(hash);
        Array.Copy(hb, 0, pdf, start, 64);
        return true;
    }
}
