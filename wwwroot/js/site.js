/* Марти Хигиена — глобални скрипти (сидебар, тема, табели, датуми, известувања) */
(function () {
    'use strict';

    var doc = document;
    var root = doc.documentElement;

    /* ---------- Тема (светла / темна) ---------- */
    doc.addEventListener('click', function (e) {
        if (!e.target.closest('[data-theme-toggle]')) return;
        var next = root.dataset.theme === 'dark' ? 'light' : 'dark';
        root.dataset.theme = next;
        try { localStorage.setItem('mh-theme', next); } catch (err) { /* приватен режим */ }
    });

    /* ---------- Сидебар на мобилен ---------- */
    doc.addEventListener('click', function (e) {
        if (e.target.closest('[data-sidebar-open]')) doc.body.classList.add('sidebar-open');
        else if (e.target.closest('[data-sidebar-close]')) doc.body.classList.remove('sidebar-open');
    });
    doc.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') doc.body.classList.remove('sidebar-open');
    });

    /* ---------- Датум во горната лента ---------- */
    var dateEl = doc.getElementById('topbarDate');
    if (dateEl) {
        try {
            dateEl.textContent = new Intl.DateTimeFormat('mk-MK', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(new Date());
        } catch (err) { /* стар прелистувач */ }
    }

    /* ---------- Известувања (TempData) ---------- */
    doc.querySelectorAll('.app-toast').forEach(function (t, i) {
        setTimeout(function () { t.classList.add('is-in'); }, 60 + i * 120);
        setTimeout(function () { t.classList.remove('is-in'); setTimeout(function () { t.remove(); }, 400); }, 5200 + i * 300);
        t.addEventListener('click', function () { t.remove(); });
    });

    /* ---------- Лента за вчитување при навигација ---------- */
    var progress = doc.getElementById('pageProgress');
    function startProgress() { if (progress) progress.classList.add('is-loading'); }
    window.addEventListener('pageshow', function () { if (progress) progress.classList.remove('is-loading'); });
    doc.addEventListener('submit', function (e) {
        var f = e.target;
        if (!e.defaultPrevented && !f.target && f.getAttribute('method') !== 'dialog') startProgress();
    });
    doc.addEventListener('click', function (e) {
        var a = e.target.closest('a[href]');
        if (!a || e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey) return;
        var href = a.getAttribute('href');
        if (!href || href.charAt(0) === '#' || href.indexOf('javascript:') === 0 || a.target || a.hasAttribute('download') || a.hasAttribute('data-bs-toggle')) return;
        if (a.host && a.host !== location.host) return;
        startProgress();
    });

    /* ---------- JSON POST со anti-forgery токен (за AI страните) ---------- */
    window.mhPostJson = function (url, data) {
        var tokenInput = doc.querySelector('input[name="__RequestVerificationToken"]');
        return fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': tokenInput ? tokenInput.value : ''
            },
            body: JSON.stringify(data || {})
        }).then(function (res) {
            return res.json().catch(function () { return {}; }).then(function (body) {
                if (!res.ok) throw new Error((body && body.error) || ('Грешка ' + res.status));
                return body;
            });
        });
    };

    window.mhEscape = function (text) {
        return String(text == null ? '' : text).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    };

    /* Компатибилност со постоечкиот код */
    window.showLoaderAndRedirect = function (url) { startProgress(); window.location.href = url; };

    /* ---------- Flatpickr: македонски формат дд.мм.гггг ---------- */
    var mk = {
        weekdays: {
            shorthand: ['Нед', 'Пон', 'Вто', 'Сре', 'Чет', 'Пет', 'Саб'],
            longhand: ['Недела', 'Понеделник', 'Вторник', 'Среда', 'Четврток', 'Петок', 'Сабота']
        },
        months: {
            shorthand: ['Јан', 'Фев', 'Мар', 'Апр', 'Мај', 'Јун', 'Јул', 'Авг', 'Сеп', 'Окт', 'Ное', 'Дек'],
            longhand: ['Јануари', 'Февруари', 'Март', 'Април', 'Мај', 'Јуни', 'Јули', 'Август', 'Септември', 'Октомври', 'Ноември', 'Декември']
        },
        firstDayOfWeek: 1,
        rangeSeparator: ' — '
    };

    function initDatePickers(scope) {
        if (typeof window.flatpickr !== 'function') return;
        (scope || doc).querySelectorAll('input.flatpickr').forEach(function (input) {
            if (input._flatpickr) return;
            var v = (input.value || '').trim();
            // Само ако полето е празно или веќе е во формат дд.мм.гггг — никогаш не ја менуваме вредноста
            if (v && !/^\d{1,2}\.\d{1,2}\.\d{4}$/.test(v)) return;
            window.flatpickr(input, { dateFormat: 'd.m.Y', allowInput: true, locale: mk, disableMobile: true });
        });
    }

    /* ---------- DataTables: заеднички изглед за #html5-extension ---------- */
    var icon = {
        prev: '<svg viewBox="0 0 24 24"><polyline points="15 18 9 12 15 6"/></svg>',
        next: '<svg viewBox="0 0 24 24"><polyline points="9 18 15 12 9 6"/></svg>',
        search: '<svg viewBox="0 0 24 24"><circle cx="11" cy="11" r="7"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>'
    };

    window.mhDataTableDefaults = {
        dom: "<'dt--top-section'<'dt-top-left'B><'dt-top-right'f>>" +
             "<'table-responsive'tr>" +
             "<'dt--bottom-section'<'dt--pages-count'i><'dt--pagination'p>>",
        buttons: [
            { extend: 'copy', text: 'Копирај', className: 'btn' },
            { extend: 'csv', text: 'CSV', className: 'btn' },
            { extend: 'excel', text: 'Excel', className: 'btn' },
            { extend: 'print', text: 'Печати', className: 'btn' }
        ],
        oLanguage: {
            oPaginate: { sPrevious: icon.prev, sNext: icon.next },
            sInfo: 'Страна _PAGE_ од _PAGES_',
            sInfoEmpty: 'Нема записи',
            sInfoFiltered: '(од вкупно _MAX_)',
            sZeroRecords: 'Нема пронајдено резултати',
            sEmptyTable: 'Нема податоци',
            sSearch: icon.search,
            sSearchPlaceholder: 'Пребарувај...',
            sLengthMenu: 'Прикажи _MENU_'
        },
        stripeClasses: [],
        lengthMenu: [7, 10, 20, 50],
        pageLength: 10,
        autoWidth: false,
        columnDefs: [{ targets: 'no-sort', orderable: false }]
    };

    function initTables() {
        if (!window.jQuery || !jQuery.fn || !jQuery.fn.dataTable) return;
        var $t = jQuery('#html5-extension');
        if (!$t.length || jQuery.fn.dataTable.isDataTable($t[0])) return;
        $t.DataTable(jQuery.extend(true, {}, window.mhDataTableDefaults));
    }

    /* ---------- Паѓачки листи со пребарување ----------
       Секој <select data-search="текст за пребарување"> станува searchable (Tom Select).
       Оригиналниот select останува во формата, па name/value/change работат исто. */
    function initSearchSelects(scope) {
        if (!window.TomSelect) return;
        (scope || doc).querySelectorAll('select[data-search]').forEach(function (el) {
            if (el.tomselect) return;
            new TomSelect(el, {
                placeholder: el.getAttribute('data-search') || 'Пребарувај...',
                allowEmptyOption: true,
                create: false,
                maxOptions: 2000,
                searchField: ['text'],
                // Редоследот од серверот се задржува (без сортирање)
                sortField: [{ field: '$order' }],
                render: {
                    no_results: function () { return '<div class="no-results">Нема резултати</div>'; }
                }
            });
        });
    }
    window.mhInitSearchSelects = initSearchSelects;

    function ready() {
        initDatePickers();
        initSearchSelects();
        // По скриптите на страната — ако страната сама ја иницијализира табелата, не ја допираме.
        setTimeout(initTables, 0);
    }

    // Модалите секогаш директно под <body>: инаку позадината (backdrop) ги покрива
    // и полињата не можат да се менуваат. Модал внатре во <form> не се мести.
    doc.addEventListener('show.bs.modal', function (e) {
        var m = e.target;
        if (m && m.parentNode !== doc.body && !m.closest('form')) doc.body.appendChild(m);
    }, true);

    if (doc.readyState === 'loading') doc.addEventListener('DOMContentLoaded', ready);
    else ready();
})();
