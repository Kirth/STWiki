// wiki-editor-blazor-bridge.js
// Bridge between the new modular WikiEditor and the existing Blazor EditorSimple component
// This replaces the old editor-enhanced.js functionality with the new plugin architecture

import { 
    mountById, 
    PreviewPlugin, 
    StatsPlugin, 
    FormattingPlugin, 
    AutocompletePlugin, 
    DragDropPlugin,
    CollabPlugin 
} from './wiki-editor.js';

// Global registry for editors (for Blazor interop)
window.wikiEditors = new Map();

// Initialize a new modular wiki editor
window.initWikiEditor = function(containerId, initialContent, format, dotNetRef) {
    try {
        console.log(`🚀 [V2] Initializing modular editor for container: ${containerId}`);
        
        const container = document.getElementById(containerId);
        if (!container) {
            console.error(`❌ [V2] Container not found: ${containerId}`);
            return false;
        }

        // Enhanced PreviewPlugin with wiki macro resolution
        class WikiPreviewPlugin extends PreviewPlugin {
            async resolveWikiMacros(html) {
                // Resolve [[Page|Text]] and [[media:*]] with existing wiki rules
                return html.replace(/\[\[([^\]]+)\]\]/g, (_, body) => {
                    const [raw, label] = body.split('|').map(s => s.trim());
                    if (/^media:/i.test(raw)) {
                        const path = raw.slice(6);
                        const cap = label ? `<figcaption class="small text-muted">${this.esc(label)}</figcaption>` : '';
                        return `<figure><img src="/media/${encodeURIComponent(path)}" class="img-fluid" loading="lazy">${cap}</figure>`;
                    }
                    const name = raw;
                    return `<a class="wiki-link" href="/${encodeURIComponent(name)}">${this.esc(label || name)}</a>`;
                });
            }
            
            esc(s) { return s.replace(/[&<>"']/g, m => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m])); }
        }

        // Enhanced AutocompletePlugin with STWiki API endpoints
        class STWikiAutocompletePlugin extends AutocompletePlugin {
            constructor(editor) {
                super(editor, {
                    fetchPages: async () => {
                        try {
                            const response = await fetch('/api/pages/suggestions');
                            const items = await response.json();
                            return items.map(x => ({ 
                                value: x.name || x.title, 
                                title: x.title || x.name, 
                                sub: x.path || '' 
                            }));
                        } catch (e) {
                            console.warn('Failed to fetch page suggestions:', e);
                            return [];
                        }
                    },
                    fetchMedia: async () => {
                        try {
                            const response = await fetch('/api/media/suggestions');
                            const items = await response.json();
                            return items.map(x => ({ 
                                value: x.filename, 
                                title: x.filename, 
                                sub: x.size ? `${x.size}B` : '' 
                            }));
                        } catch (e) {
                            console.warn('Failed to fetch media suggestions:', e);
                            return [];
                        }
                    }
                });
            }
        }

        // Enhanced DragDropPlugin - now uses modal integration instead of direct upload
        class STWikiDragDropPlugin extends DragDropPlugin {
            constructor(editor) {
                super(editor, {
                    // Fallback upload function (only used if modal integration fails)
                    upload: async (file) => {
                        const formData = new FormData();
                        formData.append('file', file);
                        
                        const response = await fetch('/api/media/upload', {
                            method: 'POST',
                            body: formData
                        });
                        
                        if (!response.ok) {
                            throw new Error(`Upload failed: ${response.status}`);
                        }
                        
                        const result = await response.json();
                        return { fileName: result.fileName || file.name };
                    }
                });
            }
        }

        // Enhanced CollabPlugin that bridges to Blazor SignalR
        class STWikiCollabPlugin extends CollabPlugin {
            constructor(editor, dotNetRef) {
                super(editor, {
                    send: (operation) => {
                        // Bridge to existing Blazor collaboration system
                        console.log(`📡 [V2] Sending operation via Blazor:`, operation);
                        
                        // Convert to format expected by existing system
                        if (dotNetRef) {
                            try {
                                switch(operation.type) {
                                    case 'insert':
                                        dotNetRef.invokeMethodAsync('OnTextChange', this.editor.value, operation.position, 'insert', operation.content || operation.text || '');
                                        break;
                                    case 'delete':
                                        dotNetRef.invokeMethodAsync('OnTextChange', this.editor.value, operation.position, 'delete', '');
                                        break;
                                    case 'replace':
                                        dotNetRef.invokeMethodAsync('OnTextReplace', operation.selectionStart || operation.start, operation.selectionEnd || operation.end, operation.content || operation.text || '');
                                        break;
                                    case 'set':
                                        // Treat as replace operation for full content updates
                                        dotNetRef.invokeMethodAsync('OnTextReplace', 0, this.lastValue?.length || 0, operation.value || this.editor.value);
                                        break;
                                }
                            } catch (e) {
                                console.error('Failed to send operation to Blazor:', e);
                            }
                        }
                    }
                });
                this.dotNetRef = dotNetRef;
                this.lastValue = editor.value;
            }
            
            onInput(e, v) {
                if (this.editor.state.isRemote) return;
                
                // For now, send as full content replacement 
                // TODO: Implement proper diff detection for granular operations
                const cursorPos = this.editor.textarea.selectionStart;
                this.send({ 
                    type: 'set', 
                    value: v, 
                    position: cursorPos 
                });
                this.lastValue = v;
            }
        }

        // Initialize editor with all plugins
        const editor = mountById(containerId, {
            initialContent,
            format,
            plugins: [
                WikiPreviewPlugin,
                StatsPlugin,
                FormattingPlugin,
                STWikiAutocompletePlugin,
                STWikiDragDropPlugin,
                class extends STWikiCollabPlugin {
                    constructor(editor) { super(editor, dotNetRef); }
                }
            ]
        });

        // Store editor reference for later cleanup
        window.wikiEditors.set(containerId, {
            editor,
            dotNetRef
        });

        console.log(`✅ [V2] Modular editor initialized successfully for ${containerId}`);
        return true;

    } catch (error) {
        console.error(`❌ [V2] Failed to initialize editor for ${containerId}:`, error);
        return false;
    }
};

// Get editor content (replacement for getEnhancedEditorContent)
window.getWikiEditorContent = function(containerId) {
    const editorData = window.wikiEditors.get(containerId);
    if (editorData?.editor) {
        return editorData.editor.value;
    }
    console.warn(`⚠️ [V2] Editor not found for ${containerId}`);
    return '';
};

// Set editor content (replacement for setEnhancedEditorContent)
window.setWikiEditorContent = function(containerId, content) {
    const editorData = window.wikiEditors.get(containerId);
    if (editorData?.editor) {
        // Mark as remote to prevent collaboration echo
        editorData.editor.state.isRemote = true;
        editorData.editor.value = content;
        editorData.editor.state.isRemote = false;
        return true;
    }
    console.warn(`⚠️ [V2] Editor not found for ${containerId}`);
    return false;
};

// Destroy editor (replacement for destroyEnhancedEditor)
window.destroyWikiEditor = function(containerId) {
    const editorData = window.wikiEditors.get(containerId);
    if (editorData?.editor) {
        editorData.editor.destroy();
        window.wikiEditors.delete(containerId);
        console.log(`🗑️ [V2] Editor destroyed: ${containerId}`);
        return true;
    }
    return false;
};

// Legacy compatibility functions (for gradual migration)
window.getEnhancedEditorContent = function(editorId) {
    return window.getWikiEditorContent(`editor-container-${editorId}`);
};

window.setEnhancedEditorContent = function(editorId, content) {
    const containerId = `editor-container-${editorId}`;
    const editorData = window.wikiEditors.get(containerId);
    if (editorData?.editor) {
        const textarea = editorData.editor.textarea;
        
        // Save cursor position before applying content change
        const wasFocused = document.activeElement === textarea;
        const savedSelectionStart = textarea.selectionStart;
        const savedSelectionEnd = textarea.selectionEnd;
        
        // Mark as remote to prevent collaboration echo
        editorData.editor.state.isRemote = true;
        editorData.editor.value = content;
        
        // Restore cursor position only if this editor was focused
        if (wasFocused && content.length > 0) {
            const maxPos = content.length;
            const newStart = Math.min(savedSelectionStart, maxPos);
            const newEnd = Math.min(savedSelectionEnd, maxPos);
            textarea.setSelectionRange(newStart, newEnd);
        }
        
        editorData.editor.state.isRemote = false;
        
        console.log(`📝 [V2] Set editor content (${content.length} chars), cursor preserved at ${savedSelectionStart}-${savedSelectionEnd}`);
        return true;
    }
    return false;
};

window.destroyEnhancedEditor = function(editorId) {
    return window.destroyWikiEditor(`editor-container-${editorId}`);
};

// Show editor status (compatibility)
window.showEditorStatus = function(message) {
    console.log(`📋 [V2] Status: ${message}`);
    
    // Update all status elements
    document.querySelectorAll('[data-role="status"]').forEach(el => {
        el.textContent = message;
    });
};

// Other compatibility functions needed by Blazor component
window.insertMarkdown = function(before, after) {
    console.log(`📝 [V2] Insert markdown: ${before}...${after}`);
    // TODO: Apply to currently focused editor
};

window.markContentAsCommitted = function() {
    console.log(`✅ [V2] Content marked as committed`);
};

window.applyInsertOperation = function(editorId, position, content) {
    const containerId = `editor-container-${editorId}`;
    const editorData = window.wikiEditors.get(containerId);
    if (editorData?.editor) {
        const textarea = editorData.editor.textarea;
        
        // Save cursor position before applying remote operation
        const wasFocused = document.activeElement === textarea;
        const savedSelectionStart = textarea.selectionStart;
        const savedSelectionEnd = textarea.selectionEnd;
        
        editorData.editor.state.isRemote = true;
        
        // Apply insert operation
        textarea.setRangeText(content, position, position, 'preserve');
        
        // Restore cursor position only if this editor was focused
        if (wasFocused) {
            let newStart = savedSelectionStart;
            let newEnd = savedSelectionEnd;
            
            // If cursor was at or after insert position, adjust for inserted content
            if (savedSelectionStart >= position) {
                newStart = savedSelectionStart + content.length;
                newEnd = savedSelectionEnd + content.length;
            }
            
            textarea.setSelectionRange(newStart, newEnd);
        }
        
        editorData.editor.onInput(new Event('input'));
        editorData.editor.state.isRemote = false;
        
        console.log(`➕ [V2] Applied insert operation: position ${position}, content "${content}", cursor preserved at ${savedSelectionStart}-${savedSelectionEnd}`);
    }
};

window.applyDeleteOperation = function(editorId, position, length) {
    const containerId = `editor-container-${editorId}`;
    const editorData = window.wikiEditors.get(containerId);
    if (editorData?.editor) {
        const textarea = editorData.editor.textarea;
        
        // Save cursor position before applying remote operation
        const wasFocused = document.activeElement === textarea;
        const savedSelectionStart = textarea.selectionStart;
        const savedSelectionEnd = textarea.selectionEnd;
        
        editorData.editor.state.isRemote = true;
        
        // Apply delete operation
        textarea.setRangeText('', position, position + length, 'preserve');
        
        // Restore cursor position only if this editor was focused
        if (wasFocused) {
            let newStart = savedSelectionStart;
            let newEnd = savedSelectionEnd;
            
            // If cursor was after the deleted range, adjust for deleted content
            if (savedSelectionStart >= position + length) {
                newStart = savedSelectionStart - length;
                newEnd = savedSelectionEnd - length;
            }
            // If cursor was within the deleted range, place at delete position
            else if (savedSelectionStart >= position) {
                newStart = newEnd = position;
            }
            // If cursor was before delete position, no adjustment needed
            
            textarea.setSelectionRange(newStart, newEnd);
        }
        
        editorData.editor.onInput(new Event('input'));
        editorData.editor.state.isRemote = false;
        
        console.log(`➖ [V2] Applied delete operation: position ${position}, length ${length}, cursor preserved at ${savedSelectionStart}-${savedSelectionEnd}`);
    }
};

window.applyReplaceOperation = function(editorId, start, end, content) {
    const containerId = `editor-container-${editorId}`;
    const editorData = window.wikiEditors.get(containerId);
    if (editorData?.editor) {
        const textarea = editorData.editor.textarea;
        
        // Save cursor position before applying remote operation
        const wasFocused = document.activeElement === textarea;
        const savedSelectionStart = textarea.selectionStart;
        const savedSelectionEnd = textarea.selectionEnd;
        
        editorData.editor.state.isRemote = true;
        
        // Apply the operation without moving cursor to 'end'
        textarea.setRangeText(content, start, end, 'preserve');
        
        // Restore cursor position only if this editor was focused
        if (wasFocused) {
            // Adjust cursor position based on the operation
            const deltaLength = content.length - (end - start);
            let newStart = savedSelectionStart;
            let newEnd = savedSelectionEnd;
            
            // If cursor was after the operation, adjust for length change
            if (savedSelectionStart >= end) {
                newStart = savedSelectionStart + deltaLength;
                newEnd = savedSelectionEnd + deltaLength;
            }
            // If cursor was within the operation range, place at end of new content
            else if (savedSelectionStart >= start) {
                newStart = newEnd = start + content.length;
            }
            // If cursor was before operation, no adjustment needed
            
            textarea.setSelectionRange(newStart, newEnd);
        }
        
        editorData.editor.onInput(new Event('input'));
        editorData.editor.state.isRemote = false;
        
        console.log(`🔄 [V2] Applied replace operation: ${start}-${end} with "${content}", cursor preserved at ${savedSelectionStart}-${savedSelectionEnd}`);
    }
};

window.getEditorCursorPosition = function(editorId) {
    const containerId = `editor-container-${editorId}`;
    const editorData = window.wikiEditors.get(containerId);
    if (editorData?.editor) {
        const ta = editorData.editor.textarea;
        return [ta.selectionStart, ta.selectionEnd];
    }
    return [0, 0];
};

// Misc compatibility functions
window.checkBlazorConnection = function() {
    return true; // Always return true for now
};

// Set up immediate auto-save on content changes
window.setupImmediateAutoSave = function(editorId) {
    console.log(`🚀 Setting up immediate auto-save for editor: ${editorId}`);
    
    const containerId = `editor-container-${editorId}`;
    const editorData = window.wikiEditors.get(containerId);
    
    if (!editorData?.editor?.textarea) {
        console.warn(`⚠️ Could not find editor data for ${containerId}`);
        return;
    }
    
    const textarea = editorData.editor.textarea;
    
    // Set up input listener for immediate auto-save
    textarea.addEventListener('input', function() {
        // Find the Blazor component reference
        const editorContainer = document.getElementById(containerId);
        if (editorContainer && editorContainer._blazorComponentRef) {
            try {
                // Trigger immediate auto-save via Blazor
                editorContainer._blazorComponentRef.invokeMethodAsync('TriggerImmediateAutoSave');
            } catch (error) {
                console.warn('Could not trigger immediate auto-save:', error);
            }
        }
    });
    
    console.log(`✅ Immediate auto-save set up for ${editorId}`);
};

window.updateRemoteCursor = function(editorId, userId, start, end, color, displayName) {
    console.log(`👆 [V2] Update remote cursor: ${displayName} at ${start}-${end}`);
    // TODO: Implement remote cursor display
};

window.removeRemoteCursor = function(editorId, userId) {
    console.log(`👆 [V2] Remove remote cursor: ${userId}`);
    // TODO: Implement remote cursor removal
};

console.log('✅ [V2] WikiEditor Blazor bridge loaded');