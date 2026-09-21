/* RF04 - Visor documental (pdf.js). */
(function () {
    "use strict";

    var shell = document.getElementById("vzShell");
    if (!shell) return;

    var cfg = {
        bin: shell.getAttribute("data-bin"),
        formato: (shell.getAttribute("data-formato") || "").toLowerCase(),
        worker: shell.getAttribute("data-pdfworker"),
        doc: shell.getAttribute("data-doc"),
        exp: shell.getAttribute("data-exp"),
        re: shell.getAttribute("data-re"),
        t: shell.getAttribute("data-t"),
        tipo: shell.getAttribute("data-tipo"),
        ocrEstado: shell.getAttribute("data-ocr"),
        nombre: shell.getAttribute("data-nombre"),
        fecha: shell.getAttribute("data-fecha"),
        scroll: shell.getAttribute("data-scroll") === "1"  // TRON-22: lectura de firma
    };

    var state = {
        pdf: null, page: 1, total: 1, scale: 1, rotation: 0, fit: false, rendering: false, pendingPage: null,
        anot: [], armedField: null, ocrText: null, anotLoaded: false, trazLoaded: false, versLoaded: false,
        viewed: {}, leidoNotificado: false,
        viewMode: "scroll"  // scroll continuo por defecto en todos los visores (toggle disponible)
    };

    // ── TRON-22 — Gating de firma: avisa al stepper (pagina padre) cuando el
    // firmante vio TODAS las paginas del documento. Para imagen / no-PDF se avisa
    // al cargar (no hay paginacion). Solo tiene efecto si el padre define el hook.
    function firReportarLeido() {
        // Solo aplica cuando el visor se abrió desde el proceso de firma (el stepper
        // agrega &scroll=1). En "Ver" de Mis Firmas (&fir=1) u otros visores, no.
        if (!cfg.scroll) return;
        if (state.leidoNotificado) return;
        state.leidoNotificado = true;
        firAvisoLecturaCompleta();  // toast dentro del propio visor (siempre visible)
        try {
            if (window.parent && window.parent !== window && typeof window.parent.firDocumentoLeido === "function") {
                window.parent.firDocumentoLeido(cfg.doc);
            }
        } catch (e) { }
    }
    // TRON-22: aviso visible ENCIMA del visor (el toast del padre queda tapado por
    // el overlay del iframe). Se muestra dentro del propio documento del visor.
    function firAvisoLecturaCompleta() {
        try {
            var d = document.createElement("div");
            d.innerHTML = '<i class="fa fa-check-circle" style="margin-right:8px;"></i>Documento revisado completamente. Ya puede continuar con la firma.';
            d.style.cssText = "position:fixed;top:18px;right:18px;z-index:2147483647;background:#16A34A;color:#fff;" +
                "padding:13px 18px;border-radius:11px;box-shadow:0 12px 34px rgba(0,0,0,.32);" +
                "font:600 13px/1.4 -apple-system,Segoe UI,Roboto,sans-serif;max-width:340px;opacity:0;transition:opacity .3s;";
            document.body.appendChild(d);
            requestAnimationFrame(function () { d.style.opacity = "1"; });
            setTimeout(function () {
                d.style.opacity = "0";
                setTimeout(function () { if (d.parentNode) d.parentNode.removeChild(d); }, 400);
            }, 3800);
        } catch (e) { }
    }
    function firCheckLecturaCompleta() {
        if (!state.total) return;
        var n = 0; for (var k in state.viewed) { if (state.viewed.hasOwnProperty(k)) n++; }
        if (n >= state.total) { firReportarLeido(); firHabilitarFirmaVisor(); }
    }
    // TRON-22: habilita el botón Firmar del panel Gestión (visor "Ver") al terminar la lectura.
    function firHabilitarFirmaVisor() {
        var b = document.getElementById("vzBtnFirmar");
        if (b) { b.disabled = false; b.classList.remove("vz-g-btn-dis"); }
        var h = document.getElementById("vzFirmaHint"); if (h) h.style.display = "none";
    }

    // Parametros comunes para los servicios ashx.
    function qbase() {
        return "doc=" + encodeURIComponent(cfg.doc) + "&exp=" + encodeURIComponent(cfg.exp) +
               "&re=" + encodeURIComponent(cfg.re) + "&t=" + encodeURIComponent(cfg.t);
    }
    function getJSON(url, cb) {
        var x = new XMLHttpRequest();
        x.open("GET", url, true);
        x.onreadystatechange = function () {
            if (x.readyState === 4) { try { cb(JSON.parse(x.responseText)); } catch (e) { cb(null); } }
        };
        x.send();
    }
    function postForm(url, body, cb) {
        var x = new XMLHttpRequest();
        x.open("POST", url, true);
        x.setRequestHeader("Content-Type", "application/x-www-form-urlencoded");
        x.onreadystatechange = function () {
            if (x.readyState === 4) { try { cb(JSON.parse(x.responseText)); } catch (e) { cb(null); } }
        };
        x.send(body);
    }
    function esc(s) { var d = document.createElement("div"); d.textContent = (s == null ? "" : s); return d.innerHTML; }

    var elWrap = document.getElementById("vzCanvasWrap");
    var elLoading = document.getElementById("vzLoading");
    var elPageNum = document.getElementById("vzPageNum");
    var elPageCount = document.getElementById("vzPageCount");
    var elZoomSel = document.getElementById("vzZoomSel");
    var elThumbsList = document.getElementById("vzThumbsList");

    // ── Pestañas y paneles (sirve para PDF, imagen y errores) ──
    initTabs();
    initPanelToggle();
    initExtras();
    initAcciones();
    initOcr();

    // ── Imagen: no usa pdf.js ──
    if (cfg.formato === "jpg" || cfg.formato === "jpeg" || cfg.formato === "png" || cfg.formato === "gif") {
        elLoading.style.display = "none";
        var img = document.createElement("img");
        img.className = "vz-img";
        img.src = cfg.bin;
        elWrap.appendChild(img);
        document.getElementById("vzThumbs").style.display = "none";
        firReportarLeido(); firHabilitarFirmaVisor(); // TRON-22: imagen = una sola vista.
        return;
    }

    // ── No-PDF no visualizable inline ──
    if (cfg.formato !== "pdf") {
        elLoading.innerHTML = '<i class="fa fa-file-o"></i>&nbsp; Vista previa no disponible para este formato. Usa Descargar.';
        firReportarLeido(); firHabilitarFirmaVisor(); // TRON-22: sin preview no se puede exigir todas las paginas.
        document.getElementById("vzThumbs").style.display = "none";
        return;
    }

    // ── PDF.js ──
    if (typeof PDFJS === "undefined") {
        elLoading.innerHTML = '<i class="fa fa-exclamation-triangle"></i>&nbsp; No se pudo cargar el motor de PDF.';
        return;
    }
    try { PDFJS.workerSrc = cfg.worker; } catch (e) { }
    try { PDFJS.disableWorker = false; } catch (e) { }

    var loadingTask = PDFJS.getDocument(cfg.bin);
    var loadPromise = loadingTask.promise ? loadingTask.promise : loadingTask;
    loadPromise.then(function (pdf) {
        state.pdf = pdf;
        state.total = pdf.numPages;
        elPageCount.textContent = state.total;
        elPageNum.value = "1";
        if (state.viewMode === "scroll") { renderScroll(); } else { renderPage(1); }
        buildThumbnails();
    }).catch(function (err) {
        elLoading.innerHTML = '<i class="fa fa-exclamation-triangle"></i>&nbsp; Error al abrir el PDF.';
    });

    // ── Render de la pagina actual a canvas + capa de texto ──
    function renderPage(num) {
        if (!state.pdf || num < 1 || num > state.total) return;
        if (state.rendering) { state.pendingPage = num; return; }
        state.rendering = true;
        state.page = num;
        elPageNum.value = num;
        markThumbActive(num);
        // TRON-22: registra la pagina como vista para el gating de firma.
        state.viewed[num] = true;
        firCheckLecturaCompleta();

        state.pdf.getPage(num).then(function (page) {
            var scale = state.scale;
            if (state.fit) {
                var base = page.getViewport(1, state.rotation);
                var avail = elWrap.clientWidth - 48;
                scale = avail > 0 ? (avail / base.width) : 1;
            }
            var viewport = page.getViewport(scale, state.rotation);

            elWrap.innerHTML = "";
            var pageDiv = document.createElement("div");
            pageDiv.className = "vz-pagebox";
            pageDiv.style.width = Math.floor(viewport.width) + "px";
            pageDiv.style.height = Math.floor(viewport.height) + "px";

            var canvas = document.createElement("canvas");
            canvas.className = "vz-canvas";
            canvas.width = Math.floor(viewport.width);
            canvas.height = Math.floor(viewport.height);
            pageDiv.appendChild(canvas);

            var textLayer = document.createElement("div");
            textLayer.className = "vz-textlayer";
            textLayer.style.width = Math.floor(viewport.width) + "px";
            textLayer.style.height = Math.floor(viewport.height) + "px";
            pageDiv.appendChild(textLayer);

            elWrap.appendChild(pageDiv);
            state.curPageDiv = pageDiv;
            renderPins(pageDiv, num);

            var renderTask = page.render({ canvasContext: canvas.getContext("2d"), viewport: viewport });
            var rp = renderTask.promise ? renderTask.promise : renderTask;
            rp.then(function () {
                // Capa de texto (para selección / indexación manual).
                return page.getTextContent();
            }).then(function (textContent) {
                if (textContent && PDFJS.renderTextLayer) {
                    PDFJS.renderTextLayer({
                        textContent: textContent,
                        container: textLayer,
                        viewport: viewport,
                        textDivs: []
                    });
                }
                finishRender();
            }).catch(function () { finishRender(); });
        }).catch(function () { finishRender(); });
    }

    function finishRender() {
        state.rendering = false;
        if (state.pendingPage !== null) {
            var p = state.pendingPage; state.pendingPage = null; renderPage(p);
        }
    }

    // ── Modo scroll continuo (TRON-22 + default de todos los visores) ──
    // Apila las páginas en scroll vertical con render PEREZOSO (cada página se pinta
    // al acercarse al viewport). Marca cada página como vista al aparecer (gating).
    function renderScroll() {
        elWrap.innerHTML = "";
        elWrap.classList.add("vz-scrollmode");
        elWrap.style.overflowY = "auto";

        // Observer: marca la página vista (gating) + página actual + curPageDiv.
        var seenObs = window.IntersectionObserver ? new IntersectionObserver(function (ents) {
            for (var i = 0; i < ents.length; i++) {
                if (!ents[i].isIntersecting) continue;
                var pg = parseInt(ents[i].target.getAttribute("data-pg"), 10);
                if (isNaN(pg)) continue;
                state.viewed[pg] = true;
                state.page = pg; elPageNum.value = pg; markThumbActive(pg);
                state.curPageDiv = ents[i].target;
                firCheckLecturaCompleta();
            }
        }, { root: elWrap, threshold: 0.35 }) : null;

        // Observer: render perezoso al acercarse (rootMargin adelantado).
        var renderObs = window.IntersectionObserver ? new IntersectionObserver(function (ents) {
            for (var i = 0; i < ents.length; i++) {
                if (!ents[i].isIntersecting) continue;
                var div = ents[i].target;
                renderObs.unobserve(div);
                pintarPaginaScroll(div, parseInt(div.getAttribute("data-pg"), 10));
            }
        }, { root: elWrap, rootMargin: "1000px 0px" }) : null;

        function pintarPaginaScroll(div, num) {
            if (!div || div.getAttribute("data-rendered") === "1") return;
            div.setAttribute("data-rendered", "1");
            state.pdf.getPage(num).then(function (page) {
                var base = page.getViewport(1, 0);
                var avail = elWrap.clientWidth - 48;
                var scale = avail > 0 ? (avail / base.width) : 1;
                var viewport = page.getViewport(scale, 0);
                div.style.width = Math.floor(viewport.width) + "px";
                div.style.height = Math.floor(viewport.height) + "px";
                var canvas = document.createElement("canvas");
                canvas.className = "vz-canvas";
                canvas.width = Math.floor(viewport.width);
                canvas.height = Math.floor(viewport.height);
                div.appendChild(canvas);
                var textLayer = document.createElement("div");
                textLayer.className = "vz-textlayer";
                textLayer.style.width = Math.floor(viewport.width) + "px";
                textLayer.style.height = Math.floor(viewport.height) + "px";
                div.appendChild(textLayer);
                renderPins(div, num);
                var rt = page.render({ canvasContext: canvas.getContext("2d"), viewport: viewport });
                var rp = rt.promise ? rt.promise : rt;
                rp.then(function () { return page.getTextContent(); }).then(function (tc) {
                    if (tc && PDFJS.renderTextLayer) {
                        PDFJS.renderTextLayer({ textContent: tc, container: textLayer, viewport: viewport, textDivs: [] });
                    }
                }).catch(function () { });
            });
        }

        // Placeholders para todas las páginas (altura estimada con la página 1).
        state.pdf.getPage(1).then(function (p1) {
            var base = p1.getViewport(1, 0);
            var avail = elWrap.clientWidth - 48;
            var scale = avail > 0 ? (avail / base.width) : 1;
            var estW = Math.floor(base.width * scale), estH = Math.floor(base.height * scale);
            for (var n = 1; n <= state.total; n++) {
                var div = document.createElement("div");
                div.className = "vz-pagebox vz-scrollpage";
                div.setAttribute("data-pg", n);
                div.style.width = estW + "px";
                div.style.height = estH + "px";
                div.style.margin = "0 auto 16px";
                elWrap.appendChild(div);
                if (n === 1) state.curPageDiv = div;
                if (renderObs) { renderObs.observe(div); } else { pintarPaginaScroll(div, n); }
                if (seenObs) { seenObs.observe(div); } else { state.viewed[n] = true; firCheckLecturaCompleta(); }
            }
        });
    }

    // Navegación: en modo scroll hace scroll a la página; si no, la renderiza.
    function goToPage(n) {
        n = Math.min(Math.max(n, 1), state.total);
        if (state.viewMode === "scroll") {
            var pd = elWrap.querySelector('.vz-scrollpage[data-pg="' + n + '"]');
            if (pd && pd.scrollIntoView) pd.scrollIntoView({ behavior: "smooth", block: "start" });
        } else {
            renderPage(n);
        }
    }

    // ── Miniaturas ──
    function buildThumbnails() {
        elThumbsList.innerHTML = "";
        for (var i = 1; i <= state.total; i++) {
            (function (n) {
                var item = document.createElement("div");
                item.className = "vz-thumb";
                item.setAttribute("data-page", n);
                var num = document.createElement("div");
                num.className = "vz-thumb-n";
                num.textContent = n;
                item.appendChild(num);
                item.addEventListener("click", function () { goToPage(n); });
                elThumbsList.appendChild(item);

                state.pdf.getPage(n).then(function (page) {
                    var vp = page.getViewport(0.22, 0);
                    var c = document.createElement("canvas");
                    c.className = "vz-thumb-canvas";
                    c.width = Math.floor(vp.width);
                    c.height = Math.floor(vp.height);
                    item.insertBefore(c, num);
                    page.render({ canvasContext: c.getContext("2d"), viewport: vp });
                });
            })(i);
        }
        markThumbActive(1);
    }

    function markThumbActive(num) {
        var all = elThumbsList.querySelectorAll(".vz-thumb");
        for (var i = 0; i < all.length; i++) {
            all[i].className = (parseInt(all[i].getAttribute("data-page"), 10) === num) ? "vz-thumb active" : "vz-thumb";
        }
    }

    // ── Controles ──
    document.getElementById("vzPrev").addEventListener("click", function () { goToPage(state.page - 1); });
    document.getElementById("vzNext").addEventListener("click", function () { goToPage(state.page + 1); });
    elPageNum.addEventListener("change", function () {
        var n = parseInt(elPageNum.value, 10);
        if (!isNaN(n)) goToPage(Math.min(Math.max(n, 1), state.total));
    });
    // Zoom/rotar solo en modo página (en scroll continuo se usa ancho ajustado).
    document.getElementById("vzZoomIn").addEventListener("click", function () { if (state.viewMode !== "scroll") setScale(state.scale + 0.25); });
    document.getElementById("vzZoomOut").addEventListener("click", function () { if (state.viewMode !== "scroll") setScale(state.scale - 0.25); });
    elZoomSel.addEventListener("change", function () {
        if (state.viewMode === "scroll") return;
        if (elZoomSel.value === "fit") { state.fit = true; renderPage(state.page); }
        else { state.fit = false; setScale(parseFloat(elZoomSel.value)); }
    });
    document.getElementById("vzRotate").addEventListener("click", function () {
        if (state.viewMode === "scroll") return;
        state.rotation = (state.rotation + 90) % 360; renderPage(state.page);
    });
    // Alternar vista scroll continuo ⇄ página por página.
    (function () {
        var btn = document.getElementById("vzViewMode");
        if (!btn) return;
        function syncIcon() {
            btn.innerHTML = (state.viewMode === "scroll") ? '<i class="fa fa-file-o"></i>' : '<i class="fa fa-arrows-v"></i>';
            btn.title = (state.viewMode === "scroll") ? "Ver página por página" : "Ver en scroll continuo";
        }
        syncIcon();
        btn.addEventListener("click", function () {
            if (!state.pdf) return;
            if (state.viewMode === "scroll") {
                state.viewMode = "page";
                elWrap.classList.remove("vz-scrollmode");
                elWrap.style.overflowY = "";
                renderPage(state.page || 1);
            } else {
                state.viewMode = "scroll";
                renderScroll();
            }
            syncIcon();
        });
    })();
    document.getElementById("vzThumbsToggle").addEventListener("click", function () {
        var t = document.getElementById("vzThumbs");
        t.classList.toggle("vz-hidden");
    });

    function setScale(s) {
        s = Math.min(Math.max(s, 0.25), 4);
        state.scale = s; state.fit = false;
        // refleja en el select si coincide
        var found = false;
        for (var i = 0; i < elZoomSel.options.length; i++) {
            if (parseFloat(elZoomSel.options[i].value) === s) { elZoomSel.selectedIndex = i; found = true; break; }
        }
        renderPage(state.page);
    }

    // ── Pestañas ──
    function initTabs() {
        var tabs = document.querySelectorAll(".vz-tab");
        for (var i = 0; i < tabs.length; i++) {
            tabs[i].addEventListener("click", function () {
                var key = this.getAttribute("data-tab");
                var allTabs = document.querySelectorAll(".vz-tab");
                for (var j = 0; j < allTabs.length; j++) allTabs[j].classList.remove("active");
                this.classList.add("active");
                var panes = document.querySelectorAll(".vz-pane");
                for (var k = 0; k < panes.length; k++) {
                    panes[k].classList.toggle("active", panes[k].getAttribute("data-pane") === key);
                }
                lazyLoad(key);
            });
        }
    }

    function lazyLoad(key) {
        if (key === "anot" && !state.anotLoaded) { state.anotLoaded = true; cargarAnotaciones(); }
        if (key === "traz" && !state.trazLoaded) { state.trazLoaded = true; cargarTrazabilidad(); }
        if (key === "vers" && !state.versLoaded) { state.versLoaded = true; cargarVersiones(); }
    }

    // ── Acciones del documento (Firmar / Nueva versión / Compartir / Correo) ──
    // El visor corre en un iframe; delega en la página host (bandeja o expediente)
    // que ya tiene los modales/handlers de cada acción.
    function initAcciones() {
        var map = { vzActFirmar: "firmar", vzActVersion: "version", vzActCompartir: "compartir", vzActCorreo: "correo", vzActArchivar: "archivar" };
        for (var id in map) {
            (function (bid, accion) {
                var b = document.getElementById(bid);
                if (b) b.addEventListener("click", function () { hostAccion(accion); });
            })(id, map[id]);
        }
        // Archivar: solo para borradores (sin expediente). En un expediente ya está archivado.
        var bArch = document.getElementById("vzActArchivar");
        if (bArch) bArch.style.display = (!cfg.exp || cfg.exp === "0") ? "" : "none";

        // Validar / Aprobar: salta al panel "Gestión" si hay una tarea asignada.
        var bVal = document.getElementById("vzActValidar");
        if (bVal) bVal.addEventListener("click", function () {
            var tab = document.querySelector('.vz-tab[data-tab="gest"]');
            if (tab) tab.click();
            else alert("Este documento no tiene una tarea de validación/aprobación asignada.");
        });

        // Solicitar trámite (delegado al host).
        var bTram = document.getElementById("vzActTramite");
        if (bTram) bTram.addEventListener("click", function () { hostAccion("tramite"); });

        // Pantalla completa.
        var bFull = document.getElementById("vzActFull");
        if (bFull) bFull.addEventListener("click", vzToggleFull);
    }
    function vzToggleFull() {
        try {
            var el = document.documentElement;
            if (!document.fullscreenElement && !document.webkitFullscreenElement) {
                if (el.requestFullscreen) el.requestFullscreen();
                else if (el.webkitRequestFullscreen) el.webkitRequestFullscreen();
            } else {
                if (document.exitFullscreen) document.exitFullscreen();
                else if (document.webkitExitFullscreen) document.webkitExitFullscreen();
            }
        } catch (e) { }
    }
    function hostAccion(accion) {
        try {
            if (window.parent && window.parent !== window && typeof window.parent.vzHostAccion === "function") {
                window.parent.vzHostAccion(accion, cfg.doc, cfg.exp);
                return;
            }
        } catch (e) { }
        alert("Esta acción no está disponible en este contexto.");
    }

    // ── RF11/RF12 — Responder la tarea (Aprobar/Devolver/Rechazar) desde el panel Gestion ──
    window.vzGestionar = function (accion) {
        var pane = document.querySelector('.vz-pane[data-pane="gest"]');
        if (!pane) return;
        var val = pane.getAttribute("data-val");
        var txt = document.getElementById("vzGestComent");
        var comentario = txt ? txt.value.trim() : "";
        var msg = document.getElementById("vzGestMsg");
        function showMsg(cls, t) { if (msg) { msg.style.display = "block"; msg.className = "vz-g-msg " + cls; msg.textContent = t; } }

        if ((accion === "Devuelto" || accion === "Rechazado") && !comentario) {
            showMsg("err", "El comentario es obligatorio al devolver o rechazar.");
            if (txt) txt.focus();
            return;
        }
        var btns = pane.querySelectorAll(".vz-g-btn");
        for (var i = 0; i < btns.length; i++) btns[i].disabled = true;
        showMsg("wait", "Procesando…");

        var body = "val=" + encodeURIComponent(val) + "&accion=" + encodeURIComponent(accion) +
                   "&comentario=" + encodeURIComponent(comentario) +
                   "&re=" + encodeURIComponent(cfg.re) + "&t=" + encodeURIComponent(cfg.t);
        postForm("exp_validacion.ashx", body, function (r) {
            if (!r || r.error) {
                showMsg("err", (r && r.error) || "No se pudo procesar la respuesta.");
                for (var i = 0; i < btns.length; i++) btns[i].disabled = false;
                return;
            }
            showMsg("ok", "Respuesta registrada. Cerrando…");
            var handled = false;
            try {
                if (window.parent && window.parent !== window && window.parent.vzTareaRespondida) {
                    setTimeout(function () { window.parent.vzTareaRespondida(); }, 500);
                    handled = true;
                }
            } catch (e) { }
            if (!handled) {
                setTimeout(function () {
                    try {
                        if (window.parent && window.parent !== window && window.parent.vzCerrarVisor) { window.parent.vzCerrarVisor(); }
                        else { window.close(); }
                    } catch (e) { window.close(); }
                }, 500);
            }
        });
    };

    // ── TRON-21 — Firmar / Rechazar desde el panel Gestion del firmante ──
    // Cableado al stepper actual: cierra el visor y abre el stepper en la pagina
    // padre (Mis Firmas). El stepper ofrece Firmar y Rechazar (pasos 1-4).
    window.vzFirmar = function (doc, exp) {
        try {
            var p = window.parent;
            if (p && p !== window) {
                // Preferido: una sola llamada al padre (cierra overlay + abre stepper).
                if (typeof p.mfFirmarDesdeVisor === "function") { p.mfFirmarDesdeVisor(doc, exp); return; }
                if (typeof p.mfFirmar === "function") {
                    if (typeof p.vzCerrarVisor === "function") p.vzCerrarVisor();
                    p.mfFirmar(doc, exp);
                    return;
                }
            }
        } catch (e) { }
        // Fallback: ir a la bandeja Mis Firmas.
        window.location.href = "../Firma/mis_firmas.aspx";
    };
    window.vzRechazarFirma = function (doc, exp) {
        // El mismo stepper permite rechazar (comentario obligatorio) en la lectura.
        window.vzFirmar(doc, exp);
    };

    function initPanelToggle() {
        var btn = document.getElementById("vzTogglePanel");
        if (btn) btn.addEventListener("click", function () {
            document.getElementById("vzPanel").classList.toggle("vz-hidden");
        });
    }

    // Estado del OCR (async): se muestra y, si esta pendiente, se consulta cada 5s
    // hasta completarse para refrescar el indicador y la indexacion.
    function renderOcrChip(estado) {
        var el = document.getElementById("vzOcr");
        if (!el) return;
        if (estado === "Completado") { el.className = "vz-ocr ok"; el.innerHTML = '<i class="fa fa-check-circle"></i> OCR completado'; }
        else if (estado === "Procesando") { el.className = "vz-ocr wait"; el.innerHTML = '<i class="fa fa-spinner fa-spin"></i> OCR en proceso…'; }
        else if (estado === "Pendiente") { el.className = "vz-ocr wait"; el.innerHTML = '<i class="fa fa-clock-o"></i> OCR pendiente'; }
        else if (estado === "Error") { el.className = "vz-ocr err"; el.innerHTML = '<i class="fa fa-exclamation-triangle"></i> OCR con error'; }
        else { el.className = "vz-ocr"; el.innerHTML = ""; }
        // Reprocesar disponible cuando quedó pendiente o con error.
        if (estado === "Pendiente" || estado === "Error") {
            el.innerHTML += ' <a href="javascript:void(0)" id="vzReocrLink" style="margin-left:6px;font-weight:700;color:#2563EB;text-decoration:underline;"><i class="fa fa-refresh"></i> Reprocesar</a>';
            var lk = document.getElementById("vzReocrLink");
            if (lk) lk.onclick = vzReocr;
        }
    }
    function vzReocr() {
        var el = document.getElementById("vzOcr");
        if (el) { el.className = "vz-ocr wait"; el.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Reprocesando…'; }
        getJSON("/visor/data?op=reocr&" + qbase(), function (r) {
            if (!r || r.error) {
                if (el) { el.className = "vz-ocr err"; el.innerHTML = '<i class="fa fa-exclamation-triangle"></i> ' + esc((r && r.error) || "No se pudo reprocesar"); }
                return;
            }
            cfg.ocrEstado = "Procesando";
            initOcr();
        });
        return false;
    }
    function initOcr() {
        renderOcrChip(cfg.ocrEstado);
        // Si quedó en error, trae el motivo (guardado en el texto OCR) como tooltip.
        if (cfg.ocrEstado === "Error") {
            getJSON("/visor/data?op=ocr&" + qbase(), function (r) {
                if (r && r.texto) {
                    state.ocrText = r.texto;
                    var el = document.getElementById("vzOcr");
                    if (el) el.title = r.texto;
                }
            });
            return;
        }
        if (cfg.ocrEstado !== "Pendiente" && cfg.ocrEstado !== "Procesando") return;
        var intentos = 0;
        var iv = setInterval(function () {
            intentos++;
            getJSON("/visor/data?op=ocr&" + qbase(), function (r) {
                if (!r) return;
                if (r.estado !== cfg.ocrEstado) {
                    cfg.ocrEstado = r.estado;
                    renderOcrChip(r.estado);
                    state.ocrText = r.texto || state.ocrText;
                }
                if (r.estado === "Completado" || r.estado === "Error" || r.estado === "No_Aplica" || intentos > 60) {
                    clearInterval(iv);
                }
            });
        }, 5000);
    }

    // RF04 - Indexación asistida / Anotaciones / Trazabilidad / Versiones
    function initExtras() {
        var bIdx = document.getElementById("vzBtnIndexar");
        if (bIdx) bIdx.addEventListener("click", abrirIndexacion);
        var bNota = document.getElementById("vzBtnNota");
        if (bNota) bNota.addEventListener("click", armarNota);
        var bPub = document.getElementById("vzAnotPub");
        if (bPub) bPub.addEventListener("click", publicarComentario);

        // Captura manual (indexación) y colocación de notas: clic/selección en el documento.
        if (elWrap) {
            elWrap.addEventListener("mouseup", function () {
                if (!state.armedField) return;
                var sel = (window.getSelection && window.getSelection().toString() || "").trim();
                if (sel) {
                    state.armedField.value = sel;
                    desarmarCampo();
                }
            });
            elWrap.addEventListener("click", function (ev) {
                if (!state.notaMode) return;
                // En scroll continuo, la nota se ubica sobre la página realmente clickeada.
                var box = (ev.target && ev.target.closest) ? ev.target.closest(".vz-pagebox") : null;
                if (box) {
                    state.curPageDiv = box;
                    var pg = parseInt(box.getAttribute("data-pg"), 10);
                    if (!isNaN(pg)) state.page = pg;
                } else {
                    box = state.curPageDiv;
                }
                if (!box) return;
                var r = box.getBoundingClientRect();
                var x = (ev.clientX - r.left) / r.width;
                var y = (ev.clientY - r.top) / r.height;
                if (x < 0 || x > 1 || y < 0 || y > 1) return;
                state.notaMode = false;
                fijarPosicionNota(state.page, x, y);
            });
        }
    }

    // ── Editar metadatos (datos básicos + tipo documental + metadatos, con ayuda de OCR) ──
    function abrirIndexacion() {
        var cont = document.getElementById("vzMetaForm");
        var read = document.getElementById("vzMetaRead");
        if (!cont) return;
        cont.innerHTML = '<div class="vz-loading2"><i class="fa fa-spinner fa-spin"></i> Cargando…</div>';
        cont.style.display = "block";
        if (read) read.style.display = "none";

        state.tipoSel = (cfg.tipo && cfg.tipo !== "0") ? cfg.tipo : "0";

        function cargarCamposYRender(tiposList) {
            getJSON("/visor/data?op=campos&tipo=" + encodeURIComponent(state.tipoSel) + "&" + qbase(), function (campos) {
                getJSON("/visor/data?op=ocr&" + qbase(), function (ocr) {
                    state.ocrText = (ocr && ocr.texto) ? ocr.texto : "";
                    state.ocrEstadoIdx = (ocr && ocr.estado) ? ocr.estado : "No_Aplica";
                    renderFormIndexacion(cont, campos || [], state.ocrEstadoIdx, false);
                });
            });
        }
        if (state.tiposList) {
            cargarCamposYRender(state.tiposList);
        } else {
            getJSON("/visor/data?op=tipos&" + qbase(), function (tipos) {
                state.tiposList = tipos || [];
                cargarCamposYRender(state.tiposList);
            });
        }
    }

    // Al cambiar el tipo documental: recarga los campos (preserva nombre/fecha y el toggle OCR).
    function recargarCamposPorTipo(nuevoTipo) {
        cfg.nombre = (document.getElementById("vzfNombre") || {}).value || cfg.nombre;
        cfg.fecha = (document.getElementById("vzfFecha") || {}).value || cfg.fecha;
        var autoOn = !!((document.getElementById("vzfOcrAuto") || {}).checked);
        state.tipoSel = nuevoTipo;
        var cont = document.getElementById("vzMetaForm");
        cont.innerHTML = '<div class="vz-loading2"><i class="fa fa-spinner fa-spin"></i> Cargando campos…</div>';
        getJSON("/visor/data?op=campos&tipo=" + encodeURIComponent(nuevoTipo) + "&" + qbase(), function (campos) {
            renderFormIndexacion(cont, campos || [], state.ocrEstadoIdx || "No_Aplica", autoOn);
        });
    }

    function tipoOptions(sel) {
        var lista = state.tiposList || [];
        var h = '<option value="0">— Sin tipo —</option>';
        for (var i = 0; i < lista.length; i++) {
            var s = (String(lista[i].reg) === String(sel)) ? ' selected' : '';
            h += '<option value="' + lista[i].reg + '"' + s + '>' + esc(lista[i].nombre) + '</option>';
        }
        return h;
    }

    function renderFormIndexacion(cont, campos, ocrEstado, autoOn) {
        autoOn = !!autoOn;
        var h = "";
        // Datos básicos del documento.
        h += '<div class="vz-idx-row"><label class="vz-idx-l">Nombre del documento <span class="vz-req">*</span></label>' +
             '<div class="vz-idx-in"><input type="text" id="vzfNombre" value="' + esc(cfg.nombre || "") + '" /></div></div>';
        h += '<div class="vz-idx-row"><label class="vz-idx-l">Fecha del documento <span class="vz-req">*</span></label>' +
             '<div class="vz-idx-in"><input type="date" id="vzfFecha" value="' + esc(cfg.fecha || "") + '" /></div></div>';
        // Tipo documental: reseleccionable (recarga los metadatos del tipo elegido).
        h += '<div class="vz-idx-row"><label class="vz-idx-l">Tipo documental</label>' +
             '<div class="vz-idx-in"><select id="vzfTipo">' + tipoOptions(state.tipoSel) + '</select></div></div>';

        if (campos.length) {
            h += '<div class="vz-meta-sec">Metadatos del tipo documental</div>';
            if (ocrEstado === "Completado") {
                // Autocompletado por OCR: opt-in explícito.
                h += '<label class="vz-ocr-auto"><input type="checkbox" id="vzfOcrAuto"' + (autoOn ? ' checked' : '') + ' /> ' +
                     '<span><i class="fa fa-magic"></i> Autocompletar con OCR' +
                     '<span class="vz-ocr-auto-h">Propone valores en los campos vacíos; tú confirmas o corriges. (Desmarca para llenarlos a mano.)</span></span></label>';
            } else {
                h += '<div class="vz-aviso"><i class="fa fa-info-circle"></i> El texto del documento aún se está indexando (OCR no disponible). ' +
                     'Captura los valores manualmente.</div>';
            }
            for (var i = 0; i < campos.length; i++) {
                var c = campos[i];
                h += '<div class="vz-idx-row">' +
                     '<label class="vz-idx-l">' + esc(c.nombre) + (c.obligatorio ? ' <span class="vz-req">*</span>' : '') + '</label>' +
                     '<div class="vz-idx-in">' +
                     '<input type="text" id="vzf_' + c.reg + '" data-tipo="' + esc(c.tipo) + '" value="' + esc(c.valor || "") + '" />' +
                     '<button type="button" class="vz-cap" data-target="vzf_' + c.reg + '" title="Capturar del documento"><i class="fa fa-crosshairs"></i></button>' +
                     '</div></div>';
            }
        }
        h += '<div class="vz-idx-actions">' +
             '<button type="button" class="vz-mini-btn" id="vzIdxCancel">Cancelar</button>' +
             '<button type="button" class="vz-mini-btn primary" id="vzIdxSave"><i class="fa fa-save"></i> Guardar</button></div>';
        h += '<div id="vzIdxMsg"></div>';
        cont.innerHTML = h;

        var caps = cont.querySelectorAll(".vz-cap");
        for (var k = 0; k < caps.length; k++) {
            caps[k].addEventListener("click", function () { armarCampo(this.getAttribute("data-target")); });
        }
        var selTipo = document.getElementById("vzfTipo");
        if (selTipo) selTipo.addEventListener("change", function () { recargarCamposPorTipo(this.value); });
        var chk = document.getElementById("vzfOcrAuto");
        if (chk) chk.addEventListener("change", function () { aplicarAutoOcr(campos, this.checked); });
        // Si el usuario escribe en un campo, deja de considerarse "autocompletado".
        for (var j = 0; j < campos.length; j++) {
            (function (reg) {
                var el = document.getElementById("vzf_" + reg);
                if (el) el.addEventListener("input", function () { el.removeAttribute("data-auto"); });
            })(campos[j].reg);
        }
        // Si el toggle venía activo (al recargar por tipo), reaplica la propuesta.
        if (autoOn) aplicarAutoOcr(campos, true);

        document.getElementById("vzIdxCancel").addEventListener("click", cerrarIndexacion);
        document.getElementById("vzIdxSave").addEventListener("click", function () { guardarIndexacion(campos); });
    }

    // Aplica (o retira) la auto-propuesta del OCR en los campos de metadatos.
    function aplicarAutoOcr(campos, on) {
        for (var i = 0; i < campos.length; i++) {
            var el = document.getElementById("vzf_" + campos[i].reg);
            if (!el) continue;
            if (on) {
                if (!el.value) {
                    var prop = proponerValor(campos[i]);
                    if (prop) { el.value = prop; el.setAttribute("data-auto", "1"); }
                }
            } else if (el.getAttribute("data-auto") === "1") {
                el.value = "";
                el.removeAttribute("data-auto");
            }
        }
    }

    // Heurística simple de auto-propuesta desde el texto OCR según el nombre del campo.
    function proponerValor(campo) {
        var txt = state.ocrText || "";
        if (!txt) return "";
        var n = (campo.nombre || "").toLowerCase();
        var m;
        if (/fecha/.test(n)) { m = txt.match(/(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})/); if (m) return m[1]; }
        if (/nit|c[eé]dula|cc|identificaci/.test(n)) { m = txt.match(/(\d{1,3}(?:[\.\d]{5,})(?:-\d)?)/); if (m) return m[1]; }
        if (/valor|monto|precio|presupuesto/.test(n)) { m = txt.match(/\$\s*([\d\.,]+)/); if (m) return m[1]; }
        if (/factura|n[uú]mero|no\.?|consecutivo/.test(n)) { m = txt.match(/(?:no\.?|n[uú]mero|factura)[:\s#]*([A-Z0-9\-]{3,})/i); if (m) return m[1]; }
        return "";
    }

    function armarCampo(id) {
        desarmarCampo();
        state.armedField = document.getElementById(id);
        if (state.armedField) {
            state.armedField.classList.add("vz-armed");
            var msg = document.getElementById("vzIdxMsg");
            if (msg) msg.innerHTML = '<div class="vz-aviso"><i class="fa fa-hand-pointer-o"></i> Selecciona el texto en el documento para llenar el campo.</div>';
        }
    }
    function desarmarCampo() {
        if (state.armedField) state.armedField.classList.remove("vz-armed");
        state.armedField = null;
        var msg = document.getElementById("vzIdxMsg");
        if (msg) msg.innerHTML = "";
    }

    function guardarIndexacion(campos) {
        var msg0 = document.getElementById("vzIdxMsg");
        var nombre = (document.getElementById("vzfNombre") || {}).value || "";
        var fecha = (document.getElementById("vzfFecha") || {}).value || "";
        if (!nombre.trim() || !fecha.trim()) {
            if (msg0) msg0.innerHTML = '<div class="vz-aviso err">El nombre y la fecha son obligatorios.</div>';
            return;
        }
        var tipo = (document.getElementById("vzfTipo") || {}).value || "0";
        var body = qbase() + "&op=guardar" +
                   "&nombre=" + encodeURIComponent(nombre) +
                   "&fecha=" + encodeURIComponent(fecha) +
                   "&tipo=" + encodeURIComponent(tipo);
        for (var i = 0; i < campos.length; i++) {
            var el = document.getElementById("vzf_" + campos[i].reg);
            if (el) body += "&meta_" + campos[i].reg + "=" + encodeURIComponent(el.value || "");
        }
        postForm("/visor/data", body, function (res) {
            var msg = document.getElementById("vzIdxMsg");
            if (res && res.success) {
                if (msg) msg.innerHTML = '<div class="vz-aviso ok"><i class="fa fa-check"></i> Metadatos guardados.</div>';
                setTimeout(cerrarIndexacion, 800);
            } else {
                if (msg) msg.innerHTML = '<div class="vz-aviso err">No se pudo guardar.</div>';
            }
        });
    }
    function cerrarIndexacion() {
        desarmarCampo();
        var cont = document.getElementById("vzMetaForm");
        var read = document.getElementById("vzMetaRead");
        if (cont) { cont.style.display = "none"; cont.innerHTML = ""; }
        if (read) read.style.display = "block";
    }

    // ── Anotaciones ──
    function cargarAnotaciones() {
        var cont = document.getElementById("vzAnotList");
        if (!cont) return;
        getJSON("exp_anotaciones.ashx?op=list&" + qbase(), function (lista) {
            state.anot = lista || [];
            renderAnotList();
            if (state.viewMode === "scroll") {
                // Repinta los pines en TODAS las páginas apiladas.
                var pages = elWrap.querySelectorAll(".vz-scrollpage");
                for (var i = 0; i < pages.length; i++) {
                    var pg = parseInt(pages[i].getAttribute("data-pg"), 10);
                    if (!isNaN(pg)) renderPins(pages[i], pg);
                }
            } else if (state.curPageDiv) {
                renderPins(state.curPageDiv, state.page);
            }
        });
    }
    function anotIniciales(nombre) {
        var s = (nombre || "").trim(); if (!s) return "?";
        var p = s.split(/\s+/);
        var a = p[0].charAt(0); var b = (p.length > 1) ? p[p.length - 1].charAt(0) : "";
        return (a + b).toUpperCase();
    }
    function renderAnotList() {
        var cont = document.getElementById("vzAnotList");
        if (!cont) return;
        if (!state.anot.length) { cont.innerHTML = '<div class="vz-anot-empty"><i class="fa fa-comments-o"></i><div>Aún no hay anotaciones. Sé el primero en comentar.</div></div>'; return; }
        var h = "";
        for (var i = 0; i < state.anot.length; i++) {
            var a = state.anot[i];
            var esRes = (a.tipo === "Resaltado");
            var badge = (esRes ? "Resaltado" : "Comentario") + " · pág. " + a.pagina;
            h += '<div class="vz-anot" data-page="' + a.pagina + '" data-reg="' + a.reg + '">' +
                 '<div class="vz-anot-top">' +
                 '<span class="vz-anot-av">' + esc(anotIniciales(a.autor)) + '</span>' +
                 '<div class="vz-anot-id"><div class="vz-anot-nm">' + esc(a.autor || "Usuario") + '</div>' +
                 '<div class="vz-anot-meta">' + esc(a.fecha) + '</div></div>' +
                 '<span class="vz-anot-badge ' + (esRes ? 'res' : 'com') + '">' + esc(badge) + '</span>' +
                 '<button type="button" class="vz-anot-del" data-reg="' + a.reg + '" title="Eliminar"><i class="fa fa-trash-o"></i></button>' +
                 '</div>' +
                 '<div class="vz-anot-tx">' + esc(a.contenido) + '</div></div>';
        }
        cont.innerHTML = h;
        var items = cont.querySelectorAll(".vz-anot");
        for (var k = 0; k < items.length; k++) {
            items[k].addEventListener("click", function (e) {
                if (e.target.closest(".vz-anot-del")) return;
                irAAnotacion(this.getAttribute("data-reg"));
            });
        }
        var dels = cont.querySelectorAll(".vz-anot-del");
        for (var d = 0; d < dels.length; d++) {
            dels[d].addEventListener("click", function (e) {
                e.stopPropagation();
                eliminarNota(this.getAttribute("data-reg"));
            });
        }
    }
    // Publica el contenido del cuadro. Si hay una posición marcada en el documento,
    // la nota queda ubicada (con pin); si no, es un comentario general de la página.
    function publicarComentario() {
        var ta = document.getElementById("vzAnotTxt");
        if (!ta) return;
        var txt = (ta.value || "").trim();
        if (!txt) { ta.focus(); return; }
        var btn = document.getElementById("vzAnotPub");
        if (btn) btn.disabled = true;
        var pos = state.pendingPos;
        var body = qbase() + "&op=save&tipo=Nota&pagina=" + (pos ? pos.page : state.page) + "&contenido=" + encodeURIComponent(txt);
        if (pos) body += "&x=" + pos.x.toFixed(4) + "&y=" + pos.y.toFixed(4);
        postForm("exp_anotaciones.ashx", body, function (res) {
            if (btn) btn.disabled = false;
            if (res && res.success) {
                ta.value = "";
                cancelarPosicionNota();
                state.anotLoaded = true; cargarAnotaciones();
            }
        });
    }
    // Activa el modo "marcar posición": el siguiente clic en el documento fija el punto.
    function armarNota() {
        state.notaMode = true;
        var hint = document.getElementById("vzNotaHint");
        if (hint) hint.textContent = "Haz clic en el documento para marcar la posición de la nota.";
    }
    // Fija la posición marcada (sin pedir texto): muestra un marcador y enfoca el cuadro.
    function fijarPosicionNota(page, x, y) {
        state.pendingPos = { page: page, x: x, y: y };
        quitarPinTmp();
        if (state.curPageDiv) {
            var pin = document.createElement("div");
            pin.className = "vz-pin vz-pin-tmp";
            pin.style.left = (x * 100) + "%";
            pin.style.top = (y * 100) + "%";
            pin.innerHTML = '<i class="fa fa-map-marker"></i>';
            state.curPageDiv.appendChild(pin);
            state.tmpPin = pin;
        }
        var hint = document.getElementById("vzNotaHint");
        if (hint) hint.innerHTML = '<i class="fa fa-map-marker"></i> Posición marcada en pág. ' + page + '. Escribe el mensaje y pulsa Publicar. <a href="javascript:void(0)" id="vzNotaCancel">cancelar</a>';
        var cancel = document.getElementById("vzNotaCancel");
        if (cancel) cancel.onclick = cancelarPosicionNota;
        var ta = document.getElementById("vzAnotTxt");
        if (ta) ta.focus();
    }
    function cancelarPosicionNota() {
        state.pendingPos = null;
        quitarPinTmp();
        var hint = document.getElementById("vzNotaHint");
        if (hint) hint.textContent = "";
    }
    function quitarPinTmp() {
        if (state.tmpPin && state.tmpPin.parentNode) state.tmpPin.parentNode.removeChild(state.tmpPin);
        state.tmpPin = null;
    }
    function eliminarNota(reg) {
        if (!window.confirm("¿Eliminar esta anotación?")) return;
        postForm("exp_anotaciones.ashx", qbase() + "&op=delete&areg=" + encodeURIComponent(reg), function (res) {
            if (res && res.success) cargarAnotaciones();
        });
    }
    function renderPins(pageDiv, num) {
        if (!pageDiv || !state.anot || !state.anot.length) return;
        for (var i = 0; i < state.anot.length; i++) {
            var a = state.anot[i];
            if (parseInt(a.pagina, 10) !== num) continue;
            if (a.x == null) continue;
            if (Number(a.x) === 0 && Number(a.y) === 0) continue; // comentario general (sin pin)
            var resaltado = (String(a.reg) === String(state.highlightReg));
            var pin = document.createElement("div");
            pin.className = "vz-pin" + (resaltado ? " vz-pin-hl" : "");
            pin.style.left = (a.x * 100) + "%";
            pin.style.top = (a.y * 100) + "%";
            pin.title = a.contenido;
            pin.setAttribute("data-reg", a.reg);
            pin.innerHTML = '<i class="fa fa-comment"></i>';
            (function (ann) { pin.addEventListener("click", function (e) { e.stopPropagation(); mostrarCallout(pageDiv, ann); }); })(a);
            pageDiv.appendChild(pin);
        }
    }
    // Ir a una anotación: si está en la página actual no re-renderiza (evita que la
    // hoja parpadee/desaparezca); si está en otra página, la abre. Luego enfoca el pin.
    function irAAnotacion(reg) {
        var ann = null;
        for (var z = 0; z < state.anot.length; z++) { if (String(state.anot[z].reg) === String(reg)) { ann = state.anot[z]; break; } }
        if (!ann) return;
        state.highlightReg = reg;
        var posicionada = (ann.x != null) && !(Number(ann.x) === 0 && Number(ann.y) === 0);
        // El texto SIEMPRE se ve en un aviso flotante (nunca se mueve la hoja).
        mostrarNotaFlotante(ann, posicionada);
        if (!posicionada) return;
        // Además, si está ubicada, se resalta su pin en el sitio (sin scroll).
        var pg = parseInt(ann.pagina, 10) || 1;
        if (state.viewMode === "scroll") {
            // Scroll a la página y resalta el pin (ya está pintado por página).
            goToPage(pg);
            setTimeout(function () {
                var pd = elWrap.querySelector('.vz-scrollpage[data-pg="' + pg + '"]');
                if (pd) marcarPin(pd, ann);
            }, 250);
        } else if (pg === state.page && state.curPageDiv) {
            marcarPin(state.curPageDiv, ann);
        } else {
            renderPage(pg);
            setTimeout(function () { if (state.curPageDiv) marcarPin(state.curPageDiv, ann); }, 150);
        }
    }
    // Resalta el pin de la anotación y dibuja su cuadro en el sitio. NO hace scroll.
    function marcarPin(pageDiv, ann) {
        var pins = pageDiv.querySelectorAll(".vz-pin");
        for (var i = 0; i < pins.length; i++) {
            if (pins[i].getAttribute("data-reg") === String(ann.reg)) pins[i].classList.add("vz-pin-hl");
            else pins[i].classList.remove("vz-pin-hl");
        }
        mostrarCallout(pageDiv, ann);
    }
    // Aviso flotante SIEMPRE visible con el texto del comentario (no toca la hoja).
    function mostrarNotaFlotante(ann, posicionada) {
        var v = document.querySelector(".vz-viewer") || elWrap;
        var old = document.getElementById("vzNotaFloat");
        if (old && old.parentNode) old.parentNode.removeChild(old);
        var sub = posicionada ? ("ubicado en pág. " + ann.pagina) : ("comentario general · pág. " + ann.pagina);
        var box = document.createElement("div");
        box.id = "vzNotaFloat";
        box.className = "vz-notafloat";
        box.innerHTML = '<button type="button" class="vz-callout-x">&times;</button>' +
                        '<div class="vz-callout-au">' + esc(ann.autor || "Usuario") + ' · ' + sub + '</div>' +
                        '<div class="vz-callout-tx">' + esc(ann.contenido) + '</div>';
        v.appendChild(box);
        var bx = box.querySelector(".vz-callout-x");
        if (bx) bx.addEventListener("click", function () { if (box.parentNode) box.parentNode.removeChild(box); });
        setTimeout(function () { if (box.parentNode) box.parentNode.removeChild(box); }, 8000);
    }
    // Cuadro flotante con el texto del comentario, anclado sobre el pin.
    function mostrarCallout(pageDiv, a) {
        if (!pageDiv) return;
        var old = pageDiv.querySelector(".vz-callout");
        if (old && old.parentNode) old.parentNode.removeChild(old);
        var box = document.createElement("div");
        box.className = "vz-callout";
        box.style.left = (a.x * 100) + "%";
        box.style.top = (a.y * 100) + "%";
        box.innerHTML = '<button type="button" class="vz-callout-x">&times;</button>' +
                        '<div class="vz-callout-au">' + esc(a.autor || "Usuario") + '</div>' +
                        '<div class="vz-callout-tx">' + esc(a.contenido) + '</div>';
        pageDiv.appendChild(box);
        var bx = box.querySelector(".vz-callout-x");
        if (bx) bx.addEventListener("click", function (e) { e.stopPropagation(); if (box.parentNode) box.parentNode.removeChild(box); });
    }

    // ── Trazabilidad ──
    function cargarTrazabilidad() {
        var cont = document.getElementById("vzTrazList");
        if (!cont) return;
        cont.innerHTML = '<div class="vz-loading2"><i class="fa fa-spinner fa-spin"></i></div>';
        getJSON("/visor/data?op=traz&" + qbase(), function (lista) {
            if (!lista || !lista.length) { cont.innerHTML = '<div class="vz-soon">Sin eventos de trazabilidad.</div>'; return; }
            var h = "";
            for (var i = 0; i < lista.length; i++) {
                var e = lista[i];
                // Usuario = snapshot del momento: nombre · cargo · rol (RF06).
                var ident = [e.cargo, e.rol].filter(function (x) { return x && x !== "Sin especificar"; }).join(" · ");
                var ipTxt = (e.ip && e.ip !== "0.0.0.0") ? e.ip : "no registrada";
                h += '<div class="vz-trz"><div class="vz-trz-a">' + esc(e.accion) + '</div>' +
                     '<div class="vz-trz-m">' + esc(e.usuario) + ' · ' + esc(e.fecha) + '</div>' +
                     (ident ? '<div class="vz-trz-u">' + esc(ident) + '</div>' : '') +
                     '<div class="vz-trz-ip"><i class="fa fa-globe"></i> IP: ' + esc(ipTxt) + '</div>' +
                     fmtDetalle(e.detalle) + '</div>';
            }
            cont.innerHTML = h;
        });
    }

    // Etiqueta legible para una clave del detalle (campo técnico → texto humano).
    var VZ_LBL = {
        nombre: "Nombre", folios: "Folios", flujo: "Flujo", soporte: "Soporte", origen: "Origen",
        hash_sha256: "Hash SHA-256", hash_anterior: "Hash anterior", hash_nuevo: "Hash nuevo",
        para: "Para", archivo: "Archivo", destino: "Destino", tipo: "Tipo", permisos: "Permisos",
        usuario: "Usuario", rol: "Rol", justificacion: "Justificación", accion: "Acción",
        antes: "Antes", despues: "Después", via: "Vía", respuesta: "Respuesta", comentarios: "Comentarios",
        version_anterior: "Versión anterior", version_nueva: "Versión nueva", asignado: "Asignado"
    };
    function fmtLbl(k) {
        if (VZ_LBL[k]) return VZ_LBL[k];
        var s = String(k).replace(/_/g, " ");
        return s.charAt(0).toUpperCase() + s.slice(1);
    }

    // Formatea el DETALLE_JSON del evento como "Campo: anterior → nuevo" (RF06).
    function fmtDetalle(detalle) {
        if (!detalle) return "";
        var obj;
        try { obj = JSON.parse(detalle); } catch (ex) { return ""; }
        if (!obj || typeof obj !== "object") return "";
        var filas = "";
        for (var k in obj) {
            if (!obj.hasOwnProperty(k)) continue;
            var v = obj[k];
            var txt;
            if (v && typeof v === "object" && ("antes" in v || "despues" in v)) {
                txt = '<span class="vz-trz-ar">' + esc(String(v.antes == null ? "" : v.antes)) +
                      ' → </span>' + esc(String(v.despues == null ? "" : v.despues));
            } else {
                txt = esc(String(v == null ? "" : v));
            }
            filas += '<div class="vz-trz-dl"><span class="vz-trz-dk">' + esc(fmtLbl(k)) + ':</span> ' +
                     '<span class="vz-trz-dv">' + txt + '</span></div>';
        }
        if (!filas) return "";
        return '<div class="vz-trz-d">' + filas + '</div>';
    }

    // ── Versiones ──
    function cargarVersiones() {
        var cont = document.getElementById("vzVersList");
        if (!cont) return;
        cont.innerHTML = '<div class="vz-loading2"><i class="fa fa-spinner fa-spin"></i></div>';
        getJSON("/visor/data?op=vers&" + qbase(), function (lista) {
            if (!lista || !lista.length) { cont.innerHTML = '<div class="vz-soon">Sin historial de versiones.</div>'; return; }
            var h = '<div class="vz-vtable-wrap"><table class="vz-vtable">' +
                    '<thead><tr><th>Versión</th><th>Fecha</th><th>Usuario</th><th>Justificación</th><th>Acciones</th></tr></thead><tbody>';
            for (var i = 0; i < lista.length; i++) {
                var v = lista[i];
                var verTxt = 'v' + esc(v.version) + (v.actual ? ' <span class="vz-ver-act">actual</span>' : '');
                var acc;
                if (v.descargable) {
                    acc = '<a class="vz-vlink" target="_blank" href="/visor/bin?doc=' + encodeURIComponent(v.reg) +
                          '&exp=' + encodeURIComponent(cfg.exp) + '&re=' + encodeURIComponent(cfg.re) +
                          '&t=' + encodeURIComponent(cfg.t) + '&dl=1"><i class="fa fa-download"></i> Descargar</a>';
                } else {
                    acc = '<span class="vz-vmuted">—</span>';
                }
                h += '<tr>' +
                     '<td class="vz-vver">' + verTxt + '</td>' +
                     '<td>' + esc(v.fecha) + '</td>' +
                     '<td>' + esc(v.usuario) + '</td>' +
                     '<td>' + (v.justificacion ? esc(v.justificacion) : '<span class="vz-vmuted">—</span>') + '</td>' +
                     '<td>' + acc + '</td>' +
                     '</tr>';
            }
            h += '</tbody></table></div>';
            cont.innerHTML = h;
        });
    }

})();

/* ──────────────────────────────────────────────────────────────────────────
   Autofirma — Ubicar la firma en el documento.
   Solo cuando el visor se abre con &ubic=1 (lo agrega el stepper en autofirma).
   El firmante dibuja un recuadro sobre una página; se calculan las coordenadas
   en % (origen arriba-izquierda) y se avisan al stepper (padre) por
   window.parent.firUbicacionFirma(pagina, x, y, w, h). Módulo aislado: opera
   sobre el DOM ya renderizado por el visor (páginas .vz-scrollpage).
   ────────────────────────────────────────────────────────────────────────── */
(function () {
    "use strict";
    if (!/[?&]ubic=1/.test(location.search)) return;
    var wrap = document.getElementById("vzCanvasWrap");
    if (!wrap) return;

    var RATIO = 200 / 660;   // alto/ancho de la cajita de firma
    var active = false, drawing = false, startPg = null, startX = 0, startY = 0, box = null, placed = null;

    // Barra superior con instrucción + botones (activar / rehacer). Se coloca en el
    // contenedor .vz-viewer (encima del área de páginas) porque vzCanvasWrap se limpia
    // (innerHTML="") cada vez que el visor pinta el PDF y se llevaría la barra.
    var bar = document.createElement("div");
    bar.style.cssText = "flex:0 0 auto;display:flex;align-items:center;gap:12px;" +
        "padding:10px 16px;background:#0EA5E9;color:#fff;font:600 13px/1.3 'Segoe UI',Roboto,sans-serif;" +
        "box-shadow:0 2px 8px rgba(0,0,0,.16);";
    bar.innerHTML =
        '<i class="fa fa-hand-pointer-o"></i>' +
        '<span id="vzUbicMsg" style="flex:1;">Dibuje el recuadro donde desea que aparezca su firma en el documento.</span>' +
        '<button type="button" id="vzUbicToggle" style="border:0;border-radius:7px;padding:6px 14px;font-weight:700;cursor:pointer;background:#fff;color:#0369A1;">Ubicar firma</button>' +
        '<button type="button" id="vzUbicClear" style="display:none;border:1px solid rgba(255,255,255,.65);border-radius:7px;padding:6px 14px;font-weight:700;cursor:pointer;background:transparent;color:#fff;">Rehacer</button>';
    var viewer = wrap.parentNode || wrap;
    viewer.insertBefore(bar, wrap);

    var btnToggle = document.getElementById("vzUbicToggle");
    var btnClear = document.getElementById("vzUbicClear");
    var msg = document.getElementById("vzUbicMsg");

    function setActive(on) {
        active = on;
        wrap.style.cursor = on ? "crosshair" : "";
        wrap.style.userSelect = on ? "none" : "";
        btnToggle.textContent = on ? "Cancelar" : (placed ? "Reubicar" : "Ubicar firma");
    }
    btnToggle.addEventListener("click", function () { setActive(!active); });
    btnClear.addEventListener("click", function () {
        if (box && box.parentNode) box.parentNode.removeChild(box);
        box = null; placed = null;
        btnClear.style.display = "none";
        msg.textContent = "Dibuje el recuadro donde desea que aparezca su firma en el documento.";
        setActive(true);
    });

    function pageFromNode(node) {
        while (node && node !== wrap) {
            if (node.classList && node.classList.contains("vz-scrollpage")) return node;
            node = node.parentNode;
        }
        return null;
    }

    wrap.addEventListener("mousedown", function (e) {
        if (!active) return;
        var pg = pageFromNode(e.target);
        if (!pg) return;
        e.preventDefault();
        if (getComputedStyle(pg).position === "static") pg.style.position = "relative";
        startPg = pg;
        var r = pg.getBoundingClientRect();
        startX = e.clientX - r.left;
        startY = e.clientY - r.top;
        if (box && box.parentNode) box.parentNode.removeChild(box);
        box = document.createElement("div");
        box.style.cssText = "position:absolute;border:2px dashed #0EA5E9;background:rgba(14,165,233,.14);" +
            "z-index:35;pointer-events:none;border-radius:4px;box-shadow:0 2px 10px rgba(14,165,233,.25);";
        box.style.left = startX + "px"; box.style.top = startY + "px"; box.style.width = "0px"; box.style.height = "0px";
        pg.appendChild(box);
        drawing = true;
    });
    document.addEventListener("mousemove", function (e) {
        if (!drawing || !startPg || !box) return;
        var r = startPg.getBoundingClientRect();
        var cx = Math.min(Math.max(e.clientX - r.left, 0), r.width);
        var cy = Math.min(Math.max(e.clientY - r.top, 0), r.height);
        box.style.left = Math.min(cx, startX) + "px";
        box.style.top = Math.min(cy, startY) + "px";
        box.style.width = Math.abs(cx - startX) + "px";
        box.style.height = Math.abs(cy - startY) + "px";
    });
    document.addEventListener("mouseup", function () {
        if (!drawing) return;
        drawing = false;
        if (!startPg || !box) return;
        var r = startPg.getBoundingClientRect();
        var bx = box.offsetLeft, by = box.offsetTop, bw = box.offsetWidth, bh = box.offsetHeight;
        // Si el trazo fue muy chico, se usa un tamaño por defecto proporcional a la cajita.
        if (bw < 24 || bh < 12) {
            bw = r.width * 0.26; bh = bw * RATIO;
            bx = Math.min(bx, r.width - bw); by = Math.min(by, r.height - bh);
            box.style.left = bx + "px"; box.style.top = by + "px"; box.style.width = bw + "px"; box.style.height = bh + "px";
        }
        var pg = parseInt(startPg.getAttribute("data-pg"), 10) || 1;
        var xPct = bx / r.width * 100, yPct = by / r.height * 100;
        var wPct = bw / r.width * 100, hPct = bh / r.height * 100;
        placed = true;
        box.innerHTML = '<div style="position:absolute;left:0;top:-19px;background:#0EA5E9;color:#fff;' +
            'font:700 10px/1 \'Segoe UI\';padding:3px 7px;border-radius:4px;white-space:nowrap;">Aqu&iacute; ir&aacute; su firma</div>';
        btnClear.style.display = "inline-block";
        msg.textContent = "Firma ubicada en la página " + pg + ". Puede rehacerla o continuar con la firma.";
        setActive(false);
        try {
            if (window.parent && window.parent !== window && typeof window.parent.firUbicacionFirma === "function") {
                window.parent.firUbicacionFirma(pg, xPct, yPct, wPct, hPct);
            }
        } catch (err) { }
        avisoUbicacionGuardada(pg);
    });

    // Toast DENTRO del visor (el del padre queda tapado por el overlay del iframe):
    // confirma que la ubicación de la firma quedó guardada.
    function avisoUbicacionGuardada(pg) {
        try {
            var d = document.createElement("div");
            d.innerHTML = '<i class="fa fa-check-circle" style="margin-right:8px;"></i>' +
                'Ubicaci&oacute;n de la firma guardada (p&aacute;g. ' + pg + '). Ya puede continuar con la firma.';
            d.style.cssText = "position:fixed;top:18px;right:18px;z-index:2147483647;background:#16A34A;color:#fff;" +
                "padding:13px 18px;border-radius:11px;box-shadow:0 12px 34px rgba(0,0,0,.32);" +
                "font:600 13px/1.4 -apple-system,Segoe UI,Roboto,sans-serif;max-width:340px;opacity:0;transition:opacity .3s;";
            document.body.appendChild(d);
            requestAnimationFrame(function () { d.style.opacity = "1"; });
            setTimeout(function () {
                d.style.opacity = "0";
                setTimeout(function () { if (d.parentNode) d.parentNode.removeChild(d); }, 400);
            }, 3800);
        } catch (e) { }
    }

    // Arranca inactivo: el firmante primero lee y luego pulsa "Ubicar firma".
    setActive(false);
})();
