// Enhanced Markdown Editor for STWiki
// Provides live preview, formatting helpers, and enhanced editing experience

let editorInstances = new Map();

// Initialize the enhanced editor (markdown or HTML)
window.initEnhancedEditor = function(editorId, initialContent, componentRef) {
    try {
        console.log('Initializing enhanced editor:', editorId);
        
        const textarea = document.getElementById(editorId);
        if (!textarea) {
            console.error('Editor textarea not found:', editorId);
            return false;
        }

        // Find the editor container and store the component reference
        const editorContainer = textarea.closest('.editor-container');
        if (editorContainer && componentRef) {
            editorContainer._blazorComponentRef = componentRef;
            console.log('Stored component reference on editor container');
        }

        // Detect the current format from the page
        const formatBadge = document.querySelector('.badge.bg-primary');
        const currentFormat = formatBadge ? formatBadge.textContent.toLowerCase() : 'markdown';
        
        // Create editor instance with safe element lookups
        const instance = {
            textarea: textarea,
            previewElement: document.getElementById('preview-content') || null,
            wordCountElement: document.getElementById('word-count') || null,
            charCountElement: document.getElementById('char-count') || null,
            statusElement: document.getElementById('status-text') || null,
            lastContent: initialContent || '',
            updateTimeout: null,
            componentRef: componentRef,
            format: currentFormat
        };

        editorInstances.set(editorId, instance);

        // Set initial content
        textarea.value = initialContent || '';

        // Setup event listeners
        setupEventListeners(instance);
        
        // Setup keyboard shortcuts  
        setupKeyboardShorts(instance);
        
        // Initial preview update
        updatePreview(instance);
        updateStats(instance);

        console.log('Enhanced editor initialized successfully');
        return true;
    } catch (error) {
        console.error('Failed to initialize enhanced editor:', error);
        return false;
    }
};

// Setup event listeners for the editor
function setupEventListeners(instance) {
    const { textarea } = instance;

    // Live preview updates with debouncing
    textarea.addEventListener('input', function() {
        clearTimeout(instance.updateTimeout);
        instance.updateTimeout = setTimeout(() => {
            updatePreview(instance);
            updateStats(instance);
        }, 300);
    });

    // Tab key for indentation
    textarea.addEventListener('keydown', function(e) {
        if (e.key === 'Tab') {
            e.preventDefault();
            const start = textarea.selectionStart;
            const end = textarea.selectionEnd;
            
            textarea.value = textarea.value.substring(0, start) + '    ' + textarea.value.substring(end);
            textarea.selectionStart = textarea.selectionEnd = start + 4;
            
            updatePreview(instance);
            updateStats(instance);
        }
    });

    // Auto-pair brackets and quotes
    textarea.addEventListener('keydown', function(e) {
        const pairs = {
            '(': ')',
            '[': ']',
            '{': '}',
            '"': '"',
            "'": "'"
        };

        if (pairs[e.key]) {
            const start = textarea.selectionStart;
            const end = textarea.selectionEnd;
            
            if (start === end) { // No selection
                e.preventDefault();
                const before = textarea.value.substring(0, start);
                const after = textarea.value.substring(end);
                
                textarea.value = before + e.key + pairs[e.key] + after;
                textarea.selectionStart = textarea.selectionEnd = start + 1;
                
                updatePreview(instance);
                updateStats(instance);
            }
        }
    });
}

// Setup keyboard shortcuts
function setupKeyboardShorts(instance) {
    const { textarea } = instance;

    textarea.addEventListener('keydown', function(e) {
        if (e.ctrlKey || e.metaKey) {
            switch(e.key.toLowerCase()) {
                case 'b':
                    e.preventDefault();
                    wrapSelection(textarea, '**', '**');
                    updatePreview(instance);
                    updateStats(instance);
                    break;
                case 'i':
                    e.preventDefault();
                    wrapSelection(textarea, '*', '*');
                    updatePreview(instance);
                    updateStats(instance);
                    break;
                case 'k':
                    e.preventDefault();
                    insertLink(textarea);
                    updatePreview(instance);
                    updateStats(instance);
                    break;
            }
        }
    });
}

