// Live preview on the review form: re-realise the canonical expression as it is edited.
document.addEventListener("DOMContentLoaded", () => {
  const canonical = document.getElementById("canonical");
  const preview = document.getElementById("preview");
  if (!canonical || !preview) return;
  const profile = document.getElementById("profile");
  const intent = document.getElementById("intent");
  const status = document.getElementById("expectedStatus");
  let timer = null;

  const escape = (s) => s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));

  async function refresh() {
    if (status.value !== "parsed") { preview.innerHTML = `<p class="muted">Expected status '${escape(status.value)}' needs no expression.</p>`; return; }
    const e = canonical.value.trim();
    if (!e) { preview.innerHTML = ""; return; }
    const url = `/dates/train/realise?e=${encodeURIComponent(e)}&profile=${encodeURIComponent(profile.value)}&intent=${encodeURIComponent(intent.value)}`;
    try {
      const r = await (await fetch(url, { credentials: "same-origin" })).json();
      if (!r.ok) { preview.innerHTML = `<p class="diff">${escape(r.error || "Not a canonical expression.")}</p>`; return; }
      const spans = [["TimeSpan", r.timespan], ["Begin", r.begin], ["End", r.end]].filter(([, v]) => v);
      preview.innerHTML =
        `<p><strong>Preview:</strong> <code>${escape(r.canonical || e)}</code>${r.edtf ? ` · EDTF <code>${escape(r.edtf)}</code>` : ""} · ${escape(r.kind)}</p>` +
        `<div class="json-pair">${spans.map(([l, v]) => `<div><h3>${l}</h3><pre><code>${escape(JSON.stringify(v, null, 2))}</code></pre></div>`).join("")}</div>`;
    } catch {
      preview.innerHTML = `<p class="diff">Preview unavailable.</p>`;
    }
  }

  const schedule = () => { clearTimeout(timer); timer = setTimeout(refresh, 300); };
  canonical.addEventListener("input", schedule);
  for (const el of [profile, intent, status]) el.addEventListener("change", schedule);
});
