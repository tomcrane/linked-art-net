// The tray (doc 12 section 13, item 2; the owner's walk through the Ackermanns, 2026-11-07): entities collected from any page of a
// project into a list held in this browser, then merged together at the merge page with the survivor, label and class chosen.
// Held per project in localStorage; a page that reloads after a merge with #tray-cleared empties it. Nothing here talks to the server.
(function () {
    var bar = document.getElementById("tray");
    if (!bar) return;
    var dataset = bar.getAttribute("data-dataset");
    var key = "tray:" + dataset;

    function read() {
        try { var v = JSON.parse(localStorage.getItem(key) || "[]"); return Array.isArray(v) ? v : []; } catch (e) { return []; }
    }
    function write(items) {
        try { localStorage.setItem(key, JSON.stringify(items)); } catch (e) { /* private window or storage off: the tray lasts the page */ }
        render(items);
    }
    function has(items, id) { return items.some(function (x) { return x.id === id; }); }

    function render(items) {
        document.querySelectorAll("[data-tray]").forEach(function (b) {
            var inTray = has(items, b.getAttribute("data-tray"));
            b.textContent = inTray ? "In the tray" : (b.getAttribute("data-tray-text") || "Add to tray");
            b.classList.toggle("in-tray", inTray);
            b.title = inTray ? "Click to take it out of the tray" : "Collect this entity to merge it with others from the tray";
        });
        if (items.length === 0) { bar.hidden = true; bar.innerHTML = ""; return; }
        bar.hidden = false;
        var parts = ['<span class="tray-title">Tray</span>'];
        items.forEach(function (x) {
            parts.push('<span class="tray-item"><a href="/p/' + encodeURIComponent(dataset) + '/entity?id=' + encodeURIComponent(x.id) + '">' + escapeHtml(x.label || x.id) + '</a> <button type="button" class="link" data-tray-remove="' + escapeAttr(x.id) + '" title="Take out of the tray">×</button></span>');
        });
        var ids = items.map(function (x) { return x.id; }).join(",");
        parts.push('<a class="button" href="/p/' + encodeURIComponent(dataset) + '/merge?ids=' + encodeURIComponent(ids) + '"' + (items.length < 2 ? ' aria-disabled="true" title="Collect at least two"' : "") + '>Merge these' + (items.length >= 2 ? " (" + items.length + ")" : "") + '</a>');
        parts.push('<button type="button" class="link" data-tray-clear title="Empty the tray">Clear</button>');
        bar.innerHTML = parts.join(" ");
    }
    function escapeHtml(s) { return String(s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); }
    function escapeAttr(s) { return escapeHtml(s); }

    document.addEventListener("click", function (ev) {
        var t = ev.target.closest ? ev.target.closest("[data-tray], [data-tray-remove], [data-tray-clear]") : null;
        if (!t) return;
        ev.preventDefault();
        var items = read();
        if (t.hasAttribute("data-tray-clear")) { write([]); return; }
        if (t.hasAttribute("data-tray-remove")) { write(items.filter(function (x) { return x.id !== t.getAttribute("data-tray-remove"); })); return; }
        var id = t.getAttribute("data-tray");
        if (has(items, id)) write(items.filter(function (x) { return x.id !== id; }));
        else { items.push({ id: id, label: t.getAttribute("data-tray-label") || id }); write(items); }
    });

    if (location.hash === "#tray-cleared") { write([]); history.replaceState(null, "", location.pathname + location.search); }
    else render(read());
})();
