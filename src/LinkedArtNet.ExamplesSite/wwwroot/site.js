document.addEventListener("DOMContentLoaded", () => {
  if (window.hljs) hljs.highlightAll();
  for (const section of document.querySelectorAll(".code-forms")) {
    for (const tab of section.querySelectorAll("button.form")) {
      tab.addEventListener("click", () => {
        for (const other of section.querySelectorAll("button.form")) {
          const active = other === tab;
          other.classList.toggle("active", active);
          other.setAttribute("aria-selected", active ? "true" : "false");
        }
        for (const pane of section.querySelectorAll("div[data-form]")) pane.hidden = pane.dataset.form !== tab.dataset.form;
        const copy = section.querySelector("button.copy");
        if (copy) copy.dataset.copy = tab.dataset.form;
      });
    }
  }
  for (const button of document.querySelectorAll("button.copy")) {
    button.addEventListener("click", async () => {
      const target = document.getElementById(button.dataset.copy);
      if (!target) return;
      try {
        await navigator.clipboard.writeText(target.textContent);
        const old = button.textContent;
        button.textContent = "Copied";
        setTimeout(() => (button.textContent = old), 1200);
      } catch {
        button.textContent = "Select and copy";
      }
    });
  }
});
