using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Tronox.Application.Common;

namespace Tronox.Infrastructure.Pdf;

/// <summary>
/// HTML -&gt; PDF con Chromium headless (PuppeteerSharp), reemplazo cross-platform de SelectPdf (RF08).
/// El binario de Chromium se resuelve por configuracion: si <c>Puppeteer:ExecutablePath</c> (o la variable
/// de entorno PUPPETEER_EXECUTABLE_PATH) apunta a un Chromium instalado (contenedor Linux de prod), se usa
/// ese; en dev, si no hay path, se descarga una revision cacheada la primera vez. El fragmento HTML del
/// editor se envuelve en una hoja tamano Carta con margenes de 1" (calca contentsCss de CKEditor).
/// </summary>
public sealed class PuppeteerHtmlToPdfConverter : IHtmlToPdfConverter
{
    private readonly string? _executablePath;
    private readonly ILogger<PuppeteerHtmlToPdfConverter> _log;
    private static readonly SemaphoreSlim _fetchGate = new(1, 1);
    private static bool _fetched;

    public PuppeteerHtmlToPdfConverter(IConfiguration configuration, ILogger<PuppeteerHtmlToPdfConverter> log)
    {
        _executablePath = configuration["Puppeteer:ExecutablePath"]
            ?? Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");
        _log = log;
    }

    public async Task<byte[]> ConvertirAsync(string html, CancellationToken cancellationToken = default)
    {
        var launch = new LaunchOptions
        {
            Headless = true,
            // --no-sandbox: obligatorio para Chromium corriendo como root en el contenedor.
            Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"]
        };

        if (!string.IsNullOrWhiteSpace(_executablePath))
        {
            launch.ExecutablePath = _executablePath;
        }
        else
        {
            await EnsureBrowserDownloadedAsync(cancellationToken);
        }

        await using var browser = await Puppeteer.LaunchAsync(launch);
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(Envolver(html), new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.Networkidle0]
        });

        var pdf = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.Letter,
            PrintBackground = true,
            // Los margenes los da el padding del body (calca la hoja del editor); PDF sin margen extra.
            MarginOptions = new MarginOptions { Top = "0", Bottom = "0", Left = "0", Right = "0" }
        });
        return pdf;
    }

    private async Task EnsureBrowserDownloadedAsync(CancellationToken ct)
    {
        if (_fetched) { return; }
        await _fetchGate.WaitAsync(ct);
        try
        {
            if (_fetched) { return; }
            _log.LogInformation("Descargando Chromium para PuppeteerSharp (primera vez)...");
            var fetcher = new BrowserFetcher();
            await fetcher.DownloadAsync();
            _fetched = true;
        }
        finally { _fetchGate.Release(); }
    }

    /// <summary>Envuelve el cuerpo del editor en un documento Carta con la misma hoja/tipografia del editor.</summary>
    private static string Envolver(string bodyHtml)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        sb.Append("<style>");
        sb.Append("@page{size:Letter;margin:0;}");
        sb.Append("html{background:#fff;}");
        sb.Append("body{font-family:'Segoe UI',Arial,sans-serif;font-size:13px;line-height:1.7;color:#0F172A;");
        sb.Append("margin:0;padding:96px;box-sizing:border-box;}");
        sb.Append("img{max-width:100%;}");
        sb.Append("table{border-collapse:collapse;}table td,table th{border:1px solid #CBD5E1;padding:6px 8px;}");
        sb.Append("</style></head><body>");
        sb.Append(bodyHtml);
        sb.Append("</body></html>");
        return sb.ToString();
    }
}
