using Tronox.Application.Firmas;

namespace Tronox.Application.Common;

/// <summary>
/// Genera el PDF del acta / certificado de firma (RQ05 - RF04, legacy fir_certificado.aspx). El acta es
/// el "informe de auditoria" estilo Adobe Acrobat Sign: resumen (ID de transaccion, hash, estado) +
/// historial de eventos del ledger. El legacy lo imprimia desde el navegador; en TRONOX se genera
/// server-side con QuestPDF (sin depender de un navegador headless).
/// </summary>
public interface IActaFirmaRenderer
{
    byte[] Render(ActaFirmaDto acta);
}
