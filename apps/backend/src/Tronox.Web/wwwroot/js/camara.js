// ============================================================================
// Captura por camara (Digitalizar, RQ04 - RF18). getUserMedia -> preview en un
// <video>; capture() dibuja el frame en un canvas y devuelve el JPEG (dataURL).
// Prioriza la camara trasera del movil (facingMode environment). Sin dependencias.
// ============================================================================
(function () {
    "use strict";
    var stream = null;

    async function start(videoId) {
        var v = document.getElementById(videoId);
        if (!v) { return "no-video"; }
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) { return "no-support"; }
        try {
            stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: "environment" }, width: { ideal: 1600 }, height: { ideal: 1200 } },
                audio: false
            });
            v.srcObject = stream;
            await v.play();
            return "ok";
        } catch (e) {
            return "denied:" + (e && e.name ? e.name : "error");
        }
    }

    function capture(videoId) {
        var v = document.getElementById(videoId);
        if (!v || !v.videoWidth) { return ""; }
        var c = document.createElement("canvas");
        c.width = v.videoWidth;
        c.height = v.videoHeight;
        c.getContext("2d").drawImage(v, 0, 0, c.width, c.height);
        return c.toDataURL("image/jpeg", 0.9);
    }

    function stop() {
        if (stream) {
            stream.getTracks().forEach(function (t) { t.stop(); });
            stream = null;
        }
    }

    window.tronoxCamara = { start: start, capture: capture, stop: stop };
})();
