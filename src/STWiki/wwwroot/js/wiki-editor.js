// wiki-editor.js  —  ES module
// Core: small, deterministic, plugin-driven. No globals except opt-in registry.

export class WikiEditor {
  /** @param {HTMLElement} container - .editor-container root with data-* hooks
      @param {Object} opts - { initialContent, format, plugins: [PluginClass, ...] } */
  constructor(container, opts = {}) {
    if (!container) throw new Error('WikiEditor: container required');
    this.container = container;
    this.textarea = container.querySelector('[data-role="editor"]');
    if (!this.textarea) throw new Error('WikiEditor: [data-role="editor"] not found');

    this.previewEl = container.querySelector('[data-role="preview"]');
    this.wordEl = container.querySelector('[data-role="word-count"]');
    this.charEl = container.querySelector('[data-role="char-count"]');
    this.statusEl = container.querySelector('[data-role="status"]');
    this.hiddenField = container.querySelector('[data-role="body"]'); // optional

    this.format = (opts.format || container.dataset.format || 'markdown').toLowerCase();
    this.lastContent = '';
    this.ac = new AbortController(); // all listeners hang off this
    this.plugins = [];
    this.state = {
      isRemote: false,
      selectedAutocompleteIndex: 0,
      dropdown: null,
      mirror: null, // for cursor/selection metrics (if plugin needs)
    };

    // init content
    const existing = this.textarea.value || '';
    const param = opts.initialContent || '';
    this.textarea.value = (param.length >= existing.length) ? param : existing;

    // init plugins
    const pluginClasses = opts.plugins || [];
    for (const P of pluginClasses) {
      const plugin = new P(this);        // lifecycle: init/destroy
      plugin.init?.();
      this.plugins.push(plugin);
    }

    // core listeners (one each)
    this.textarea.addEventListener('input', this.onInput, { signal: this.ac.signal });
    this.textarea.addEventListener('keydown', this.onKeydown, { signal: this.ac.signal });

    // initial paint
    this.render();
  }

  // --- Core events (dispatch to plugins) ---
  onInput = (e) => {
    const v = this.textarea.value;
    if (v === this.lastContent) return;
    this.lastContent = v;
    this.hiddenField && (this.hiddenField.value = v);
    this.hiddenField && this.hiddenField.dispatchEvent(new Event('input', { bubbles: true }));
    for (const p of this.plugins) p.onInput?.(e, v);
    this.render();
  };

  onKeydown = (e) => {
    for (const p of this.plugins) {
      if (p.onKeydown?.(e) === true) return; // allow plugin to short-circuit
    }
  };

  // --- Core render (preview + stats via plugins) ---
  async render() {
    for (const p of this.plugins) {
      await p.onRender?.();
    }
  }

  // --- External API ---
  get value() { return this.textarea.value; }
  set value(v) {
    this.textarea.value = v ?? '';
    this.lastContent = this.textarea.value;
    this.render();
  }

  setFormat(fmt) {
    this.format = (fmt || 'markdown').toLowerCase();
    this.render();
  }

  destroy() {
    this.ac.abort();
    for (const p of this.plugins) p.destroy?.();
    this.plugins = [];
    this.state.mirror && this.state.mirror.remove();
    this.state.mirror = null;
  }
}

/* ---------------- Plugins ---------------- */

// Utility: cheap debounce
const debounce = (fn, ms=200) => {
  let t; return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
};

// Utility: basic word/char count
const countWords = (s) => (s.trim() ? s.trim().split(/\s+/).length : 0);

