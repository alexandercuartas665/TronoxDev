// Captura Tier 2 de formularios (ola F6): firma en canvas, GPS y archivo->dataURL.
// Sin callbacks a .NET: las funciones devuelven valores que Blazor obtiene con InvokeAsync.
// Todas operan por id del elemento (document.getElementById) para no cablear ElementReference por campo.
window.tronoxFormCapture = (function () {
  function canvasOf(id) {
    const el = document.getElementById(id);
    return el && el.getContext ? el : null;
  }

  // Inicializa el trazo del canvas de firma (idempotente por marca en el elemento).
  function initSignature(id) {
    const canvas = canvasOf(id);
    if (!canvas || canvas.dataset.ecxInit === '1') { return; }
    canvas.dataset.ecxInit = '1';
    const ctx = canvas.getContext('2d');
    ctx.lineWidth = 2.2; ctx.lineCap = 'round'; ctx.strokeStyle = '#1B1B1E';
    let drawing = false, last = null;
    const pos = (e) => {
      const r = canvas.getBoundingClientRect();
      const t = e.touches ? e.touches[0] : e;
      return { x: t.clientX - r.left, y: t.clientY - r.top };
    };
    const down = (e) => { drawing = true; last = pos(e); e.preventDefault(); };
    const move = (e) => {
      if (!drawing) { return; }
      const p = pos(e);
      ctx.beginPath(); ctx.moveTo(last.x, last.y); ctx.lineTo(p.x, p.y); ctx.stroke();
      last = p; e.preventDefault();
    };
    const up = () => { drawing = false; };
    canvas.addEventListener('pointerdown', down);
    canvas.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  }

  function signatureData(id) {
    const canvas = canvasOf(id);
    return canvas ? canvas.toDataURL('image/png') : '';
  }

  function clearSignature(id) {
    const canvas = canvasOf(id);
    if (canvas) { canvas.getContext('2d').clearRect(0, 0, canvas.width, canvas.height); }
  }

  // Dibuja un trazo de prueba (para verificacion automatizada) y devuelve el dataURL.
  function testStroke(id) {
    const canvas = canvasOf(id);
    if (!canvas) { return ''; }
    const ctx = canvas.getContext('2d');
    ctx.lineWidth = 2.2; ctx.strokeStyle = '#1B1B1E';
    ctx.beginPath(); ctx.moveTo(10, 30); ctx.lineTo(60, 10); ctx.lineTo(110, 40); ctx.lineTo(160, 15); ctx.stroke();
    return canvas.toDataURL('image/png');
  }

  function geolocate() {
    return new Promise((resolve) => {
      if (!navigator.geolocation) { resolve('sin-geolocalizacion'); return; }
      navigator.geolocation.getCurrentPosition(
        (p) => resolve(p.coords.latitude.toFixed(5) + ', ' + p.coords.longitude.toFixed(5)),
        (e) => resolve('error: ' + e.message),
        { timeout: 8000 });
    });
  }

  // Descarga un archivo desde base64 (export XLSX de la bandeja de modulo).
  function downloadBase64(b64, filename, mime) {
    const bin = atob(b64);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) { bytes[i] = bin.charCodeAt(i); }
    const blob = new Blob([bytes], { type: mime || 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = filename || 'archivo'; document.body.appendChild(a);
    a.click(); a.remove(); setTimeout(() => URL.revokeObjectURL(url), 1500);
  }

  // Captura de archivo/foto: input file -> dataURL (para Photo/Image/File).
  function fileToDataUrl(inputId) {
    return new Promise((resolve) => {
      const el = document.getElementById(inputId);
      if (!el || !el.files || !el.files[0]) { resolve(''); return; }
      const r = new FileReader();
      r.onload = () => resolve(r.result);
      r.onerror = () => resolve('');
      r.readAsDataURL(el.files[0]);
    });
  }

  return { initSignature, signatureData, clearSignature, testStroke, geolocate, downloadBase64, fileToDataUrl };
})();
