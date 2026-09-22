// Helper del Administrador de Menu (Ola 2): descarga un texto (JSON de la vista exportada)
// como archivo, sin depender de librerias. Se invoca desde ConfiguracionMenu.razor via JS interop.
window.tronoxMenuConfig = {
  downloadText: function (filename, text, mime) {
    try {
      var blob = new Blob([text], { type: mime || "application/json;charset=utf-8" });
      var url = window.URL.createObjectURL(blob);
      var a = document.createElement("a");
      a.href = url;
      a.download = filename || "vista-menu.json";
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
      return true;
    } catch (e) {
      return false;
    }
  },
  // Devuelve la posicion (x,y en % del stage) del sello de radicado arrastrable (RF02-5).
  // La posicion la mantiene el arrastre en data-estx/data-esty del stage (ver el delegado global abajo).
  estampaPos: function (stageId) {
    try {
      var s = document.getElementById(stageId);
      if (!s) { return { x: 62, y: 6 }; }
      return { x: parseFloat(s.dataset.estx || "62"), y: parseFloat(s.dataset.esty || "6") };
    } catch (e) { return { x: 62, y: 6 }; }
  },
  // Abre una ventana emergente con el HTML dado y lanza el dialogo de impresion (sticker de radicado).
  printHtml: function (title, html) {
    try {
      var w = window.open("", "_blank", "width=420,height=300");
      if (!w) { return false; }
      w.document.write("<html><head><title>" + (title || "Impresion") + "</title></head><body style=\"margin:16px;\">" + html + "</body></html>");
      w.document.close();
      w.focus();
      w.print();
      return true;
    } catch (e) {
      return false;
    }
  }
};

// Arrastre del sello de radicado (RF02-5). Delegacion global: cualquier ".tx-est-chip" dentro de un
// ".tx-est-stage" se puede arrastrar; la posicion se guarda como % en data-estx/data-esty del stage y
// la lee tronoxMenuConfig.estampaPos al radicar. Idempotente: no necesita re-init por render de Blazor.
(function () {
  if (window.__txEstampaInit) { return; }
  window.__txEstampaInit = true;
  var drag = null;
  function clamp(v, min, max) { return Math.max(min, Math.min(max, v)); }
  function apply(stage, chip, x, y) {
    stage.dataset.estx = x.toFixed(1);
    stage.dataset.esty = y.toFixed(1);
    chip.style.left = x + "%";
    chip.style.top = y + "%";
  }
  document.addEventListener("pointerdown", function (e) {
    var chip = e.target.closest ? e.target.closest(".tx-est-chip") : null;
    if (!chip) { return; }
    var stage = chip.closest(".tx-est-stage");
    if (!stage) { return; }
    drag = { chip: chip, stage: stage };
    try { chip.setPointerCapture(e.pointerId); } catch (x) {}
    e.preventDefault();
  });
  document.addEventListener("pointermove", function (e) {
    if (!drag) { return; }
    var r = drag.stage.getBoundingClientRect();
    if (r.width <= 0 || r.height <= 0) { return; }
    var x = clamp((e.clientX - r.left) / r.width * 100, 0, 88);
    var y = clamp((e.clientY - r.top) / r.height * 100, 0, 90);
    apply(drag.stage, drag.chip, x, y);
  });
  document.addEventListener("pointerup", function () { drag = null; });
})();
