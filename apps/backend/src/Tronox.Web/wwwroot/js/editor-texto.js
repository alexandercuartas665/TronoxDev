// Editor de texto interno (RF08): interop de TinyMCE self-hosted para Blazor.
// Reemplaza el CKEditor 4 del legacy. Toolbar equivalente (Formato/Fuente/Tamano, estilos basicos,
// colores, listas, sangrias, alineaciones, link, imagen, tabla, pantalla completa). Hoja tamano Carta.
window.txEditor = (function () {
    var SEL = 'txEdArea';
    var _cargando = null;

    // Carga tinymce.min.js bajo demanda (solo al abrir el editor), una sola vez.
    function ensureTiny() {
        if (typeof tinymce !== 'undefined') { return Promise.resolve(); }
        if (_cargando) { return _cargando; }
        _cargando = new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = '/tinymce/tinymce.min.js';
            s.referrerPolicy = 'origin';
            s.onload = function () { resolve(); };
            s.onerror = function () { reject(new Error('No se pudo cargar TinyMCE.')); };
            document.head.appendChild(s);
        });
        return _cargando;
    }

    async function init(contenidoInicial) {
        await ensureTiny();
        if (typeof tinymce === 'undefined') { return; }
        var prev = tinymce.get(SEL);
        if (prev) { prev.remove(); }
        tinymce.init({
            selector: '#' + SEL,
            license_key: 'gpl',
            base_url: '/tinymce',
            suffix: '.min',
            height: Math.max(400, window.innerHeight - 250),
            menubar: false,
            branding: false,
            promotion: false,
            resize: false,
            plugins: 'advlist lists link image table charmap searchreplace fullscreen autolink',
            toolbar: 'undo redo | blocks fontfamily fontsize | ' +
                     'bold italic underline strikethrough removeformat | forecolor backcolor | ' +
                     'bullist numlist | outdent indent | alignleft aligncenter alignright alignjustify | ' +
                     'link image table | fullscreen',
            // Hoja Carta (816x1056px con margenes de 1"/96px), calca la contentsCss del legacy.
            content_style:
                'body{font-family:"Segoe UI",Arial,sans-serif;font-size:13px;line-height:1.7;color:#0F172A;' +
                'background:#fff;max-width:816px;min-height:1056px;margin:16px auto;padding:96px;' +
                'box-sizing:border-box;box-shadow:0 2px 10px rgba(15,23,42,.18);}',
            setup: function (ed) {
                ed.on('init', function () {
                    if (contenidoInicial) { ed.setContent(contenidoInicial); }
                });
            }
        });
    }

    function getContent() {
        var ed = tinymce.get(SEL);
        return ed ? ed.getContent() : '';
    }

    // Texto plano (sin tags ni &nbsp;) para saber si hay contenido real, sin ir al servidor.
    function tieneTexto() {
        var ed = tinymce.get(SEL);
        if (!ed) { return false; }
        var t = ed.getContent({ format: 'text' }).replace(/ /g, '').trim();
        return t.length > 0;
    }

    function destroy() {
        var ed = tinymce.get(SEL);
        if (ed) { ed.remove(); }
    }

    return { init: init, getContent: getContent, tieneTexto: tieneTexto, destroy: destroy };
})();
