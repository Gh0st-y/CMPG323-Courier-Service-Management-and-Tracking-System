(function () {
    "use strict";

    // Any setup problem is shown as a toast, not just in the console, so a broken page is never silent.
    function fail(message, err) {
        if (err) { console.error("reports.js:", message, err); } else { console.error("reports.js:", message); }
        if (window.CourierApp && CourierApp.toast) {
            CourierApp.toast.show(message, "error", 15000);
        } else {
            window.alert(message);
        }
    }

    try {
        init();
    } catch (err) {
        fail("The Reports page hit an error: " + (err && err.message ? err.message : err), err);
    }

    function init() {
        var root = document.getElementById("reports-root");
        if (!root) {
            fail("The Reports page is out of date: Views/Reports/Index.cshtml is the old version. Replace it with the new one.");
            return;
        }

        function $(id) { return document.getElementById(id); }

        var REQUIRED = ["fFrom", "fTo", "fStatus", "fClass", "fPay", "btnApply",
            "kTotal", "kReady", "kColl", "kAvg", "kPaid", "kUnpaid", "tOut"];
        var missing = REQUIRED.filter(function (id) { return !$(id); });
        if (missing.length) {
            fail("The Reports page is out of date: Index.cshtml is missing these ids: " + missing.join(", ") + ".");
            return;
        }

        // Optional elements: the page still works if one of these is missing.
        function setText(id, text) { var el = $(id); if (el) { el.textContent = text; } }
        function on(id, evt, fn) { var el = $(id); if (el) { el.addEventListener(evt, fn); } }

        var chartMissing = typeof Chart === "undefined";
        var charts = {};
        var MIN_SPIN_MS = 700;

        // Chart colours come from the theme tokens in site.css, so a branding change there carries through.
        function cssVar(name, fallback) {
            var v = getComputedStyle(document.documentElement).getPropertyValue(name);
            return (v && v.trim()) || fallback;
        }

        var C = {
            primary: cssVar("--color-primary", "#5c2d91"),
            primaryLight: cssVar("--color-primary-light", "#7a4bb0"),
            accent: cssVar("--color-accent", "#f2a900"),
            success: cssVar("--color-success", "#1e824c"),
            error: cssVar("--color-error", "#c0392b"),
            info: cssVar("--color-info", "#2b6cb0"),
            muted: cssVar("--color-text-muted", "#6b6572"),
            border: cssVar("--color-border", "#e0dcea"),
            neutral: "#9a95a3"
        };
        var STATUS_COLORS = { Registered: C.primaryLight, InStorage: C.info, ReadyForCollection: C.accent, Collected: C.success };
        var CLASS_COLORS = { Personal: C.primary, WorkRelated: C.accent };
        var PAY_COLORS = { Paid: C.success, Unpaid: C.error, Exempt: C.neutral };
        var FALLBACK = [C.primary, C.accent, C.success, C.info, C.error, C.primaryLight];

        if (!chartMissing) {
            try {
                Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
                Chart.defaults.color = C.muted;
                Chart.defaults.borderColor = C.border;
            } catch (err) {
                console.error("reports.js: could not set Chart.js defaults (wrong Chart.js version?)", err);
            }
        }

        function pad(n) { return n < 10 ? "0" + n : "" + n; }
        function isoDate(d) { return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate()); }

        // Same formatting as the dashboard counts (en-ZA).
        function formatCount(v) { return v === null || v === undefined ? "-" : Number(v).toLocaleString("en-ZA"); }
        function money(n) { return "R" + Number(n).toLocaleString("en-ZA", { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }

        // "ReadyForCollection" -> "Ready for collection"
        function friendly(s) {
            if (!s) { return "-"; }
            var spaced = String(s).replace(/([a-z])([A-Z])/g, "$1 $2").toLowerCase();
            return spaced.charAt(0).toUpperCase() + spaced.slice(1);
        }

        function queryString() {
            var p = new URLSearchParams();
            var map = { from: "fFrom", to: "fTo", status: "fStatus", classification: "fClass", paymentStatus: "fPay" };
            Object.keys(map).forEach(function (key) {
                var v = $(map[key]).value;
                if (v) { p.set(key, v); }
            });
            return p.toString();
        }

        // Default range: one month ago to today.
        function setDefaultFilters() {
            var today = new Date();
            var start = new Date(today.getFullYear(), today.getMonth() - 1, today.getDate());
            if (start.getDate() !== today.getDate()) {
                start = new Date(today.getFullYear(), today.getMonth(), 0); // e.g. 31 Mar -> 28/29 Feb
            }
            $("fFrom").value = isoDate(start);
            $("fTo").value = isoDate(today);
            $("fStatus").value = "";
            $("fClass").value = "";
            $("fPay").value = "";
        }

        // ---- error alert (same pattern as the dashboard) ----
        var DEFAULT_ERROR_TEXT = "Refresh the page, or contact the system administrator if this continues.";

        function showError(message) {
            var box = $("rptError");
            if (box) {
                setText("rptErrorText", message || DEFAULT_ERROR_TEXT);
                box.hidden = false;
            } else {
                fail(message || "The report could not be loaded.");
            }
        }
        function hideError() { var box = $("rptError"); if (box) { box.hidden = true; } }

        // ---- charts ----
        function colorsFor(map, rows) {
            return rows.map(function (r, i) { return map[r.label] || FALLBACK[i % FALLBACK.length]; });
        }

        var chartErrorShown = false;
        function draw(canvasId, type, rows, label, opts) {
            try {
                drawChart(canvasId, type, rows, label, opts);
            } catch (err) {
                console.error("reports.js: chart failed:", canvasId, err);
                if (!chartErrorShown) {
                    chartErrorShown = true;
                    CourierApp.toast.error("Some charts could not be drawn. The numbers and table still work.");
                }
            }
        }

        function drawChart(canvasId, type, rows, label, opts) {
            if (chartMissing || !$(canvasId)) { return; }
            opts = opts || {};
            if (charts[canvasId]) { charts[canvasId].destroy(); }

            var isLine = type === "line";
            var isDoughnut = type === "doughnut";
            var colors = isLine ? C.primary : colorsFor(opts.colors || {}, rows);

            charts[canvasId] = new Chart($(canvasId), {
                type: type,
                data: {
                    labels: rows.map(function (r) { return opts.nice ? friendly(r.label) : r.label; }),
                    datasets: [{
                        label: label,
                        data: rows.map(function (r) { return r.value; }),
                        backgroundColor: isLine ? C.primary + "1f" : colors,
                        borderColor: isLine ? C.primary : undefined,
                        borderWidth: isLine ? 2 : 0,
                        fill: isLine,
                        tension: 0.25,
                        pointRadius: isLine ? 2 : undefined,
                        borderRadius: type === "bar" ? 4 : undefined,
                        maxBarThickness: type === "bar" ? 48 : undefined
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: isDoughnut, position: "bottom" } },
                    scales: isDoughnut ? {} : {
                        x: { grid: { display: false } },
                        y: {
                            beginAtZero: true,
                            ticks: opts.money ? { callback: function (v) { return "R" + v; } } : { precision: 0 }
                        }
                    }
                }
            });
        }

        // ---- render ----
        function render(d) {
            setText("kTotal", formatCount(d.kpis.total));
            setText("kReady", formatCount(d.kpis.readyForCollection));
            setText("kColl", formatCount(d.kpis.collected));
            setText("kAvg", d.kpis.averageDaysToCollect === null ? "-" : d.kpis.averageDaysToCollect);
            setText("kPaid", money(d.kpis.feesPaid));
            setText("kUnpaid", money(d.kpis.feesUnpaid));

            draw("cDay", "line", d.perDay, "Packages", {});
            draw("cStatus", "doughnut", d.byStatus, "Packages", { nice: true, colors: STATUS_COLORS });
            draw("cClass", "bar", d.byClassification, "Packages", { nice: true, colors: CLASS_COLORS });
            draw("cPay", "bar", d.byPaymentStatus, "Rand", { colors: PAY_COLORS, money: true });

            var tbody = $("tOut");
            tbody.innerHTML = "";
            if (!d.outstanding.length) {
                var emptyRow = document.createElement("tr");
                var emptyCell = document.createElement("td");
                emptyCell.colSpan = 5;
                emptyCell.className = "results-empty";
                emptyCell.textContent = "No outstanding packages for these filters.";
                emptyRow.appendChild(emptyCell);
                tbody.appendChild(emptyRow);
                return;
            }
            d.outstanding.forEach(function (o) {
                var tr = document.createElement("tr");
                tr.className = "results-table__row";
                var cells = [
                    { text: o.f20Identifier, cls: "results-table__id" },
                    { text: o.recipientName },
                    { text: friendly(o.status), cls: "results-table__status results-table__status--" + o.status },
                    { text: o.storageLocation || "-" },
                    { text: o.ageDays }
                ];
                cells.forEach(function (c) {
                    var td = document.createElement("td");
                    td.textContent = c.text; // textContent keeps recipient data XSS-safe (SR-03)
                    if (c.cls) { td.className = c.cls; }
                    tr.appendChild(td);
                });
                tbody.appendChild(tr);
            });
        }

        // ---- loading ----
        function setLoading(isLoading) {
            var stats = $("rptStats");
            if (stats) { stats.setAttribute("aria-busy", isLoading ? "true" : "false"); }
            ["btnApply", "btnReset", "btnRefresh", "rptRetry"].forEach(function (id) {
                var el = $(id);
                if (el) { el.disabled = isLoading; }
            });
            var icon = $("rptRefreshIcon");
            if (icon) { icon.classList.toggle("spin", isLoading); }
        }

        function stampUpdated() {
            var el = $("rptUpdatedAt");
            if (!el) { return; }
            el.textContent = "Updated " + new Date().toLocaleTimeString("en-ZA", { hour: "2-digit", minute: "2-digit" });
            el.hidden = false;
        }

        // CourierApp.api.get shows the loading bar and redirects to login on a 401 (session timeout).
        function load() {
            var started = Date.now();
            setLoading(true);
            hideError();

            return CourierApp.api.get("/reports?" + queryString(), { silent: true })
                .then(function (data) { render(data); stampUpdated(); })
                .catch(function (failure) {
                    console.error("reports.js:", failure);
                    showError(failure && failure.error && failure.error.message);
                })
                .then(function () {
                    var wait = Math.max(0, MIN_SPIN_MS - (Date.now() - started));
                    return new Promise(function (resolve) { setTimeout(resolve, wait); });
                })
                .then(function () { setLoading(false); });
        }

        // ---- wire up ----
        console.info("reports.js loaded");
        setDefaultFilters();

        if (chartMissing) {
            console.error("reports.js: Chart.js did not load. Check Web/Scripts/chart.umd.js is included in the project.");
            CourierApp.toast.error("Charts could not be loaded (Chart.js is missing). The numbers and table still work. Please contact IT support.");
        }

        on("btnApply", "click", load);
        on("btnRefresh", "click", load);
        on("rptRetry", "click", load);
        on("btnReset", "click", function () { setDefaultFilters(); load(); });

        load();
    }
})();