// Update the preview (markdown or HTML)
function updatePreview(instance) {
    const { textarea, previewElement, format } = instance;
    if (!previewElement) return;

    try {
        const content = textarea.value;
        if (content === instance.lastContent) return;
        
        instance.lastContent = content;
        
        // Sync with traditional form textarea
        syncWithFormTextarea(content);
        
        if (!content.trim()) {
            previewElement.innerHTML = '<em class="text-muted">Preview will appear here...</em>';
            return;
        }

        let html;
        if (format === 'html') {
            // For HTML content, just display it directly (with basic sanitization)
            html = content;
        } else {
            // For markdown content, convert to HTML
            html = markdownToHtml(content);
        }
        
        previewElement.innerHTML = html;
        
        // Apply Prism.js syntax highlighting to the new content
        if (typeof Prism !== 'undefined' && Prism.highlightElement) {
            try {
                // Wait a bit to ensure all Prism plugins are loaded
                setTimeout(() => {
                    try {
                        // Re-highlight all code blocks in the preview
                        const codeBlocks = previewElement.querySelectorAll('pre code[class*="language-"]');
                        codeBlocks.forEach(block => {
                            try {
                                // Clear any existing highlighting
                                block.innerHTML = block.textContent;
                                
                                // Ensure the language class is valid
                                const classList = Array.from(block.classList);
                                const languageClass = classList.find(cls => cls.startsWith('language-'));
                                
                                if (languageClass) {
                                    const language = languageClass.replace('language-', '');
                                    // Only highlight if the language is supported
                                    if (Prism.languages && (Prism.languages[language] || language === 'none')) {
                                        Prism.highlightElement(block);
                                    } else {
                                        // Remove unsupported language class and add generic styling
                                        block.classList.remove(languageClass);
                                        block.classList.add('language-none');
                                        console.warn(`Prism.js: Language '${language}' not supported, using generic styling`);
                                    }
                                }
                            } catch (blockError) {
                                console.warn('Prism.js: Failed to highlight individual code block:', blockError);
                                // Continue with other blocks
                            }
                        });
                    } catch (innerError) {
                        console.warn('Prism.js: Failed to process code blocks:', innerError);
                    }
                }, 50);
            } catch (prismError) {
                console.warn('Prism.js: Syntax highlighting initialization failed:', prismError);
                // Don't stop the preview update, just skip highlighting
            }
        }
    } catch (error) {
        console.error('Error updating preview:', error);
        if (previewElement) {
            previewElement.innerHTML = '<em class="text-danger">Preview error</em>';
        }
    }
}

// Sync editor content with hidden form textarea
function syncWithFormTextarea(content) {
    try {
        const bodyTextarea = document.getElementById('body-textarea');
        if (bodyTextarea) {
            bodyTextarea.value = content;
            console.log('🔄 Synced content to form textarea');
        }
    } catch (error) {
        console.error('Error syncing with form textarea:', error);
    }
}

// Update word and character counts
function updateStats(instance) {
    try {
        const { textarea, wordCountElement, charCountElement } = instance;
        const content = textarea.value;

        if (wordCountElement) {
            const wordCount = content.trim() ? content.trim().split(/\s+/).length : 0;
            wordCountElement.textContent = `${wordCount} words`;
        }

        if (charCountElement) {
            charCountElement.textContent = `${content.length} characters`;
        }
    } catch (error) {
        console.error('Error updating stats:', error);
    }
}

