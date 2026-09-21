using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Tronox.Application.Common;

namespace Tronox.Infrastructure.Pdf;

/// <summary>
/// Conversion a PDF/A-2b con LibreOffice headless (RQ05 - RF02), port de PdfAConverter del legacy.
/// Invoca `soffice --headless --norestore -env:UserInstallation=file://&lt;perfil&gt; --convert-to
/// pdf:writer_pdf_Export:{"SelectPdfVersion":{"type":"long","value":"2"}} --outdir &lt;out&gt; &lt;entrada&gt;`.
/// Perfil temporal por conversion (concurrencia), timeout de 120 s, y validacion debil por texto
/// (pdfaid:part) como el legacy (veraPDF estricto queda diferido). Best-effort: cualquier fallo devuelve
/// null y el caller sella el PDF sin convertir (la firma no se frena).
/// </summary>
public sealed class LibreOfficePdfAConverter : IPdfAConverter
{
    private const int TimeoutMs = 120_000;
    private readonly ILogger<LibreOfficePdfAConverter> _log;
    private readonly string? _soffice;

    public LibreOfficePdfAConverter(ILogger<LibreOfficePdfAConverter> log)
    {
        _log = log;
        _soffice = ResolverSoffice();
    }

    public bool Disponible => _soffice is not null;

    private static string? ResolverSoffice()
    {
        var cfg = Environment.GetEnvironmentVariable("TRONOX_SOFFICE_PATH");
        var candidatos = new[]
        {
            cfg,
            "/usr/bin/soffice",
            "/usr/lib/libreoffice/program/soffice",
            "/opt/libreoffice/program/soffice",
            @"C:\Program Files\LibreOffice\program\soffice.exe"
        };
        foreach (var c in candidatos)
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c)) { return c; }
        }
        return null;
    }

    public async Task<byte[]?> ConvertirPdfAAsync(byte[] pdf, CancellationToken cancellationToken = default)
    {
        if (_soffice is null || pdf is null || pdf.Length == 0) { return null; }

        var trabajo = Path.Combine(Path.GetTempPath(), "firpdfa_" + Guid.NewGuid().ToString("N"));
        var perfil = Path.Combine(trabajo, "profile");
        var outDir = Path.Combine(trabajo, "out");
        var entrada = Path.Combine(trabajo, "entrada.pdf");
        try
        {
            Directory.CreateDirectory(perfil);
            Directory.CreateDirectory(outDir);
            await File.WriteAllBytesAsync(entrada, pdf, cancellationToken);

            var perfilUri = new Uri(perfil).AbsoluteUri;   // file:///... (POSIX en Linux)
            var psi = new ProcessStartInfo
            {
                FileName = _soffice,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("--headless");
            psi.ArgumentList.Add("--norestore");
            psi.ArgumentList.Add("-env:UserInstallation=" + perfilUri);
            psi.ArgumentList.Add("--convert-to");
            psi.ArgumentList.Add("pdf:writer_pdf_Export:{\"SelectPdfVersion\":{\"type\":\"long\",\"value\":\"2\"}}");
            psi.ArgumentList.Add("--outdir");
            psi.ArgumentList.Add(outDir);
            psi.ArgumentList.Add(entrada);

            using var proc = new Process { StartInfo = psi };
            if (!proc.Start()) { return null; }
            // Drenar stdout/stderr para no bloquear el buffer.
            var so = proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var se = proc.StandardError.ReadToEndAsync(cancellationToken);
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(TimeoutMs);
                try { await proc.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException)
                {
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    _log.LogWarning("Conversion PDF/A: timeout de soffice ({Ms} ms)", TimeoutMs);
                    return null;
                }
            }
            await Task.WhenAll(so, se);

            var salida = Path.Combine(outDir, "entrada.pdf");
            // soffice puede tardar en volcar el archivo tras salir; se espera hasta 3 s.
            for (var i = 0; i < 20 && !File.Exists(salida); i++) { await Task.Delay(150, cancellationToken); }
            if (!File.Exists(salida))
            {
                _log.LogWarning("Conversion PDF/A: soffice no genero salida. stderr: {Err}", Recorta(se.Result));
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(salida, cancellationToken);
            if (!EsPdfA(bytes))
            {
                _log.LogWarning("Conversion PDF/A: la salida no declara pdfaid:part; se descarta.");
                return null;
            }
            return bytes;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Conversion PDF/A fallo");
            return null;
        }
        finally
        {
            try { if (Directory.Exists(trabajo)) { Directory.Delete(trabajo, recursive: true); } } catch { }
        }
    }

    /// <summary>Chequeo debil de conformidad (como el legacy): busca el marcador pdfaid:part del XMP.</summary>
    private static bool EsPdfA(byte[] contenido)
    {
        var txt = Encoding.GetEncoding("iso-8859-1").GetString(contenido);
        return txt.IndexOf("pdfaid:part", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string Recorta(string? s) => string.IsNullOrEmpty(s) ? "" : (s.Length <= 300 ? s : s[..300]);
}
