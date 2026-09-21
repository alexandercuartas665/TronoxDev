// ============================================================================
// Pad de firma manuscrita (grafo) - RQ05 RF03 §3.3.3.
// Canvas + pointer events puros (sin libreria externa ni CDN), calca la captura
// de ctrlMiFirma.ascx del legacy (que usaba signature_pad). Expone window.firmaGrafo.
//
// Resolucion interna FIJA (1200x400): no depende de offsetWidth, que en el primer
// render de Blazor todavia puede ser 0 (el layout no ha calculado el ancho) y dejaba
// el canvas de 4px. El trazo se mapea de coordenadas de pantalla a las del canvas
// con getBoundingClientRect, asi el CSS puede escalar el ancho sin desfasar el dibujo.
// ============================================================================
(function () {
    "use strict";

    var CW = 1200, CH = 400;   // resolucion interna fija
    var pads = {};             // canvasId -> { canvas, ctx, drawing, dirty, last }

    function setup(canvas) {
        canvas.width = CW;
        canvas.height = CH;
        var ctx = canvas.getContext("2d");
        ctx.lineWidth = 4.5;
        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        ctx.strokeStyle = "#0F2340";
        return { canvas: canvas, ctx: ctx, drawing: false, dirty: false, last: null };
    }

    // Coordenadas del evento -> coordenadas del canvas (mapea el escalado CSS).
    function pos(pad, ev) {
        var r = pad.canvas.getBoundingClientRect();
        var sx = r.width > 0 ? pad.canvas.width / r.width : 1;
        var sy = r.height > 0 ? pad.canvas.height / r.height : 1;
        return { x: (ev.clientX - r.left) * sx, y: (ev.clientY - r.top) * sy };
    }

    function start(pad, ev) {
        ev.preventDefault();
        pad.drawing = true;
        pad.last = pos(pad, ev);
    }

    function move(pad, ev) {
        if (!pad.drawing) { return; }
        ev.preventDefault();
        var p = pos(pad, ev);
        pad.ctx.beginPath();
        pad.ctx.moveTo(pad.last.x, pad.last.y);
        pad.ctx.lineTo(p.x, p.y);
        pad.ctx.stroke();
        pad.last = p;
        pad.dirty = true;
    }

    function end(pad, ev) {
        if (ev) { ev.preventDefault(); }
        pad.drawing = false;
        pad.last = null;
    }

    window.firmaGrafo = {
        init: function (canvasId) {
            var canvas = document.getElementById(canvasId);
            if (!canvas) { return; }
            var pad = setup(canvas);
            pads[canvasId] = pad;
            canvas.addEventListener("pointerdown", function (e) { start(pad, e); });
            canvas.addEventListener("pointermove", function (e) { move(pad, e); });
            canvas.addEventListener("pointerup", function (e) { end(pad, e); });
            canvas.addEventListener("pointerleave", function (e) { end(pad, e); });
        },
        clear: function (canvasId) {
            var pad = pads[canvasId];
            if (!pad) { return; }
            pad.ctx.clearRect(0, 0, pad.canvas.width, pad.canvas.height);
            pad.dirty = false;
        },
        isEmpty: function (canvasId) {
            var pad = pads[canvasId];
            return !pad || !pad.dirty;
        },
        // Devuelve PNG data URI con fondo transparente (el grafo se pinta en la cajita).
        toDataURL: function (canvasId) {
            var pad = pads[canvasId];
            if (!pad || !pad.dirty) { return ""; }
            return pad.canvas.toDataURL("image/png");
        }
    };
})();