// Basic markdown to HTML conversion
function markdownToHtml(markdown) {
    let html = markdown;
    
    // Store code blocks temporarily to protect them from processing
    const codeBlocks = [];
    const codeBlockPlaceholder = '___CODE_BLOCK_PLACEHOLDER___';
    
    // Extract and protect code blocks first
    html = html.replace(/```(\w+)?\n?([\s\S]*?)```/g, function(match, lang, code) {
        const language = lang ? lang.toLowerCase() : '';
        const languageClass = language ? ` class="language-${language}"` : '';
        const preClass = language ? ` class="line-numbers"` : '';
        
        // Preserve the code exactly as written, only escaping HTML entities
        const escapedCode = code
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
        
        const codeBlockHtml = `<pre${preClass}><code${languageClass}>${escapedCode}</code></pre>`;
        codeBlocks.push(codeBlockHtml);
        return codeBlockPlaceholder + (codeBlocks.length - 1);
    });
    
    // Extract and protect inline code
    const inlineCodeBlocks = [];
    const inlineCodePlaceholder = '___INLINE_CODE_PLACEHOLDER___';
    
    html = html.replace(/`([^`]*)`/g, function(match, code) {
        const escapedCode = code
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
        
        const inlineCodeHtml = `<code class="language-none">${escapedCode}</code>`;
        inlineCodeBlocks.push(inlineCodeHtml);
        return inlineCodePlaceholder + (inlineCodeBlocks.length - 1);
    });

    // Now process the rest of the markdown (escaping HTML for non-code content)
    html = html.replace(/&/g, '&amp;')
               .replace(/</g, '&lt;')
               .replace(/>/g, '&gt;');

    // Headers
    html = html.replace(/^### (.*$)/gim, '<h3>$1</h3>');
    html = html.replace(/^## (.*$)/gim, '<h2>$1</h2>');
    html = html.replace(/^# (.*$)/gim, '<h1>$1</h1>');

    // Bold and italic
    html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
    html = html.replace(/\*(.*?)\*/g, '<em>$1</em>');

    // Links
    html = html.replace(/\[([^\]]*)\]\(([^)]*)\)/g, '<a href="$2">$1</a>');

    // Blockquotes
    html = html.replace(/^> (.*$)/gim, '<blockquote class="blockquote">$1</blockquote>');

    // Lists
    html = html.replace(/^\* (.*$)/gim, '<li>$1</li>');
    html = html.replace(/^\- (.*$)/gim, '<li>$1</li>');
    html = html.replace(/^\d+\. (.*$)/gim, '<li>$1</li>');

    // Wrap consecutive list items in ul/ol
    html = html.replace(/(<li>.*<\/li>)\n(<li>.*<\/li>)/g, '<ul>$1$2</ul>');
    html = html.replace(/<\/ul>\n<ul>/g, '');

    // Line breaks (but not inside code blocks)
    html = html.replace(/\n\n/g, '</p><p>');
    html = html.replace(/\n/g, '<br>');

    // Wrap in paragraphs if needed
    if (html && !html.startsWith('<')) {
        html = '<p>' + html + '</p>';
    }
    
    // Restore inline code blocks first
    for (let i = 0; i < inlineCodeBlocks.length; i++) {
        html = html.replace(inlineCodePlaceholder + i, inlineCodeBlocks[i]);
    }

    // Restore code blocks last
    for (let i = 0; i < codeBlocks.length; i++) {
        html = html.replace(codeBlockPlaceholder + i, codeBlocks[i]);
    }

    return html;
}

// Insert markdown formatting around selection
function wrapSelection(textarea, before, after) {
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selectedText = textarea.value.substring(start, end);
    const beforeText = textarea.value.substring(0, start);
    const afterText = textarea.value.substring(end);

    if (selectedText) {
        // Wrap selected text
        textarea.value = beforeText + before + selectedText + after + afterText;
        textarea.selectionStart = start + before.length;
        textarea.selectionEnd = start + before.length + selectedText.length;
    } else {
        // Insert formatting markers at cursor
        textarea.value = beforeText + before + after + afterText;
        textarea.selectionStart = textarea.selectionEnd = start + before.length;
    }
    
    textarea.focus();
}

// Insert link formatting
function insertLink(textarea) {
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selectedText = textarea.value.substring(start, end);
    const linkText = selectedText || 'Link text';
    const linkUrl = 'url';
    
    const beforeText = textarea.value.substring(0, start);
    const afterText = textarea.value.substring(end);
    
    textarea.value = beforeText + `[${linkText}](${linkUrl})` + afterText;
    
    if (selectedText) {
        // Select the URL part
        const urlStart = start + linkText.length + 3;
        textarea.selectionStart = urlStart;
        textarea.selectionEnd = urlStart + linkUrl.length;
    } else {
        // Select the link text
        textarea.selectionStart = start + 1;
        textarea.selectionEnd = start + 1 + linkText.length;
    }
    
    textarea.focus();
}

// Global function for toolbar buttons
window.insertMarkdown = function(before, after) {
    // Find the active editor (most recent one or focused one)
    let activeInstance = null;
    let activeId = null;

    for (const [id, instance] of editorInstances) {
        if (document.activeElement === instance.textarea) {
            activeInstance = instance;
            activeId = id;
            break;
        }
    }

    // If no focused editor, use the first one
    if (!activeInstance && editorInstances.size > 0) {
        activeId = editorInstances.keys().next().value;
        activeInstance = editorInstances.get(activeId);
    }

    if (!activeInstance) {
        console.error('No active editor found');
        return;
    }

    wrapSelection(activeInstance.textarea, before, after);
    updatePreview(activeInstance);
    updateStats(activeInstance);
};

// Get editor content
window.getEnhancedEditorContent = function(editorId) {
    const instance = editorInstances.get(editorId);
    if (!instance) {
        console.error('Editor instance not found:', editorId);
        return '';
    }
    return instance.textarea.value;
};

// Show status message
window.showEditorStatus = function(message) {
    // Update status text in all active editors
    for (const instance of editorInstances.values()) {
        if (instance.statusElement) {
            instance.statusElement.textContent = message;
            
            // Clear the message after 3 seconds
            setTimeout(() => {
                if (instance.statusElement) {
                    instance.statusElement.textContent = 'Ready';
                }
            }, 3000);
        }
    }
    
    console.log('🔥 DETAILED EDITOR RESPONSE:', message);
    
    // If it's a response message, try to parse and show more details
    if (message.includes('Response:')) {
        try {
            const responsePart = message.split('Response: ')[1];
            if (responsePart) {
                console.log('🔥 RAW RESPONSE CONTENT:', responsePart);
                try {
                    const parsedResponse = JSON.parse(responsePart);
                    console.log('🔥 PARSED JSON RESPONSE:', parsedResponse);
                } catch (e) {
                    console.log('🔥 RESPONSE IS NOT JSON, RAW TEXT:', responsePart);
                }
            }
        } catch (e) {
            console.log('🔥 ERROR PARSING RESPONSE:', e);
        }
    }
};

// Update editor format when format is switched
window.updateEditorFormat = function(newFormat) {
    for (const instance of editorInstances.values()) {
        instance.format = newFormat.toLowerCase();
        // Trigger a preview update to reflect the new format
        updatePreview(instance);
    }
    console.log('Updated all editor instances to format:', newFormat);
};

// Clean up editor instance
window.destroyEnhancedEditor = function(editorId) {
    const instance = editorInstances.get(editorId);
    if (instance) {
        clearTimeout(instance.updateTimeout);
        editorInstances.delete(editorId);
        console.log('Enhanced editor destroyed:', editorId);
    }
};

// Auto-save functionality integration
window.addEventListener('beforeunload', function() {
    // Clean up all editor instances
    for (const [id] of editorInstances) {
        destroyEnhancedEditor(id);
    }
});

console.log('Enhanced editor JavaScript loaded');