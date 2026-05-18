// FYP site interop
window.fypSite = (function () {
  const STORAGE_KEY = 'fyp-theme';

  function getCssVar(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  }

  function readThemeColors() {
    return {
      ink: getCssVar('--ink'),
      muted: getCssVar('--muted'),
      accent: getCssVar('--accent'),
      accentDeep: getCssVar('--accent-deep'),
      accentTint: getCssVar('--accent-tint'),
      success: getCssVar('--success'),
      warning: getCssVar('--warning'),
      danger: getCssVar('--danger'),
      info: getCssVar('--info'),
      chart: [
        getCssVar('--chart-1'),
        getCssVar('--chart-2'),
        getCssVar('--chart-3'),
        getCssVar('--chart-4'),
        getCssVar('--chart-5')
      ],
      grid: getCssVar('--chart-grid'),
      text: getCssVar('--chart-text')
    };
  }

  function applyThemeToChartDefaults() {
    if (typeof Chart === 'undefined') return;
    const c = readThemeColors();
    Chart.defaults.color = c.text;
    Chart.defaults.borderColor = c.grid;
    Chart.defaults.font.family = "'Geist', 'Inter Tight', system-ui, sans-serif";
    Chart.defaults.font.size = 11;
  }

  // Run once Chart loads (Chart umd loads before this file)
  applyThemeToChartDefaults();

  function setTheme(theme, persist) {
    if (theme !== 'light' && theme !== 'dark') return;
    document.documentElement.setAttribute('data-theme', theme);
    if (persist !== false) {
      try { localStorage.setItem(STORAGE_KEY, theme); } catch (e) { }
    }
    applyThemeToChartDefaults();
    // Tell pages to recolor any active charts
    window.dispatchEvent(new CustomEvent('fyp-theme-changed', { detail: { theme: theme, colors: readThemeColors() } }));
  }

  function toggleTheme() {
    const next = (document.documentElement.getAttribute('data-theme') === 'dark') ? 'light' : 'dark';
    setTheme(next, true);
  }

  function currentTheme() {
    return document.documentElement.getAttribute('data-theme') || 'light';
  }

  return {
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

    // Theme-aware dashboard charts. Reads CSS vars so colors track light/dark.
    renderDashboardCharts: (payload) => {
      if (typeof Chart === 'undefined') return;

      function withAlpha(hex, a) {
        // Accept hex (#rrggbb / #rgb) or rgb(); produce rgba(...)
        if (!hex) return 'rgba(0,0,0,' + a + ')';
        hex = hex.trim();
        if (hex.startsWith('rgb')) {
          return hex.replace('rgb(', 'rgba(').replace(')', ',' + a + ')');
        }
        let h = hex.replace('#', '');
        if (h.length === 3) h = h.split('').map(c => c + c).join('');
        const r = parseInt(h.slice(0, 2), 16);
        const g = parseInt(h.slice(2, 4), 16);
        const b = parseInt(h.slice(4, 6), 16);
        return 'rgba(' + r + ',' + g + ',' + b + ',' + a + ')';
      }

      function build() {
        const c = readThemeColors();
        const accent = c.accent || '#6e3f5a';
        const palette = (c.chart && c.chart.filter(Boolean).length === 5) ? c.chart : [accent, c.info, c.success, c.warning, c.muted];

        applyThemeToChartDefaults();

        const baseGrid = { color: c.grid, drawBorder: false };
        const baseTicks = { color: c.text, font: { size: 11 } };

        // Doughnut: Projects by status
        if (payload.status) {
          window.fypSite.drawChart('chartStatus', {
            type: 'doughnut',
            data: {
              labels: payload.status.labels,
              datasets: [{
                data: payload.status.values,
                backgroundColor: payload.status.labels.map((_, i) => palette[i % palette.length]),
                borderColor: c.surface || '#fff',
                borderWidth: 2,
                hoverOffset: 8
              }]
            },
            options: {
              cutout: '62%',
              animation: { animateRotate: true, animateScale: true, duration: 850, easing: 'easeOutCubic' },
              plugins: {
                legend: { position: 'bottom', labels: { color: c.text, padding: 14, usePointStyle: true, pointStyle: 'circle', font: { size: 11 } } },
                tooltip: { backgroundColor: c.ink, titleColor: '#fff', bodyColor: '#eee', borderColor: c.accent, borderWidth: 1, padding: 10, cornerRadius: 8 }
              }
            }
          });
        }

        // Bar: Milestones / month
        if (payload.month) {
          window.fypSite.drawChart('chartMs', {
            type: 'bar',
            data: {
              labels: payload.month.labels,
              datasets: [{
                label: 'Completed',
                data: payload.month.values,
                backgroundColor: withAlpha(accent, 0.75),
                hoverBackgroundColor: accent,
                borderRadius: 6,
                borderSkipped: false,
                barPercentage: 0.62,
                categoryPercentage: 0.7
              }]
            },
            options: {
              animation: { duration: 800, easing: 'easeOutQuart' },
              plugins: {
                legend: { display: false },
                tooltip: { backgroundColor: c.ink, titleColor: '#fff', bodyColor: '#eee', borderColor: c.accent, borderWidth: 1, padding: 10, cornerRadius: 8 }
              },
              scales: {
                x: { grid: { display: false }, ticks: baseTicks, border: { color: c.grid } },
                y: { grid: baseGrid, ticks: baseTicks, beginAtZero: true, border: { display: false } }
              }
            }
          });
        }

        // Line: Proposal submissions trend
        if (payload.props) {
          const cnv = document.getElementById('chartProps');
          let gradient = withAlpha(accent, 0.22);
          if (cnv) {
            const ctx = cnv.getContext('2d');
            const g = ctx.createLinearGradient(0, 0, 0, cnv.height || 160);
            g.addColorStop(0, withAlpha(accent, 0.32));
            g.addColorStop(1, withAlpha(accent, 0.02));
            gradient = g;
          }
          window.fypSite.drawChart('chartProps', {
            type: 'line',
            data: {
              labels: payload.props.labels,
              datasets: [{
                label: 'Submissions',
                data: payload.props.values,
                borderColor: accent,
                backgroundColor: gradient,
                pointBackgroundColor: accent,
                pointBorderColor: c.surface || '#fff',
                pointBorderWidth: 2,
                pointRadius: 4,
                pointHoverRadius: 6,
                fill: true,
                tension: 0.4,
                borderWidth: 2
              }]
            },
            options: {
              animation: { duration: 900, easing: 'easeOutQuart' },
              plugins: {
                legend: { display: false },
                tooltip: { backgroundColor: c.ink, titleColor: '#fff', bodyColor: '#eee', borderColor: c.accent, borderWidth: 1, padding: 10, cornerRadius: 8 }
              },
              scales: {
                x: { grid: { display: false }, ticks: baseTicks, border: { color: c.grid } },
                y: { grid: baseGrid, ticks: baseTicks, beginAtZero: true, border: { display: false } }
              }
            }
          });
        }
      }

      build();

      // Re-render on theme change so charts pick up new CSS vars.
      // De-dupe by storing the latest payload + a single listener.
      if (window.__fypChartListener) {
        window.removeEventListener('fyp-theme-changed', window.__fypChartListener);
      }
      window.__fypLastChartPayload = payload;
      window.__fypChartListener = function () {
        if (window.__fypLastChartPayload) {
          window.fypSite.renderDashboardCharts(window.__fypLastChartPayload);
        }
      };
      window.addEventListener('fyp-theme-changed', window.__fypChartListener);
    },

    ripple: (e) => {
      const btn = e.currentTarget;
      const r = document.createElement('span');
      const d = Math.max(btn.clientWidth, btn.clientHeight);
      r.className = 'ripple';
      r.style.width = r.style.height = d + 'px';
      r.style.left = (e.clientX - btn.getBoundingClientRect().left - d / 2) + 'px';
      r.style.top = (e.clientY - btn.getBoundingClientRect().top - d / 2) + 'px';
      btn.appendChild(r);
      setTimeout(() => r.remove(), 700);
    },

    downloadFile: (filename, content, mime = 'text/csv') => {
      const blob = new Blob([content], { type: mime });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = filename;
      document.body.appendChild(a); a.click(); a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1500);
    },

    setTheme: setTheme,
    toggleTheme: toggleTheme,
    currentTheme: currentTheme,
    themeColors: readThemeColors
  };
})();

// Delegated event handling
document.addEventListener('click', (e) => {
  // Ripple on .btn
  const t = e.target.closest('.btn');
  if (t) window.fypSite.ripple({ currentTarget: t, clientX: e.clientX, clientY: e.clientY });

  // Theme toggle button(s)
  const toggle = e.target.closest('[data-action="toggle-theme"]');
  if (toggle) {
    e.preventDefault();
    window.fypSite.toggleTheme();
  }

  // Demo credential pill -> fill form
  const demo = e.target.closest('.demo-pill');
  if (demo) {
    const email = document.getElementById('email');
    const password = document.getElementById('password');
    if (email) email.value = demo.dataset.email || '';
    if (password) password.value = demo.dataset.password || '';
    if (password && password.type === 'password') {
      const ev = new Event('input', { bubbles: true });
      password.dispatchEvent(ev);
    }
  }
});

// Keyboard shortcut: ⌘/Ctrl + J toggles theme
document.addEventListener('keydown', (e) => {
  if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'j') {
    e.preventDefault();
    window.fypSite.toggleTheme();
  }
});
