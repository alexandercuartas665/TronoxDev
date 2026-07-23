/* ==================================================================
   TRONOX SGDEA - Selector de tema (paletas de marca)
   Puerto del prototipo (Prototipo/assets/js/tronox-theme.js), con el
   MISMO mecanismo: atributo data-tronox-theme en <html>, data-bs-theme
   para los oscuros, persistencia en localStorage bajo "tronox_theme" y
   un boton flotante que abre el panel de opciones.

   Igual que el prototipo, al elegir un tema se RECARGA la pagina. No es
   cosmetico: sin recarga, los elementos ya pintados del sidebar (el item
   activo, por ejemplo) conservan el color anterior aunque la variable
   --tx-menu-active-color ya valga la del tema nuevo; se verifico en el
   navegador. La recarga garantiza que el shell entero quede consistente.

   El tema se aplica ademas desde un snippet en el <head> (App.razor)
   para que no haya destello con el tema equivocado en la primera pintura.
   ================================================================== */
(function () {
  "use strict";

  var THEMES = [
    { id: "classic-light", name: "Tronox Clasico", sub: "Azul corporativo", dark: false, grad: "linear-gradient(135deg,#0c478a,#005ca9)" },
    { id: "fresh-light", name: "Tronox Fresh", sub: "Teal tecnologico", dark: false, grad: "linear-gradient(135deg,#00a89f,#16c79a)" },
    { id: "deep-dark", name: "Deep Dark", sub: "Oscuro corporativo", dark: true, grad: "linear-gradient(135deg,#0c478a,#16c79a)" },
    { id: "aurora-dark", name: "Aurora Dark", sub: "Teal oscuro", dark: true, grad: "linear-gradient(135deg,#005ca9,#00a89f)" },
    { id: "metronic", name: "Metronic", sub: "Tema original", dark: false, grad: "linear-gradient(135deg,#009ef7,#7239ea)" }
  ];
  var DEFAULT = "classic-light";
  var KEY = "tronox_theme";

  function byId(id) {
    for (var i = 0; i < THEMES.length; i++) { if (THEMES[i].id === id) { return THEMES[i]; } }
    return THEMES[0];
  }

  function cookieVal(n) {
    var m = document.cookie.match("(?:^|; )" + n + "=([^;]*)");
    return m ? m[1] : null;
  }

  function isKnown(v) {
    for (var i = 0; i < THEMES.length; i++) { if (THEMES[i].id === v) { return true; } }
    return false;
  }

  function current() {
    // FUENTE DE VERDAD: la cookie (la misma que lee el servidor en App.razor). localStorage
    // es solo respaldo por si la cookie no existiera. Un valor desconocido cae al por defecto.
    var c = cookieVal(KEY);
    if (isKnown(c)) { return c; }
    try {
      var v = localStorage.getItem(KEY);
      if (isKnown(v)) { return v; }
    } catch (e) { /* sin storage */ }
    return DEFAULT;
  }

  function apply(id) {
    var t = byId(id);
    var root = document.documentElement;
    // "metronic" es el :root base de custom.css: se activa QUITANDO el atributo.
    if (id === "metronic") { root.removeAttribute("data-tronox-theme"); }
    else { root.setAttribute("data-tronox-theme", id); }
    if (t.dark) { root.setAttribute("data-bs-theme", "dark"); }
    else { root.removeAttribute("data-bs-theme"); }
  }

  // Aplicar de inmediato por si el snippet del <head> no corrio.
  apply(current());

  window.tronoxTheme = {
    apply: apply,
    current: current,
    set: function (id) {
      try { localStorage.setItem(KEY, id); } catch (e) { /* sin storage: solo esta sesion */ }
      // Cookie ADEMAS de localStorage: es la que lee App.razor en el SERVIDOR para
      // renderizar data-tronox-theme en <html>. Asi la navegacion mejorada nunca pierde
      // el tema ni parpadea (el atributo ya viene en cada respuesta del servidor).
      try { document.cookie = KEY + "=" + id + ";path=/;max-age=31536000;samesite=lax"; } catch (e) { }
      apply(id);
      mark(id);
      // Ver la nota de cabecera: la recarga es lo que deja el shell entero repintado.
      // El snippet del <head> ya lee el tema recien guardado, asi que no hay parpadeo.
      location.reload();
    }
  };

  function mark(id) {
    var panel = document.getElementById("tx-theme-panel");
    if (!panel) { return; }
    var opts = panel.querySelectorAll(".tp-opt");
    for (var i = 0; i < opts.length; i++) {
      if (opts[i].getAttribute("data-theme") === id) { opts[i].classList.add("active"); }
      else { opts[i].classList.remove("active"); }
    }
  }

  function build() {
    if (!document.body || document.getElementById("tx-theme-fab")) { return; }

    var fab = document.createElement("button");
    fab.id = "tx-theme-fab";
    fab.type = "button";
    fab.title = "Cambiar tema";
    fab.setAttribute("aria-label", "Cambiar tema");
    fab.innerHTML = '<i class="bi bi-palette"></i>';

    var panel = document.createElement("div");
    panel.id = "tx-theme-panel";
    var cur = current();
    var html = '<div class="tp-title">Paleta de la plataforma</div>' +
               '<div class="tp-sub">Previsualiza las paletas de marca TRONOX.</div>';
    for (var i = 0; i < THEMES.length; i++) {
      var t = THEMES[i];
      html += '<button type="button" class="tp-opt' + (t.id === cur ? " active" : "") + '" data-theme="' + t.id + '">' +
        '<span class="tp-sw" style="background:' + t.grad + '"></span>' +
        '<span class="tp-name">' + t.name + '<small>' + t.sub + '</small></span>' +
        '<i class="bi bi-check-lg tp-check"></i></button>';
    }
    panel.innerHTML = html;

    document.body.appendChild(fab);
    document.body.appendChild(panel);

    fab.addEventListener("click", function (e) {
      e.stopPropagation();
      panel.classList.toggle("open");
    });
    document.addEventListener("click", function (e) {
      if (!panel.contains(e.target) && !fab.contains(e.target)) { panel.classList.remove("open"); }
    });
    panel.addEventListener("click", function (e) {
      var b = e.target.closest ? e.target.closest(".tp-opt") : null;
      if (!b) { return; }
      window.tronoxTheme.set(b.getAttribute("data-theme"));
    });
  }

  if (document.readyState === "loading") { document.addEventListener("DOMContentLoaded", build); }
  else { build(); }

  // La navegacion mejorada de Blazor parchea el <body> y puede llevarse el boton:
  // se vuelve a montar cuando eso pasa. build() es idempotente, asi que reinsertar
  // el FAB no dispara un ciclo con el observador.
  if (window.MutationObserver) {
    new MutationObserver(function () { build(); })
      .observe(document.documentElement, { childList: true, subtree: true });
  }

  // Red de seguridad: tras una navegacion mejorada, reaplicar el tema por si el atributo
  // se perdiera. Con App.razor renderizando data-tronox-theme desde la cookie esto casi
  // nunca hace falta, pero cubre el caso de una cookie ausente (solo localStorage).
  document.addEventListener("enhancedload", function () { apply(current()); build(); });
})();
