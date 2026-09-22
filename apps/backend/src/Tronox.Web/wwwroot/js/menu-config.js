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
