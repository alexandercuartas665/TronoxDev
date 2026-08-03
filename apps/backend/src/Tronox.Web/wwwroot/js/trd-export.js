// Descarga de archivos generados en el servidor (RQ02 - RF04): la TRD exportada a XLS/XML.
// Blazor Server no puede iniciar una descarga directamente; se pasa el contenido en base64 y
// aqui se arma un Blob y se dispara el <a download>.
window.tronoxDownload = function (filename, base64, mime) {
    try {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        const blob = new Blob([bytes], { type: mime || 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename || 'archivo';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(url); }, 1500);
    } catch (e) {
        console.error('tronoxDownload error', e);
    }
};
