// FYP site interop
window.fypSite = {
  countUp: (el, end, duration = 900) => {
    if (!el) return;
    const startTs = performance.now();
    const startVal = 0;
    function step(now) {
      const p = Math.min(1, (now - startTs) / duration);
      const eased = 1 - Math.pow(1 - p, 3);
      el.textContent = Math.round(startVal + (end - startVal) * eased).toString();
      if (p < 1) requestAnimationFrame(step);
    }
    requestAnimationFrame(step);
  },
  drawChart: (canvasId, config) => {
    if (typeof Chart === 'undefined') return;
    const c = document.getElementById(canvasId);
    if (!c) return;
    if (c._chart) c._chart.destroy();
    c._chart = new Chart(c, config);
  },
  ripple: (e) => {
    const btn = e.currentTarget;
    const r = document.createElement('span');
    const d = Math.max(btn.clientWidth, btn.clientHeight);
    r.className = 'ripple';
    r.style.width = r.style.height = d + 'px';
    r.style.left = (e.clientX - btn.getBoundingClientRect().left - d/2) + 'px';
    r.style.top  = (e.clientY - btn.getBoundingClientRect().top - d/2) + 'px';
    btn.appendChild(r);
    setTimeout(() => r.remove(), 600);
  },
  downloadFile: (filename, content, mime = 'text/csv') => {
    const blob = new Blob([content], { type: mime });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = filename;
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1500);
  }
};
document.addEventListener('click', (e) => {
  const t = e.target.closest('.btn');
  if (t) window.fypSite.ripple({ currentTarget: t, clientX: e.clientX, clientY: e.clientY });

  const demo = e.target.closest('.demo-pill');
  if (demo) {
    const email = document.getElementById('email');
    const password = document.getElementById('password');
    if (email) email.value = demo.dataset.email || '';
    if (password) password.value = demo.dataset.password || '';
  }
});
