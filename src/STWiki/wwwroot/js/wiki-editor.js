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
    this.mediaCache = new Map(); // Cache for media API responses
    this.pageCache = new Map(); // Cache for page lookup responses
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
    
    // Process wiki-specific syntax
    html = await this.processWikiSyntax(html);
    
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
  
  async processWikiSyntax(html) {
    // Process templates first (to avoid conflicts with other syntax)
    html = this.parseTemplates(html);
    
    // Process page links (now async due to API calls)
    html = await this.parseWikiLinks(html);
    
    // Process media links (async due to API calls)
    html = await this.parseMediaLinks(html);
    
    return html;
  }
  
  async parseWikiLinks(html) {
    // Match [[page-slug]] and [[page-slug|Display Text]] patterns - exclude media links
    const wikiLinkRegex = /\[\[(?!media:)([^|\]]+)(?:\|([^\]]+))?\]\]/g;
    const matches = [...html.matchAll(wikiLinkRegex)];
    
    if (matches.length === 0) return html;
    
    console.log(`[PreviewPlugin] Found ${matches.length} wiki links to process`);
    
    // Collect all unique slugs for batch lookup
    const slugsToLookup = [...new Set(matches.map(match => match[1].trim()))];
    
    // Get page info for all slugs at once
    const pageInfo = await this.lookupPages(slugsToLookup);
    
    // Replace each match with appropriate link
    for (const match of matches) {
      const [fullMatch, slug, displayText] = match;
      const cleanSlug = slug.trim();
      const linkHtml = this.renderWikiLink(cleanSlug, displayText, pageInfo[cleanSlug]);
      html = html.replace(fullMatch, linkHtml);
    }
    
    return html;
  }
  
  async lookupPages(slugs) {
    const uncachedSlugs = slugs.filter(slug => !this.pageCache.has(slug));
    
    // Fetch uncached pages
    if (uncachedSlugs.length > 0) {
      try {
        console.log(`[PreviewPlugin] Looking up ${uncachedSlugs.length} pages:`, uncachedSlugs);
        
        const response = await fetch(`/api/wiki/lookup?slugs=${uncachedSlugs.map(s => encodeURIComponent(s)).join(',')}`);
        if (response.ok) {
          const data = await response.json();
          console.log(`[PreviewPlugin] Page lookup response:`, data);
          
          // Cache the results
          uncachedSlugs.forEach(slug => {
            const title = data.pages[slug] || null;
            this.pageCache.set(slug, title);
          });
        } else {
          console.error(`[PreviewPlugin] Page lookup failed: ${response.status}`);
          // Cache null results to avoid repeated API calls
          uncachedSlugs.forEach(slug => this.pageCache.set(slug, null));
        }
      } catch (error) {
        console.error(`[PreviewPlugin] Error looking up pages:`, error);
        uncachedSlugs.forEach(slug => this.pageCache.set(slug, null));
      }
    }
    
    // Return all requested page info from cache
    const result = {};
    slugs.forEach(slug => {
      result[slug] = this.pageCache.get(slug) || null;
    });
    return result;
  }
  
  renderWikiLink(slug, displayText, pageTitle) {
    const exists = pageTitle !== null;
    const text = displayText ? displayText.trim() : (pageTitle || slug);
    const className = exists ? 'wiki-link wiki-link-exists' : 'wiki-link wiki-link-unknown';
    const title = exists ? `Navigate to: ${pageTitle}` : `Page does not exist: ${slug}`;
    
    return `<a href="/wiki/${encodeURIComponent(slug)}" class="${className}" title="${esc(title)}">${esc(text)}</a>`;
  }
  
  async parseMediaLinks(html) {
    // Match [[media:filename|params]] patterns - case insensitive
    const mediaRegex = /\[\[media:([^|\]]+)(?:\|([^\]]+))?\]\]/gi;
    const matches = [...html.matchAll(mediaRegex)];
    
    console.log(`[PreviewPlugin] Found ${matches.length} media links to process`);
    
    for (const match of matches) {
      const [fullMatch, filename, params] = match;
      console.log(`[PreviewPlugin] Processing media: "${filename}" with params: "${params}"`);
      const mediaHtml = await this.renderMediaLink(filename.trim(), params);
      html = html.replace(fullMatch, mediaHtml);
    }
    
    return html;
  }
  
  async renderMediaLink(filename, params) {
    try {
      console.log(`[PreviewPlugin] Rendering media link for: ${filename}`);
      
      // Try to get media info from cache first
      let mediaInfo = this.mediaCache.get(filename);
      
      if (!mediaInfo) {
        console.log(`[PreviewPlugin] Media not in cache, fetching from API: ${filename}`);
        
        // Search for media by filename
        const apiUrl = `/api/media?search=${encodeURIComponent(filename)}&pageSize=20`;
        console.log(`[PreviewPlugin] API URL: ${apiUrl}`);
        
        const response = await fetch(apiUrl);
        console.log(`[PreviewPlugin] API response status: ${response.status}`);
        
        if (response.ok) {
          const data = await response.json();
          console.log(`[PreviewPlugin] API response data:`, data);
          
          if (data.items && data.items.length > 0) {
            // Look for exact match first, then partial match
            mediaInfo = data.items.find(item => item.fileName === filename) || 
                       data.items.find(item => item.fileName.includes(filename)) ||
                       data.items[0];
            
            if (mediaInfo) {
              console.log(`[PreviewPlugin] Found media:`, mediaInfo);
              this.mediaCache.set(filename, mediaInfo);
            }
          }
        } else {
          console.error(`[PreviewPlugin] API error: ${response.status} ${response.statusText}`);
        }
      } else {
        console.log(`[PreviewPlugin] Using cached media info for: ${filename}`);
      }
      
      if (!mediaInfo) {
        console.warn(`[PreviewPlugin] No media found for: ${filename}`);
        return `<span class="media-link-error" title="Media file not found: ${filename}">📄 ${esc(filename)} (not found)</span>`;
      }
      
      // Parse parameters
      const paramObj = this.parseMediaParams(params);
      console.log(`[PreviewPlugin] Media parameters:`, paramObj);
      
      // Render based on content type
      if (mediaInfo.contentType.startsWith('image/')) {
        return this.renderImageMedia(mediaInfo, paramObj);
      } else {
        return this.renderFileMedia(mediaInfo, paramObj);
      }
    } catch (error) {
      console.error(`[PreviewPlugin] Failed to render media link for ${filename}:`, error);
      return `<span class="media-link-error" title="Error loading media: ${error.message}">📄 ${esc(filename)} (error)</span>`;
    }
  }
  
  parseMediaParams(paramString) {
    const params = {};
    if (!paramString) return params;
    
    const pairs = paramString.split('|');
    for (const pair of pairs) {
      const [key, value] = pair.split('=').map(s => s.trim());
      if (key) {
        params[key] = value || true;
      }
    }
    return params;
  }
  
  renderImageMedia(mediaInfo, params) {
    const { id, fileName, thumbnailUrl, width, height } = mediaInfo;
    const imageUrl = thumbnailUrl || `/api/media/${id}`;
    
    // Handle size parameters
    let sizeStyle = '';
    if (params.size) {
      sizeStyle = `max-width: ${params.size}px;`;
    } else if (params.width) {
      sizeStyle = `width: ${params.width}px;`;
    }
    
    // Handle alignment
    let alignClass = '';
    if (params.align === 'left') alignClass = 'float-start me-3';
    else if (params.align === 'right') alignClass = 'float-end ms-3';
    else if (params.align === 'center') alignClass = 'd-block mx-auto';
    
    const alt = params.alt || params.caption || fileName;
    const caption = params.caption ? `<figcaption class="figure-caption">${esc(params.caption)}</figcaption>` : '';
    
    if (caption) {
      return `<figure class="figure ${alignClass}">
        <img src="${imageUrl}" alt="${esc(alt)}" class="figure-img img-fluid rounded" style="${sizeStyle}" 
             title="Click to view full size" onclick="window.open('/api/media/${id}', '_blank')">
        ${caption}
      </figure>`;
    } else {
      return `<img src="${imageUrl}" alt="${esc(alt)}" class="img-fluid rounded ${alignClass}" 
               style="${sizeStyle} cursor: pointer;" title="Click to view full size"
               onclick="window.open('/api/media/${id}', '_blank')">`;
    }
  }
  
  renderFileMedia(mediaInfo, params) {
    const { id, fileName, contentType, fileSize } = mediaInfo;
    const sizeStr = this.formatFileSize(fileSize);
    const icon = this.getFileIcon(contentType);
    
    return `<a href="/api/media/${id}" target="_blank" class="file-link d-inline-flex align-items-center text-decoration-none">
      <i class="bi bi-${icon} me-2"></i>
      <span class="me-2">${esc(fileName)}</span>
      <small class="text-muted">(${sizeStr})</small>
    </a>`;
  }
  
  parseTemplates(html) {
    // Match {{template-name param=value}} patterns
    return html.replace(/\{\{([^}]+)\}\}/g, (match, content) => {
      const parts = content.trim().split(/\s+/);
      const templateName = parts[0];
      const params = parts.slice(1).map(p => {
        const [key, value] = p.split('=');
        return value ? `${key}: ${value}` : key;
      }).join(', ');
      
      const icon = this.getTemplateIcon(templateName);
      const description = this.getTemplateDescription(templateName);
      
      return `<div class="template-placeholder border rounded p-3 mb-3 bg-light">
        <div class="d-flex align-items-center">
          <i class="bi bi-${icon} text-primary me-2 fs-5"></i>
          <div>
            <strong>${esc(templateName)}</strong>
            ${params ? `<small class="text-muted ms-2">(${esc(params)})</small>` : ''}
            <div class="small text-muted">${description}</div>
          </div>
        </div>
      </div>`;
    });
  }
  
  getTemplateIcon(templateName) {
    const icons = {
      'recent-pages': 'clock-history',
      'child-pages': 'folder',
      'popular-pages': 'star',
      'top-contributors': 'people',
      'wiki-statistics': 'bar-chart',
      'recently-edited': 'pencil-square'
    };
    return icons[templateName] || 'puzzle';
  }
  
  getTemplateDescription(templateName) {
    const descriptions = {
      'recent-pages': 'Shows recently updated pages',
      'child-pages': 'Lists sub-pages of current page',
      'popular-pages': 'Displays most viewed pages',
      'top-contributors': 'Shows top wiki contributors',
      'wiki-statistics': 'Displays wiki metrics and statistics',
      'recently-edited': 'Shows recent page changes'
    };
    return descriptions[templateName] || 'Dynamic template content will appear here';
  }
  
  formatFileSize(bytes) {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }
  
  getFileIcon(contentType) {
    if (contentType.includes('pdf')) return 'file-earmark-pdf';
    if (contentType.includes('word') || contentType.includes('document')) return 'file-earmark-word';
    if (contentType.includes('excel') || contentType.includes('spreadsheet')) return 'file-earmark-excel';
    if (contentType.includes('powerpoint') || contentType.includes('presentation')) return 'file-earmark-ppt';
    if (contentType.startsWith('text/')) return 'file-earmark-text';
    if (contentType.startsWith('image/')) return 'file-earmark-image';
    return 'file-earmark';
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
    ov.style.cssText = 'position:absolute;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,.3);color:#fff;font-weight:bold;font-size:18px;border:3px dashed #fff;border-radius:8px;z-index:1000;';
    this.editor.container.style.position = this.editor.container.style.position || 'relative';
    this.editor.container.appendChild(ov);
    this.overlay = ov;

    const sig = { signal: this.ac.signal };
    let dragCounter = 0; // Track enter/leave pairs to prevent flicker
    
    const over = (e) => {
      e.preventDefault();
      e.stopPropagation();
      console.log('🎯 [DragDrop] Drag over detected');
    };
    
    const enter = (e) => {
      e.preventDefault();
      e.stopPropagation();
      dragCounter++;
      if (dragCounter === 1) {
        console.log('📁 [DragDrop] Showing drag overlay');
        ov.classList.remove('d-none');
      }
    };
    
    const leave = (e) => {
      e.preventDefault();
      e.stopPropagation();
      dragCounter--;
      if (dragCounter === 0) {
        console.log('🚫 [DragDrop] Hiding drag overlay');
        ov.classList.add('d-none');
      }
    };
    
    const drop = async (e) => {
      e.preventDefault();
      e.stopPropagation();
      dragCounter = 0;
      ov.classList.add('d-none');
      
      console.log('📤 [DragDrop] Drop detected, files:', e.dataTransfer.files.length);
      
      const imageFiles = [...e.dataTransfer.files].filter(f=>f.type.startsWith('image/'));
      if (imageFiles.length === 0) {
        console.warn('⚠️ [DragDrop] No image files found in drop');
        return;
      }
      
      console.log('🖼️ [DragDrop] Opening upload modal with', imageFiles.length, 'image files');
      
      // Check if modal integration is available
      if (typeof window.openUploadModalWithFiles !== 'function') {
        console.error('❌ [DragDrop] Upload modal integration not available, falling back to direct upload');
        // Fallback to direct upload for single file
        if (imageFiles.length === 1) {
          try {
            const r = await this.upload(imageFiles[0]);
            const ta = this.editor.textarea;
            const ins = `[[media:${r.fileName}|display=thumb]]`;
            ta.setRangeText(ins, ta.selectionStart, ta.selectionEnd, 'end');
            this.editor.onInput(new Event('input'));
            console.log('✅ [DragDrop] Image uploaded and inserted:', r.fileName);
          } catch (error) {
            console.error('❌ [DragDrop] Upload failed:', error);
          }
        }
        return;
      }
      
      // Open modal with callback to insert media links
      window.openUploadModalWithFiles(imageFiles, (uploadedFiles) => {
        console.log('📝 [DragDrop] Upload completed, inserting', uploadedFiles.length, 'media links');
        const ta = this.editor.textarea;
        const cursorPos = ta.selectionStart;
        
        let insertText = '';
        uploadedFiles.forEach((file, index) => {
          const altText = file.altText || '';
          const mediaLink = altText 
            ? `[[media:${file.fileName}|${altText}]]`
            : `[[media:${file.fileName}|display=thumb]]`;
          
          insertText += (index > 0 ? '\n' : '') + mediaLink;
        });
        
        ta.setRangeText(insertText, cursorPos, cursorPos, 'end');
        this.editor.onInput(new Event('input'));
        
        console.log('✅ [DragDrop] Inserted media links into editor');
      });
    };
    
    this.editor.container.addEventListener('dragover', over, sig);
    this.editor.container.addEventListener('dragenter', enter, sig);
    this.editor.container.addEventListener('dragleave', leave, sig);
    this.editor.container.addEventListener('drop', drop, sig);
    
    console.log('🎯 [DragDrop] Plugin initialized for container:', this.editor.container.id);
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