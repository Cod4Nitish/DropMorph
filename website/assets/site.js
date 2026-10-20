// Theme toggle (remembered per viewer, falls back to the system theme), OS-aware downloads and the homepage donut demo.
(() => {
  // After a download starts: thanks, who made it, and a star link.
  const thanks = () => {
    let t = document.getElementById('dlThanks');
    if (!t) {
      t = document.createElement('div'); t.id = 'dlThanks'; t.setAttribute('role', 'status');
      t.innerHTML = '<button type="button" aria-label="Close">×</button><b>Thanks for downloading DropMorph!</b>'
        + '<span>Developed by <a href="https://github.com/cod4nitish">Nitish Singh</a></span>'
        + '<a class="t-star" href="https://github.com/Cod4Nitish/DropMorph">⭐ Star on GitHub to help the developer</a>';
      t.querySelector('button').onclick = () => t.classList.remove('on');
      document.body.append(t);
    }
    t.classList.add('on'); clearTimeout(t._h); t._h = setTimeout(() => t.classList.remove('on'), 12000);
  };
  document.addEventListener('click', e => {
    const a = e.target.closest && e.target.closest('a[href]');
    if (a && /\/releases\/(latest\/)?download\//.test(a.href)) thanks();
  });

  const root = document.documentElement;
  try { const t = localStorage.getItem('dm-theme'); if (t) root.dataset.theme = t; } catch {}
  const toggle = document.getElementById('themeToggle');
  if (toggle) toggle.addEventListener('click', () => {
    const dark = root.dataset.theme ? root.dataset.theme === 'dark' : matchMedia('(prefers-color-scheme: dark)').matches;
    root.dataset.theme = dark ? 'light' : 'dark';
    try { localStorage.setItem('dm-theme', root.dataset.theme); } catch {}
  });

  // Demo reel: autoplays muted (browsers require it); the button turns sound on from the start.
  const reel = document.getElementById('reel'), sound = document.getElementById('reelSound');
  if (reel && sound) sound.addEventListener('click', () => {
    const on = reel.muted;
    reel.muted = !on;
    if (on) { reel.currentTime = 0; reel.play().catch(() => {}); }
    sound.textContent = on ? '🔇 Mute' : '🔊 Sound on';
    sound.setAttribute('aria-pressed', String(on));
  });

  // Visitor's computer: the matching download card goes first and "Download" buttons point at it. Phones get a
  // notice (DropMorph runs on a computer) and a Share link button. Without JS everything stays Windows.
  const ua = navigator.userAgent;
  const phone = /Android|iPhone|iPod/.test(ua) || (/iPad|Macintosh/.test(ua) && navigator.maxTouchPoints > 1);
  const os = phone ? '' : /Windows/.test(ua) ? 'windows' : /Macintosh|Mac OS X/.test(ua) ? 'mac' : /Linux|X11/.test(ua) && !/CrOS/.test(ua) ? 'linux' : '';
  let arch = '';
  // Apple silicon or Intel: Chrome and Firefox name the GPU ("Apple M2", "Intel Iris"), Safari says "Apple GPU". Unsure: no guess.
  if (os === 'mac') try {
    const gl = document.createElement('canvas').getContext('webgl'), ext = gl && gl.getExtension('WEBGL_debug_renderer_info');
    const gpu = gl ? String(gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER)) : '';
    arch = /Apple (M\d|GPU)/.test(gpu) ? 'arm' : /Intel|AMD|Radeon|NVIDIA/i.test(gpu) ? 'intel' : '';
    const lose = gl && gl.getExtension('WEBGL_lose_context'); if (lose) lose.loseContext();
  } catch {}
  if (phone) root.classList.add('is-phone');
  const guides = { mac: '/install/mac/', linux: '/install/linux/' };
  document.querySelectorAll('[data-dl]').forEach(a => {
    const label = a.querySelector('span');
    if (phone) { a.href = '/#download'; label.textContent = 'Download options'; }
    else if (os === 'mac') { a.href = arch ? a.dataset[arch] : guides.mac; label.textContent = 'Download for Mac'; }
    else if (os === 'linux') { a.href = guides.linux; label.textContent = 'Download for Linux'; }
  });
  if (guides[os]) document.querySelectorAll('[data-guide]').forEach(a => { a.href = guides[os]; });
  // On a phone its own card (Android or iPhone), and the phone cards come before the computer ones.
  const phoneOs = phone ? (/Android/i.test(navigator.userAgent) ? 'android' : 'ios') : '';
  if (phoneOs) { const grid = document.querySelector('.phone-grid'), head = document.querySelector('.phone-head'), pcs = document.querySelector('.os-grid:not(.phone-grid)');
    if (grid && pcs) { pcs.before(head, grid); } }
  const card = document.querySelector(`.os-card[data-os="${phoneOs || os}"]`);
  if (card) {
    card.classList.add('is-you');
    card.parentElement.prepend(card);
    const pick = arch && card.querySelector(`[data-arch="${arch}"]`);
    if (pick) card.querySelectorAll('[data-arch]').forEach(b => {
      b.classList.toggle('rec', b === pick); b.classList.toggle('btn-accent', b === pick); b.classList.toggle('btn-line', b !== pick);
    });
  }

  // Copy buttons for the install commands, and Share (falls back to copying the link).
  const status = document.createElement('span');
  status.className = 'sr-only'; status.setAttribute('aria-live', 'polite'); document.body.append(status);
  const flash = (btn, text) => {
    const old = btn.dataset.label || (btn.dataset.label = btn.textContent);
    btn.textContent = status.textContent = text;
    setTimeout(() => { btn.textContent = old; }, 2000);
  };
  const copy = async (btn, text, el) => {
    try { await navigator.clipboard.writeText(text); flash(btn, 'Copied!'); return; } catch {}
    if (el) { getSelection().selectAllChildren(el); flash(btn, 'Selected'); } else prompt('Copy this link:', text);
  };
  document.querySelectorAll('[data-copy]').forEach(b => b.addEventListener('click', () => {
    const pre = b.previousElementSibling; copy(b, pre.textContent.trim(), pre);
  }));
  document.querySelectorAll('[data-share]').forEach(b => b.addEventListener('click', async () => {
    const url = location.origin + '/#download';
    if (navigator.share) { try { await navigator.share({ title: 'DropMorph', text: 'DropMorph, a free file converter. Open this on your computer:', url }); } catch {} return; }
    copy(b, url);
  }));

  // GitHub star count on the Star buttons: fetched once per session (the API allows 60 calls an hour), silent on failure.
  const starEls = document.querySelectorAll('[data-stars]');
  const showStars = n => {
    if (!(n > 0)) return;
    const text = '⭐ ' + (n >= 1000 ? (n / 1000).toFixed(1).replace(/\.0$/, '') + 'k' : n);
    starEls.forEach(el => { el.textContent = text; el.hidden = false; });
  };
  if (starEls.length) {
    let cached = null; try { cached = sessionStorage.getItem('dm-stars'); } catch {}
    if (cached) showStars(+cached);
    else fetch('https://api.github.com/repos/Cod4Nitish/DropMorph').then(r => r.ok ? r.json() : null).then(j => {
      if (!j) return;
      showStars(j.stargazers_count);
      try { sessionStorage.setItem('dm-stars', j.stargazers_count); } catch {}
    }).catch(() => {});
  }

  const stage = document.getElementById('stage');
  if (!stage) return;
  const files = {
    jpg:  { name: 'trip.jpg',    color: '#0D9488', to: ['PNG', 'WEBP', 'PDF', 'AVIF', 'GIF', 'ICO'] },
    docx: { name: 'report.docx', color: '#378ADD', to: ['PDF', 'TXT', 'EPUB', 'HTML', 'HWPX'] },
    wav:  { name: 'song.wav',    color: '#D4537E', to: ['MP3', 'M4A', 'FLAC', 'OGG', 'OPUS', 'AAC'] },
  };
  const colorOf = { PDF:'#E04A3F', PNG:'#0D9488', WEBP:'#0B6F66', AVIF:'#0D9488', GIF:'#BA7517', ICO:'#639922', TXT:'#5F5E5A', EPUB:'#639922', HTML:'#FF6B35', HWPX:'#378ADD', MP3:'#D4537E', M4A:'#7F77DD', FLAC:'#0B6F66', OGG:'#BA7517', OPUS:'#378ADD', AAC:'#FF6B35' };
  const donut = document.getElementById('donut'), ring = document.getElementById('ring'),
        hint = document.getElementById('hint'), picker = document.getElementById('picker');
  const R2 = 132, R1 = 58, idle = 'Drag the file. A donut of formats opens, so drop it on one.';
  let kind = 'jpg', chip, results = [], drag = null;

  Object.entries(files).forEach(([k, f]) => {
    const b = document.createElement('button');
    b.type = 'button';
    b.textContent = f.name.split('.').pop().toUpperCase();
    b.setAttribute('aria-pressed', k === kind);
    b.onclick = () => { kind = k; [...picker.children].forEach(c => c.setAttribute('aria-pressed', c === b)); reset(true); };
    picker.appendChild(b);
  });

  function makeChip(name, color, cls) {
    const el = document.createElement('div');
    el.className = 'chip ' + (cls || '');
    el.innerHTML = `<div class="doc" style="background:${color}">${name.split('.').pop().toUpperCase()}</div><div class="name">${name}</div>`;
    stage.appendChild(el);
    return el;
  }
  const home = () => ({ x: 40, y: stage.clientHeight / 2 - 52 });
  const place = (el, x, y) => { el.style.left = x + 'px'; el.style.top = y + 'px'; };

  function reset(clearResults) {
    if (clearResults) { results.forEach(r => r.remove()); results = []; }
    if (chip) chip.remove();
    const f = files[kind];
    chip = makeChip(f.name, f.color);
    const h = home(); place(chip, h.x, h.y);
    chip.addEventListener('pointerdown', start);
  }

  function arc(i, n, hot) {
    const span = 2 * Math.PI / n, gap = .05, mid = -Math.PI / 2 + i * span;
    const s = mid - span / 2 + gap / 2, e = mid + span / 2 - gap / 2, r2 = hot ? R2 + 6 : R2;
    const p = (r, a) => `${(r * Math.cos(a)).toFixed(1)} ${(r * Math.sin(a)).toFixed(1)}`;
    return `M${p(r2, s)} A${r2} ${r2} 0 0 1 ${p(r2, e)} L${p(R1, e)} A${R1} ${R1} 0 0 0 ${p(R1, s)} Z`;
  }
  function drawDonut(hot) {
    const to = files[kind].to, n = to.length, span = 2 * Math.PI / n;
    donut.innerHTML = to.map((fmt, i) => {
      const a = -Math.PI / 2 + i * span, r = (R1 + R2) / 2;
      return `<path class="${i === hot ? 'hot' : ''}" d="${arc(i, n, i === hot)}"/>` +
             `<text class="${i === hot ? 'hot' : ''}" x="${(r * Math.cos(a)).toFixed(1)}" y="${(r * Math.sin(a)).toFixed(1)}">${fmt}</text>`;
    }).join('');
  }
  function hitTest(x, y) {
    const dx = x - drag.cx, dy = y - drag.cy, d = Math.hypot(dx, dy), n = files[kind].to.length;
    if (d < R1 || d > R2 + 24) return -1;
    const span = 2 * Math.PI / n;
    let a = Math.atan2(dy, dx) + Math.PI / 2 + span / 2;
    a = ((a % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
    return Math.floor(a / span) % n;
  }
  const local = e => { const r = stage.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; };

  function start(e) {
    e.preventDefault();
    chip.setPointerCapture(e.pointerId);
    const p = local(e), w = stage.clientWidth, h = stage.clientHeight;
    const cx = Math.min(Math.max(p.x + 150, R2 + 10), w - R2 - 10), cy = Math.min(Math.max(p.y, R2 + 46), h - R2 - 10);
    drag = { ox: p.x - chip.offsetLeft, oy: p.y - chip.offsetTop, cx, cy, hot: -1 };
    chip.classList.add('dragging');
    donut.style.left = cx + 'px'; donut.style.top = cy + 'px';
    drawDonut(-1); donut.classList.add('open');
    hint.textContent = 'Now drop it on a format';
    chip.addEventListener('pointermove', move);
    chip.addEventListener('pointerup', end, { once: true });
    chip.addEventListener('pointercancel', end, { once: true });
  }
  function move(e) {
    if (!drag) return;
    const p = local(e);
    place(chip, p.x - drag.ox, p.y - drag.oy);
    const hot = hitTest(p.x, p.y);
    if (hot !== drag.hot) { drag.hot = hot; drawDonut(hot); }
  }
  function end() {
    chip.removeEventListener('pointermove', move);
    chip.classList.remove('dragging');
    donut.classList.remove('open');
    const d = drag; drag = null;
    if (!d || d.hot < 0) { const h = home(); place(chip, h.x, h.y); hint.textContent = idle; return; }
    const fmt = files[kind].to[d.hot];
    chip.style.opacity = '0';
    ring.style.left = d.cx + 'px'; ring.style.top = d.cy + 'px';
    ring.classList.remove('on'); void ring.offsetWidth; ring.classList.add('on');
    hint.textContent = `Converting to ${fmt}…`;
    setTimeout(() => {
      ring.classList.remove('on');
      const base = files[kind].name.replace(/\.[^.]+$/, '');
      const out = makeChip(`${base}.${fmt.toLowerCase()}`, colorOf[fmt] || '#0D9488', 'result');
      place(out, d.cx - 46, d.cy - 52);
      results.push(out);
      if (results.length > 3) results.shift().remove();
      hint.textContent = `Done: ${base}.${fmt.toLowerCase()} is saved next to the original. Try another one!`;
      reset(false);
    }, 950);
  }

  reset(true);
  addEventListener('resize', () => { if (!drag) { const h = home(); place(chip, h.x, h.y); } });
})();
