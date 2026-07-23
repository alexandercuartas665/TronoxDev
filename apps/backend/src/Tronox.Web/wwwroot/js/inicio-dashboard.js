/* ==================================================================
   TRONOX SGDEA - Panel de Control (/inicio)
   Portado del prototipo (Prototipo/index.html): KPIs, graficas Chart.js,
   galeria de indicadores y datos DUMMY de demostracion.

   Blazor Server: la pagina /inicio es SSR estatica; este script construye
   el tablero en el cliente. Se re-inicializa en cada navegacion mejorada
   (Blazor 'enhancedload') porque el DOM del #dashGrid llega vacio del server.

   Chart.js NO se carga globalmente: se inyecta bajo demanda desde el paquete
   vendorizado (wwwroot/vendor/chartjs, MIT) solo cuando se abre este panel.
   Las graficas leen los tokens --kt-* del tema activo, como el prototipo.
   ================================================================== */
(function () {
  "use strict";

  var CHART_SRC = "vendor/chartjs/chart.umd.min.js";
  var STORE_KEY = "tronox_dash_widgets_v1";

  // ---- Carga perezosa de Chart.js (una sola vez) --------------------
  function ensureChart(cb) {
    if (typeof window.Chart !== "undefined") { cb(); return; }
    var s = document.getElementById("tx-chartjs");
    if (s) {
      if (s.getAttribute("data-loaded")) { cb(); }
      else { s.addEventListener("load", cb); }
      return;
    }
    s = document.createElement("script");
    s.id = "tx-chartjs";
    s.src = CHART_SRC;
    s.onload = function () { s.setAttribute("data-loaded", "1"); cb(); };
    document.head.appendChild(s);
  }

  var charts = [];
  function destroyCharts() {
    charts.forEach(function (c) { try { c.destroy(); } catch (e) {} });
    charts = [];
  }

  // ---- Construccion del tablero para un contenedor .tx-inicio -------
  function build(root) {
    var grid = root.querySelector("#dashGrid");
    if (!grid) { return; }

    function cv(n, f) {
      var v = getComputedStyle(document.documentElement).getPropertyValue(n).trim();
      return v || f;
    }
    var C = {
      primary: cv("--kt-primary", "#009ef7"), success: cv("--kt-success", "#50cd89"),
      warning: cv("--kt-warning", "#ffc700"), danger: cv("--kt-danger", "#f1416c"),
      info: cv("--kt-info", "#7239ea"), gray: cv("--kt-text-gray-500", "#a1a5b7")
    };

    function gradient(ctx, color) {
      var g = ctx.createLinearGradient(0, 0, 0, 260);
      g.addColorStop(0, color + "40"); g.addColorStop(1, color + "00");
      return g;
    }
    function kpiHtml(o) {
      return '<div class="stat-card h-100">' +
        '<button class="w-remove" data-remove="' + o.id + '" title="Quitar"><i class="bi bi-x-lg"></i></button>' +
        '<div class="d-flex justify-content-between align-items-start">' +
          '<div><div class="stat-val">' + o.val + '</div><div class="stat-label">' + o.label + '</div></div>' +
          '<div class="stat-icon" style="background:' + o.bg + ';color:' + o.c + '"><i class="bi ' + o.icon + '"></i></div>' +
        '</div>' +
        '<div class="stat-trend" style="color:' + o.tc + '"><i class="bi ' + o.ti + '"></i><span>' + o.tt + '</span></div>' +
      '</div>';
    }
    function cardHtml(id, title, sub, body, headRight) {
      return '<div class="card h-100">' +
        '<button class="w-remove" data-remove="' + id + '" title="Quitar"><i class="bi bi-x-lg"></i></button>' +
        '<div class="card-header"><div><div class="card-title">' + title + '</div>' +
          (sub ? '<div class="card-subtitle">' + sub + '</div>' : '') + '</div>' +
          (headRight || '') + '</div>' +
        body +
      '</div>';
    }

    // ---------- datos DUMMY ----------
    var PQRSD = [
      { name: 'En termino', val: 412, color: C.success },
      { name: 'Por vencer', val: 37, color: C.warning },
      { name: 'Vencidas', val: 11, color: C.danger },
      { name: 'Cerradas (mes)', val: 286, color: C.primary }
    ];
    var TIPO = [
      { name: 'Oficios', val: 1240, color: C.primary },
      { name: 'Memorandos', val: 860, color: C.info },
      { name: 'Resoluciones', val: 420, color: C.success },
      { name: 'Certificaciones', val: 310, color: C.warning },
      { name: 'Otros', val: 280, color: C.gray }
    ];
    var MODULOS = [
      { name: 'Correspondencia', val: 2418, color: C.primary },
      { name: 'Expedientes', val: 1640, color: C.success },
      { name: 'Documentos', val: 3120, color: C.info },
      { name: 'PQRSD', val: 746, color: C.warning },
      { name: 'Contratacion', val: 312, color: C.danger }
    ];
    var DEPENDENCIAS = [
      { name: 'Secretaria General', val: 1820, color: C.primary },
      { name: 'Hacienda', val: 1340, color: C.success },
      { name: 'Planeacion', val: 980, color: C.info },
      { name: 'Salud', val: 760, color: C.warning },
      { name: 'Gobierno', val: 540, color: C.danger }
    ];
    var TRAMITES = [
      { rad: '2026-EE-04821', asunto: 'Respuesta derecho de peticion', resp: 'L. Marquez', estado: 'En tramite', cls: 'primary', sla: 'A tiempo', slaCls: 'success', ic: 'bi-megaphone', icBg: 'var(--kt-warning-light)', icC: 'var(--kt-warning)' },
      { rad: '2026-ER-11240', asunto: 'Solicitud de certificado laboral', resp: 'C. Rojas', estado: 'Asignado', cls: 'info', sla: '6h', slaCls: 'warning', ic: 'bi-person-workspace', icBg: 'var(--kt-info-light)', icC: 'var(--kt-info)' },
      { rad: '2026-EE-04816', asunto: 'Traslado por competencia', resp: 'M. Gomez', estado: 'En tramite', cls: 'primary', sla: 'A tiempo', slaCls: 'success', ic: 'bi-arrow-left-right', icBg: 'var(--kt-primary-light)', icC: 'var(--kt-primary)' },
      { rad: '2026-ER-11238', asunto: 'Reclamo facturacion predial', resp: 'A. Castro', estado: 'Por vencer', cls: 'warning', sla: '3h', slaCls: 'danger', ic: 'bi-receipt', icBg: 'var(--kt-danger-light)', icC: 'var(--kt-danger)' },
      { rad: '2026-EI-00932', asunto: 'Memorando plan de compras', resp: 'J. Pineda', estado: 'Cerrado', cls: 'success', sla: 'Cumplido', slaCls: 'secondary', ic: 'bi-file-earmark-check', icBg: 'var(--kt-success-light)', icC: 'var(--kt-success)' }
    ];
    var ACTIVITY = [
      { ic: 'bi-box-arrow-in-down', bg: 'var(--kt-primary-light)', c: 'var(--kt-primary)', title: 'Nuevo radicado de entrada 2026-ER-11240', meta: 'Ventanilla Unica - hace 4 min' },
      { ic: 'bi-pen', bg: 'var(--kt-info-light)', c: 'var(--kt-info)', title: 'C. Rojas firmo el documento DOC-8841', meta: 'Firma Electronica - hace 12 min' },
      { ic: 'bi-folder-plus', bg: 'var(--kt-success-light)', c: 'var(--kt-success)', title: 'Expediente 4.2-EXP-0931 creado', meta: 'Gestion de Expedientes - hace 28 min' },
      { ic: 'bi-megaphone', bg: 'var(--kt-warning-light)', c: 'var(--kt-warning)', title: 'PQRSD 2026-PQ-0772 respondida al ciudadano', meta: 'PQRSD - hace 41 min' },
      { ic: 'bi-arrow-left-right', bg: 'var(--kt-primary-light)', c: 'var(--kt-primary)', title: 'Transferencia documental aprobada (TRF-118)', meta: 'Expedientes - hace 1 h' }
    ];

    function donutLegend(arr) {
      var total = arr.reduce(function (a, p) { return a + p.val; }, 0);
      return '<ul class="legend-list mt-3">' + arr.map(function (p) {
        var pct = Math.round(p.val / total * 100);
        return '<li><span class="lg-dot" style="background:' + p.color + '"></span>' +
          '<span class="lg-name">' + p.name + '</span>' +
          '<span class="lg-val">' + p.val.toLocaleString('es-CO') + '</span>' +
          '<span class="lg-pct">' + pct + '%</span></li>';
      }).join('') + '</ul>';
    }
    function barsHtml(arr) {
      var max = Math.max.apply(null, arr.map(function (m) { return m.val; }));
      return arr.map(function (m) {
        var w = Math.round(m.val / max * 100);
        return '<div class="bar-row"><div class="bar-head"><span class="bn">' + m.name + '</span>' +
          '<span class="bv">' + m.val.toLocaleString('es-CO') + '</span></div>' +
          '<div class="bar-track"><div class="bar-fill" style="width:' + w + '%;background:' + m.color + '"></div></div></div>';
      }).join('');
    }
    function mountDonut(el, arr, cvId) {
      var c = el.querySelector('#' + cvId);
      if (!c || typeof Chart === 'undefined') { return; }
      charts.push(new Chart(c.getContext('2d'), {
        type: 'doughnut',
        data: { labels: arr.map(function (p) { return p.name; }), datasets: [{ data: arr.map(function (p) { return p.val; }), backgroundColor: arr.map(function (p) { return p.color; }), borderWidth: 0, hoverOffset: 6 }] },
        options: { responsive: true, maintainAspectRatio: false, cutout: '68%', plugins: { legend: { display: false }, tooltip: { backgroundColor: '#181c32', padding: 10, cornerRadius: 8 } } }
      }));
    }

    // ---------- registro de widgets ----------
    var WIDGETS = [
      { id: 'kpi_docs', cat: 'Indicadores clave (KPI)', name: 'Documentos radicados hoy', desc: 'Total de radicados del dia con variacion.', icon: 'bi-inboxes', col: 'col-xl-3 col-md-6',
        html: function () { return kpiHtml({ id: 'kpi_docs', val: '248', label: 'Documentos radicados hoy', bg: 'var(--kt-primary-light)', c: 'var(--kt-primary)', icon: 'bi-inboxes', tc: 'var(--kt-success)', ti: 'bi-arrow-up-short', tt: '12,4% vs. ayer' }); } },
      { id: 'kpi_exp', cat: 'Indicadores clave (KPI)', name: 'Expedientes activos', desc: 'Expedientes en gestion actualmente.', icon: 'bi-folder2-open', col: 'col-xl-3 col-md-6',
        html: function () { return kpiHtml({ id: 'kpi_exp', val: '1.842', label: 'Expedientes activos', bg: 'var(--kt-success-light)', c: 'var(--kt-success)', icon: 'bi-folder2-open', tc: 'var(--kt-success)', ti: 'bi-arrow-up-short', tt: '38 nuevos esta semana' }); } },
      { id: 'kpi_pqrsd', cat: 'Indicadores clave (KPI)', name: 'PQRSD por vencer', desc: 'Solicitudes ciudadanas proximas a vencer.', icon: 'bi-calendar-x', col: 'col-xl-3 col-md-6',
        html: function () { return kpiHtml({ id: 'kpi_pqrsd', val: '37', label: 'PQRSD por vencer (72h)', bg: 'var(--kt-danger-light)', c: 'var(--kt-danger)', icon: 'bi-calendar-x', tc: 'var(--kt-danger)', ti: 'bi-exclamation-circle', tt: '9 vencen hoy' }); } },
      { id: 'kpi_firmas', cat: 'Indicadores clave (KPI)', name: 'Firmas pendientes', desc: 'Documentos en espera de firma.', icon: 'bi-pen', col: 'col-xl-3 col-md-6',
        html: function () { return kpiHtml({ id: 'kpi_firmas', val: '56', label: 'Firmas pendientes', bg: 'var(--kt-info-light)', c: 'var(--kt-info)', icon: 'bi-pen', tc: 'var(--kt-text-gray-500)', ti: 'bi-clock-history', tt: 'SLA promedio 4,2 h' }); } },
      { id: 'kpi_tareas', cat: 'Indicadores clave (KPI)', name: 'Tareas pendientes', desc: 'Tareas asignadas sin resolver.', icon: 'bi-check2-square', col: 'col-xl-3 col-md-6',
        html: function () { return kpiHtml({ id: 'kpi_tareas', val: '124', label: 'Tareas pendientes', bg: 'var(--kt-warning-light)', c: 'var(--kt-warning)', icon: 'bi-check2-square', tc: 'var(--kt-danger)', ti: 'bi-arrow-up-short', tt: '8 vencidas' }); } },
      { id: 'kpi_usuarios', cat: 'Indicadores clave (KPI)', name: 'Usuarios activos', desc: 'Funcionarios conectados hoy.', icon: 'bi-people', col: 'col-xl-3 col-md-6',
        html: function () { return kpiHtml({ id: 'kpi_usuarios', val: '186', label: 'Usuarios activos hoy', bg: 'var(--kt-primary-light)', c: 'var(--kt-primary)', icon: 'bi-people', tc: 'var(--kt-success)', ti: 'bi-arrow-up-short', tt: '92% de la planta' }); } },
      { id: 'kpi_storage', cat: 'Indicadores clave (KPI)', name: 'Almacenamiento usado', desc: 'Consumo del repositorio documental.', icon: 'bi-hdd', col: 'col-xl-3 col-md-6',
        html: function () { return kpiHtml({ id: 'kpi_storage', val: '68%', label: 'Almacenamiento (1,4 TB)', bg: 'var(--kt-info-light)', c: 'var(--kt-info)', icon: 'bi-hdd', tc: 'var(--kt-text-gray-500)', ti: 'bi-database', tt: '460 GB disponibles' }); } },
      { id: 'kpi_tiempo', cat: 'Indicadores clave (KPI)', name: 'Tiempo medio de respuesta', desc: 'Promedio de respuesta a tramites.', icon: 'bi-stopwatch', col: 'col-xl-3 col-md-6',
        html: function () { return kpiHtml({ id: 'kpi_tiempo', val: '2,8 d', label: 'Tiempo medio de respuesta', bg: 'var(--kt-success-light)', c: 'var(--kt-success)', icon: 'bi-stopwatch', tc: 'var(--kt-success)', ti: 'bi-arrow-down-short', tt: '0,4 d mejor que el mes pasado' }); } },

      { id: 'chart_radicacion', cat: 'Graficas y paneles', name: 'Radicacion documental', desc: 'Volumen por canal en los ultimos 12 meses.', icon: 'bi-graph-up', col: 'col-xl-8',
        html: function () { return cardHtml('chart_radicacion', 'Radicacion documental', 'Volumen por canal - ultimos 12 meses', '<div class="card-body"><div class="chart-wrap tall"><canvas id="cvRadic"></canvas></div></div>', '<span class="badge badge-light-primary px-3 py-2">2.418 este mes</span>'); },
        mount: function (el) {
          var c = el.querySelector('#cvRadic'); if (!c || typeof Chart === 'undefined') { return; }
          var ctx = c.getContext('2d');
          charts.push(new Chart(ctx, {
            type: 'line',
            data: { labels: ['Jul','Ago','Sep','Oct','Nov','Dic','Ene','Feb','Mar','Abr','May','Jun'],
              datasets: [
                { label: 'Entrada', data: [1820,1910,2040,2210,1980,1640,2100,2260,2380,2290,2440,2418], borderColor: C.primary, backgroundColor: gradient(ctx, C.primary), fill: true, tension: .4, borderWidth: 2.5, pointRadius: 0, pointHoverRadius: 5 },
                { label: 'Salida', data: [980,1020,1100,1180,1050,920,1160,1240,1300,1280,1360,1390], borderColor: C.success, fill: false, tension: .4, borderWidth: 2.5, pointRadius: 0, pointHoverRadius: 5 },
                { label: 'Interna', data: [540,560,610,640,590,500,620,680,720,700,760,780], borderColor: C.info, fill: false, tension: .4, borderWidth: 2.5, pointRadius: 0, pointHoverRadius: 5 }
              ] },
            options: { responsive: true, maintainAspectRatio: false, interaction: { mode: 'index', intersect: false },
              plugins: { legend: { position: 'top', align: 'end', labels: { boxWidth: 8, boxHeight: 8, usePointStyle: true, pointStyle: 'circle', padding: 16, font: { size: 11.5, weight: '600' }, color: C.gray } }, tooltip: { backgroundColor: '#181c32', padding: 10, cornerRadius: 8 } },
              scales: { x: { grid: { display: false }, border: { display: false } }, y: { grid: { color: cv('--kt-border-color', '#eff2f5') }, border: { display: false }, ticks: { padding: 8 }, beginAtZero: true } } }
          }));
        } },
      { id: 'chart_pqrsd', cat: 'Graficas y paneles', name: 'Estado de PQRSD', desc: 'Distribucion de solicitudes por estado.', icon: 'bi-pie-chart', col: 'col-xl-4',
        html: function () { return cardHtml('chart_pqrsd', 'Estado de PQRSD', '', '<div class="card-body"><div class="chart-wrap donut"><canvas id="cvPqrsd"></canvas></div>' + donutLegend(PQRSD) + '</div>'); },
        mount: function (el) { mountDonut(el, PQRSD, 'cvPqrsd'); } },
      { id: 'chart_tipo', cat: 'Graficas y paneles', name: 'Tipo documental', desc: 'Distribucion de documentos por tipologia.', icon: 'bi-files', col: 'col-xl-4',
        html: function () { return cardHtml('chart_tipo', 'Tipo documental', 'Produccion del mes', '<div class="card-body"><div class="chart-wrap donut"><canvas id="cvTipo"></canvas></div>' + donutLegend(TIPO) + '</div>'); },
        mount: function (el) { mountDonut(el, TIPO, 'cvTipo'); } },
      { id: 'bars_modulo', cat: 'Graficas y paneles', name: 'Volumen por modulo', desc: 'Documentos gestionados por modulo.', icon: 'bi-bar-chart', col: 'col-xl-5',
        html: function () { return cardHtml('bars_modulo', 'Volumen por modulo', 'Documentos gestionados (mes)', '<div class="card-body">' + barsHtml(MODULOS) + '</div>'); } },
      { id: 'bars_dep', cat: 'Graficas y paneles', name: 'Carga por dependencia', desc: 'Documentos por dependencia de la entidad.', icon: 'bi-diagram-3', col: 'col-xl-5',
        html: function () { return cardHtml('bars_dep', 'Carga por dependencia', 'Documentos asignados (mes)', '<div class="card-body">' + barsHtml(DEPENDENCIAS) + '</div>'); } },
      { id: 'sla_gauge', cat: 'Graficas y paneles', name: 'Cumplimiento de SLA', desc: 'Porcentaje de tramites dentro de termino.', icon: 'bi-speedometer2', col: 'col-xl-4',
        html: function () { return '<div class="card h-100 card-accent-top success"><button class="w-remove" data-remove="sla_gauge" title="Quitar"><i class="bi bi-x-lg"></i></button><div class="card-body sla-gauge d-flex flex-column justify-content-center h-100"><div class="sla-val" style="color:var(--kt-success)">94,6%</div><div class="sla-lbl">Cumplimiento de SLA (mes)</div><div class="progress mt-3"><div class="progress-bar" style="width:94.6%;background:var(--kt-success)"></div></div></div></div>'; } },

      { id: 'tbl_tramites', cat: 'Listas y tablas', name: 'Tramites recientes', desc: 'Ultimos tramites con su estado y SLA.', icon: 'bi-list-task', col: 'col-xl-7',
        html: function () {
          var rows = TRAMITES.map(function (t) {
            return '<tr><td><span style="font-family:monospace;font-size:12px;color:var(--kt-text-gray-800);font-weight:600">' + t.rad + '</span></td>' +
              '<td><div class="d-flex align-items-center gap-2"><span class="mini-tbl-icon" style="background:' + t.icBg + ';color:' + t.icC + '"><i class="bi ' + t.ic + '"></i></span>' +
              '<span style="color:var(--kt-text-gray-800);font-weight:500">' + t.asunto + '</span></div></td>' +
              '<td>' + t.resp + '</td><td><span class="badge badge-light-' + t.cls + ' px-2 py-1">' + t.estado + '</span></td>' +
              '<td class="text-end"><span class="badge badge-light-' + t.slaCls + ' px-2 py-1">' + t.sla + '</span></td></tr>';
          }).join('');
          var body = '<div class="card-body p-0"><div class="table-responsive"><table class="table align-middle mb-0"><thead><tr><th>Radicado</th><th>Asunto</th><th>Responsable</th><th>Estado</th><th class="text-end">SLA</th></tr></thead><tbody>' + rows + '</tbody></table></div></div>';
          return cardHtml('tbl_tramites', 'Tramites recientes', '', body, '<span class="btn btn-light-primary btn-sm"><i class="bi bi-list-task me-1"></i>Ver bandeja</span>');
        } },
      { id: 'feed_actividad', cat: 'Listas y tablas', name: 'Actividad reciente', desc: 'Eventos recientes en la plataforma.', icon: 'bi-activity', col: 'col-xl-8',
        html: function () {
          var feed = ACTIVITY.map(function (a) {
            return '<div class="timeline-item"><div class="timeline-icon" style="background:' + a.bg + ';color:' + a.c + '"><i class="bi ' + a.ic + '"></i></div>' +
              '<div class="tl-text"><div class="tl-title">' + a.title + '</div><div class="tl-meta">' + a.meta + '</div></div></div>';
          }).join('');
          return cardHtml('feed_actividad', 'Actividad reciente', 'Tiempo real', '<div class="card-body py-2">' + feed + '</div>');
        } },
      { id: 'panel_alertas', cat: 'Listas y tablas', name: 'Alertas y vencimientos', desc: 'Avisos operativos que requieren atencion.', icon: 'bi-bell', col: 'col-xl-4',
        html: function () {
          var body = '<div class="card-body d-flex flex-column gap-3">' +
            '<div class="alert-inline ai-danger"><i class="bi bi-exclamation-octagon-fill"></i><div><strong>9 PQRSD vencen hoy.</strong> Requieren respuesta antes de las 17:00.</div></div>' +
            '<div class="alert-inline ai-warning"><i class="bi bi-clock-fill"></i><div><strong>14 transferencias</strong> pendientes de aprobacion.</div></div>' +
            '<div class="alert-inline ai-primary"><i class="bi bi-pen-fill"></i><div><strong>56 documentos</strong> esperan firma electronica.</div></div>' +
            '<div class="alert-inline ai-success"><i class="bi bi-check-circle-fill"></i><div><strong>Backup diario completado</strong> a las 02:00.</div></div>' +
          '</div>';
          return cardHtml('panel_alertas', 'Alertas y vencimientos', '', body);
        } }
    ];

    var WMAP = {}; WIDGETS.forEach(function (w) { WMAP[w.id] = w; });
    var DEFAULTS = ['kpi_docs', 'kpi_exp', 'kpi_pqrsd', 'kpi_firmas', 'chart_radicacion', 'chart_pqrsd', 'tbl_tramites', 'bars_modulo', 'feed_actividad', 'panel_alertas'];

    function load() {
      try { var s = JSON.parse(localStorage.getItem(STORE_KEY)); if (Array.isArray(s)) { return s.filter(function (id) { return WMAP[id]; }); } } catch (e) {}
      return DEFAULTS.slice();
    }
    function save() { try { localStorage.setItem(STORE_KEY, JSON.stringify(selected)); } catch (e) {} }
    var selected = load();

    var $ = function (sel) { return root.querySelector(sel); };

    function renderDashboard() {
      destroyCharts();
      var gridEl = $('#dashGrid');
      var empty = $('#dashEmpty');
      var ordered = WIDGETS.filter(function (w) { return selected.indexOf(w.id) !== -1; });
      if (!ordered.length) { gridEl.innerHTML = ''; empty.style.display = 'block'; $('#dashCount').textContent = 'No hay indicadores seleccionados.'; return; }
      empty.style.display = 'none';
      gridEl.innerHTML = ordered.map(function (w) { return '<div class="dash-col ' + w.col + '">' + w.html() + '</div>'; }).join('');
      ordered.forEach(function (w) { if (w.mount) { w.mount(gridEl); } });
      $('#dashCount').textContent = ordered.length + ' indicador' + (ordered.length === 1 ? '' : 'es') + ' en tu panel.';
      gridEl.querySelectorAll('[data-remove]').forEach(function (b) {
        b.addEventListener('click', function () { toggle(b.getAttribute('data-remove')); });
      });
    }

    function renderGallery() {
      var gEl = $('#galleryGrid');
      if (!gEl) { return; }
      var cats = [];
      WIDGETS.forEach(function (w) { if (cats.indexOf(w.cat) === -1) { cats.push(w.cat); } });
      var html = '';
      cats.forEach(function (cat) {
        html += '<div class="gallery-cat">' + cat + '</div>';
        WIDGETS.filter(function (w) { return w.cat === cat; }).forEach(function (w) {
          var on = selected.indexOf(w.id) !== -1;
          var sizeTag = w.col.indexOf('col-xl-3') !== -1 ? 'KPI' : 'Panel';
          html += '<div class="g-tile' + (on ? ' on' : '') + '" data-id="' + w.id + '">' +
            '<div class="g-check"><i class="bi bi-check-lg"></i></div>' +
            '<div class="g-icon" style="color:' + (on ? 'var(--kt-primary)' : 'var(--kt-text-gray-600)') + '"><i class="bi ' + w.icon + '"></i></div>' +
            '<div class="g-name">' + w.name + '</div>' +
            '<div class="g-desc">' + w.desc + '</div>' +
            '<div class="g-size-tag">' + sizeTag + '</div>' +
          '</div>';
        });
      });
      gEl.innerHTML = html;
      gEl.querySelectorAll('.g-tile').forEach(function (t) {
        t.addEventListener('click', function () { toggle(t.getAttribute('data-id')); });
      });
      $('#gallerySel').textContent = selected.length + ' de ' + WIDGETS.length + ' indicadores activos';
    }

    function toggle(id) {
      var i = selected.indexOf(id);
      if (i === -1) { selected.push(id); } else { selected.splice(i, 1); }
      save(); renderDashboard(); renderGallery();
    }

    // ---------- saludo + arranque ----------
    var greet = $('#dash-greeting');
    var userName = root.getAttribute('data-user') || 'funcionario';
    var tenantName = root.getAttribute('data-tenant') || 'su entidad';
    if (greet) {
      var now = new Date();
      var fmt = now.toLocaleDateString('es-CO', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
      fmt = fmt.charAt(0).toUpperCase() + fmt.slice(1);
      greet.textContent = 'Bienvenido, ' + userName + '. ' + fmt + ' - Resumen operativo de ' + tenantName + '.';
    }

    if (typeof Chart !== 'undefined') { Chart.defaults.font.family = 'Inter, sans-serif'; Chart.defaults.font.size = 11; Chart.defaults.color = C.gray; }

    var btnReset = $('#btnReset');
    if (btnReset) {
      btnReset.addEventListener('click', function () {
        selected = DEFAULTS.slice(); save(); renderDashboard(); renderGallery();
      });
    }

    renderDashboard();
    renderGallery();
  }

  // ---- Inicializacion (idempotente por contenedor) ------------------
  function initDashboard() {
    var root = document.querySelector('.tx-inicio');
    if (!root) { return; }
    if (root.getAttribute('data-dash-init') === '1') { return; }
    root.setAttribute('data-dash-init', '1');
    ensureChart(function () { build(root); });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initDashboard);
  } else {
    initDashboard();
  }

  // Navegacion mejorada de Blazor: el nuevo #dashGrid llega vacio del server.
  (function registerEnhanced() {
    if (window.Blazor && window.Blazor.addEventListener) {
      window.Blazor.addEventListener('enhancedload', initDashboard);
    } else {
      setTimeout(registerEnhanced, 200);
    }
  })();
})();
