// dom-traps.js
// DOM operation debugging traps - runs after Blazor initialization

(function(){
    console.log('🔧 Installing DOM operation traps...');
    
    function hexify(s){
        if (typeof s !== "string") return s;
        return [...s].map(c => `U+${c.codePointAt(0).toString(16).toUpperCase().padStart(4,'0')}(${c})`).join(' ');
    }
    function logFail(kind, obj){
        const pretty = {};
        for (const [k,v] of Object.entries(obj)) pretty[k] = typeof v === 'string' ? `${v}  [${hexify(v)}]` : v;
        console.error(`[trap] ${kind} failed`, pretty);
        
        // Also log Blazor state when errors occur
        if (window.blazorDiagnostics) {
            console.error('[trap] Blazor state at failure:', window.blazorDiagnostics.logBlazorState());
        }
    }

    const _sa  = Element.prototype.setAttribute;
    Element.prototype.setAttribute = function(name, value){
        try { return _sa.call(this, name, value); }
        catch(e){ logFail('setAttribute', {el:this, name, value, e}); throw e; }
    };

    const _sans = Element.prototype.setAttributeNS;
    Element.prototype.setAttributeNS = function(ns, name, value){
        try { return _sans.call(this, ns, name, value); }
        catch(e){ logFail('setAttributeNS', {el:this, ns, name, value, e}); throw e; }
    };

    const _ca = Document.prototype.createAttribute;
    Document.prototype.createAttribute = function(name){
        try { return _ca.call(this, name); }
        catch(e){ logFail('createAttribute', {name, e}); throw e; }
    };

    const _ce = Document.prototype.createElement;
    Document.prototype.createElement = function(name){
        try { return _ce.call(this, name); }
        catch(e){ logFail('createElement', {name, e}); throw e; }
    };

    const _cc = Document.prototype.createComment;
    Document.prototype.createComment = function(data){
        try { return _cc.call(this, data); }
        catch(e){ logFail('createComment', {data, e}); throw e; }
    };

    const _add = DOMTokenList.prototype.add;
    DOMTokenList.prototype.add = function(...tokens){
        try { return _add.apply(this, tokens); }
        catch(e){ logFail('classList.add', {el:this, tokens: tokens.map(t => `${t}  [${hexify(t)}]`), e}); throw e; }
    };

    // Optional: catch innerHTML too (invalid markup can also throw)
    const _inner = Object.getOwnPropertyDescriptor(Element.prototype, "innerHTML");
    Object.defineProperty(Element.prototype, "innerHTML", {
        set(html){ try { return _inner.set.call(this, html); }
            catch(e){ logFail('innerHTML', {el:this, html, e}); throw e; } },
        get(){ return _inner.get.call(this); }
    });

    window.addEventListener('error', ev => console.error('[trap] window.error', ev.error || ev));
    window.addEventListener('unhandledrejection', ev => console.error('[trap] unhandledrejection', ev.reason || ev));
    
    console.log('✅ DOM operation traps installed');
})();