// Utility: simple HTML escape
const esc = (s) => s.replace(/[&<>"']/g, m => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]));

// Optional: pluggable markdown engine; fallback to very small rules
function fallbackMarkdown(md) {
  let html = md;
  // protect code blocks
  const code = [];
  html = html.replace(/```(\w+)?\n?([\s\S]*?)```/g, (_, lang='', body) => {
    const i = code.push(`<pre><code class="language-${esc(lang)}">${esc(body)}</code></pre>`) - 1;
    return `§§CODE${i}§§`;
  });
  // inline code
  const inline = [];
  html = html.replace(/`([^`]+)`/g, (_, body) => {
    const i = inline.push(`<code>${esc(body)}</code>`) - 1;
    return `§§INL${i}§§`;
  });
  // headers, emph, strong, links (basic)
  html = esc(html)
    .replace(/^### (.*)$/gim, '<h3>$1</h3>')
    .replace(/^## (.*)$/gim, '<h2>$1</h2>')
    .replace(/^# (.*)$/gim, '<h1>$1</h1>')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.+?)\*/g, '<em>$1</em>')
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>')
    .replace(/^> (.*)$/gim, '<blockquote>$1</blockquote>')
    .replace(/^(?:\*|\-|\d+\.) (.*)$/gim, '<li>$1</li>')
    .replace(/(<li>.*<\/li>)(\n<li>.*<\/li>)+/g, m => `<ul>${m.replace(/\n/g,'')}</ul>`)
    .replace(/\n{2,}/g, '</p><p>')
    .replace(/^(?!<h\d|<ul|<pre|<blockquote|<p|<figure|<code)/, '<p>');
  inline.forEach((h,i)=> html = html.replace(`§§INL${i}§§`, h));
  code.forEach((h,i)=> html = html.replace(`§§CODE${i}§§`, h));
  return html.endsWith('</p>') ? html : html + '</p>';
}

// PreviewPlugin: renders markdown or html, async friendly
export class PreviewPlugin {
  constructor(editor, opts={}) {
    this.editor = editor;
    this.debounced = debounce(() => this.render(), opts.delay ?? 120);
    this.worker = opts.worker || null; // optional Web Worker for heavy parsing
    this.resolveWikiMacros = opts.resolveWikiMacros || (async (html)=>html);
  }
  init() { /* no-op */ }
  async onInput() { this.debounced(); }
  async onRender() { this.debounced(); } // unify
  async render() {
    const { editor } = this;
    if (!editor.previewEl) return;
    const raw = editor.value;
    if (!raw.trim()) {
      editor.previewEl.innerHTML = '<em class="text-muted">Preview…</em>';
      return;
    }
    let html;
    if (editor.format === 'html') {
      html = raw; // trust upstream; sanitize on server if needed
    } else if (this.worker) {
      html = await this.renderInWorker(raw, editor.format);
    } else {
      html = fallbackMarkdown(raw);
    }
    // wiki macro pass (async)
    html = await this.resolveWikiMacros(html);
    editor.previewEl.innerHTML = html;
    // optional Prism hook
    if (window.Prism?.highlightAllUnder) {
      try { window.Prism.highlightAllUnder(editor.previewEl); } catch {}
    }
  }
  renderInWorker(text, fmt) {
    return new Promise((res, rej) => {
      const w = this.worker;
      const onMsg = (e) => { w.removeEventListener('message', onMsg); res(e.data.html); };
      const onErr = (e) => { w.removeEventListener('error', onErr); rej(e); };
      w.addEventListener('message', onMsg);
      w.addEventListener('error', onErr);
      w.postMessage({ type:'render', format: fmt, text });
    });
  }
  destroy() { /* worker is external */ }
}

// StatsPlugin: keeps counts fresh
export class StatsPlugin {
  constructor(editor){ this.editor = editor; }
  onInput(){ this.update(); }
  onRender(){ this.update(); }
  update(){
    const { editor } = this;
    editor.wordEl && (editor.wordEl.textContent = `${countWords(editor.value)} words`);
    editor.charEl && (editor.charEl.textContent = `${editor.value.length} characters`);
  }
}

// FormattingPlugin: keyboard shortcuts + simple wrap API
export class FormattingPlugin {
  constructor(editor){ this.editor = editor; }
  onKeydown(e){
    if (!(e.ctrlKey||e.metaKey)) return false;
    const ta = this.editor.textarea;
    const wrap = (before, after) => {
      const s = ta.selectionStart, e2 = ta.selectionEnd;
      const sel = ta.value.slice(s, e2);
      ta.setRangeText(before + sel + after, s, e2, 'end');
      this.editor.onInput(new Event('input')); // ensure downstream updates
    };
    switch (e.key.toLowerCase()) {
      case 'b': e.preventDefault(); wrap('**','**'); return true;
      case 'i': e.preventDefault(); wrap('*','*'); return true;
      case 'k': e.preventDefault(); {
        e.preventDefault();
        const s = ta.selectionStart, e2 = ta.selectionEnd;
        const txt = ta.value.slice(s, e2) || 'Link text';
        const ins = `[${txt}](url)`;
        ta.setRangeText(ins, s, e2, 'select');
        // select URL
        const start = s + 1 + txt.length + 2; // [ + txt + ](
        ta.selectionStart = start; ta.selectionEnd = start + 3; // 'url'
        this.editor.onInput(new Event('input'));
        return true;
      }
    }
    return false;
  }
}

/** AutocompletePlugin (skeleton)
 * - Owns a dropdown inside container
 * - Never touches document listeners; uses container-level only
 */
export class AutocompletePlugin {
  constructor(editor, opts={}) {
    this.editor = editor;
    this.fetchPages = opts.fetchPages || (async ()=>[]);
    this.fetchMedia = opts.fetchMedia || (async ()=>[]);
    this.dropdown = null;
    this.ac = new AbortController();
  }
  init(){
    // create dropdown once
    this.dropdown = document.createElement('div');
    this.dropdown.className = 'autocomplete d-none';
    this.dropdown.setAttribute('data-role','autocomplete');
    this.editor.container.appendChild(this.dropdown);

    // local listeners
    this.editor.container.addEventListener('mousedown', (e)=>{
      const item = e.target.closest('[data-item]');
      if (!item) return;
      e.preventDefault();
      this.apply(item.dataset.value);
    }, { signal: this.ac.signal });

    // keydown steering stays in onKeydown to avoid multi-handlers
  }
  onInput(e){
    const ta = this.editor.textarea;
    const before = ta.value.slice(0, ta.selectionStart);
    if (/\[\[$/i.test(before)) return this.open('page');
    if (/\[\[media:$/i.test(before)) return this.open('media');
    this.hide();
  }
  onKeydown(e){
    if (this.dropdown?.classList.contains('d-none')) return false;
    if (e.key === 'Escape') { this.hide(); return true; }
    if (e.key === 'Enter' || e.key === 'Tab') {
      e.preventDefault();
      const el = this.dropdown.querySelector('[data-item].selected') || this.dropdown.querySelector('[data-item]');
      if (el) this.apply(el.dataset.value);
      return true;
    }
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      this.move(e.key === 'ArrowDown' ? 1 : -1);
      return true;
    }
    return false;
  }
  async open(mode){
    const items = mode === 'media' ? await this.fetchMedia() : await this.fetchPages();
    if (!items.length) return this.hide();
    this.dropdown.innerHTML = items.map((it,i)=>
      `<div data-item data-value="${it.value}" class="autocomplete-item${i===0?' selected':''}">
         <div class="t">${it.title||it.value}</div>
         ${it.sub ? `<div class="s">${it.sub}</div>`:''}
       </div>`).join('');
    this.dropdown.classList.remove('d-none');
    // naive positioning: stick to bottom-left of textarea; refine if you want caret-based positioning
    const r = this.editor.textarea.getBoundingClientRect();
    Object.assign(this.dropdown.style, { position:'absolute', left:`${r.left + window.scrollX}px`, top:`${r.bottom + window.scrollY + 4}px`, zIndex:1000 });
  }
  move(delta){
    const all = [...this.dropdown.querySelectorAll('[data-item]')];
    const i = Math.max(0, Math.min(all.length-1, all.findIndex(x=>x.classList.contains('selected')) + delta));
    all.forEach(x=>x.classList.remove('selected'));
    all[i].classList.add('selected');
    all[i].scrollIntoView({ block:'nearest' });
  }
  apply(value){
    const ta = this.editor.textarea;
    const pos = ta.selectionStart;
    const before = ta.value.slice(0, pos);
    const start = before.lastIndexOf('[[');
    if (start < 0) return this.hide();
    const mode = /\[\[media:/i.test(before.slice(start)) ? 'media' : 'page';
    const replacement = mode === 'media' ? `[[media:${value}]]` : `[[${value}]]`;
    ta.setRangeText(replacement, start, pos, 'end');
    this.editor.onInput(new Event('input'));
    this.hide();
  }
  hide(){ this.dropdown?.classList.add('d-none'); }
  destroy(){ this.ac.abort(); this.dropdown?.remove(); }
}

/** DragDropPlugin (skeleton)
 * Provide a single overlay per instance; no document-level drag gymnastics.
 */
export class DragDropPlugin {
  constructor(editor, opts={}) {
    this.editor = editor;
    this.upload = opts.upload || (async (file)=>({fileName:file.name}));
    this.ac = new AbortController();
    this.overlay = null;
  }
  init(){
    const ov = document.createElement('div');
    ov.className = 'drag-overlay d-none';
    ov.textContent = 'Drop image to upload';
    ov.style.cssText = 'position:absolute;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,.3);color:#fff;';
    this.editor.container.style.position = this.editor.container.style.position || 'relative';
    this.editor.container.appendChild(ov);
    this.overlay = ov;

    const sig = { signal: this.ac.signal };
    const over = (e)=>{ e.preventDefault(); ov.classList.remove('d-none'); };
    const leave= (e)=>{ e.preventDefault(); ov.classList.add('d-none'); };
    const drop = async (e)=>{
      e.preventDefault(); ov.classList.add('d-none');
      const f = [...e.dataTransfer.files].find(f=>f.type.startsWith('image/'));
      if (!f) return;
      const r = await this.upload(f); // { fileName }
      const ta = this.editor.textarea;
      const ins = `[[media:${r.fileName}|display=thumb]]`;
      ta.setRangeText(ins, ta.selectionStart, ta.selectionEnd, 'end');
      this.editor.onInput(new Event('input'));
    };
    this.editor.container.addEventListener('dragover', over, sig);
    this.editor.container.addEventListener('dragleave', leave, sig);
    this.editor.container.addEventListener('drop', drop, sig);
  }
  destroy(){ this.ac.abort(); this.overlay?.remove(); }
}

/** CollabPlugin (skeleton)
 * Remote ops guarded by state.isRemote to avoid echo.
 */
export class CollabPlugin {
  constructor(editor, opts={}) {
    this.editor = editor;
    this.send = opts.send || (()=>{});
  }
  onInput(e, v){
    if (this.editor.state.isRemote) return;
    this.send({ type:'set', value: v });
  }
  applyInsert(pos, text){
    const ta = this.editor.textarea;
    this.editor.state.isRemote = true;
    ta.setRangeText(text, pos, pos, 'end');
    this.editor.onInput(new Event('input'));
    this.editor.state.isRemote = false;
  }
  applyDelete(pos, len){
    const ta = this.editor.textarea;
    this.editor.state.isRemote = true;
    ta.setRangeText('', pos, pos+len, 'end');
    this.editor.onInput(new Event('input'));
    this.editor.state.isRemote = false;
  }
}

// ---- Helper to boot one editor ----
export function bootEditor(container, options={}) {
  const ed = new WikiEditor(container, {
    initialContent: options.initialContent,
    format: options.format,
    plugins: options.plugins || [PreviewPlugin, StatsPlugin, FormattingPlugin]
  });
  return ed;
}

// (Optional) global registry for legacy interop, without leaking APIs everywhere
if (!window.STWikiEditors) window.STWikiEditors = new Map();
export function mountById(containerId, options){
  const el = document.getElementById(containerId);
  const ed = bootEditor(el, options);
  window.STWikiEditors.set(containerId, ed);
  return ed;
}