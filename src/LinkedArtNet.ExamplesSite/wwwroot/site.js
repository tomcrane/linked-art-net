document.addEventListener("DOMContentLoaded", () => {
  if (window.hljs) hljs.highlightAll();
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
