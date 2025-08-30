// Enhanced Markdown Editor for STWiki
// Provides live preview, formatting helpers, and enhanced editing experience

let editorInstances = new Map();

// Helper function for scoped DOM lookups
function getScoped(container, selector) {
    return container ? container.querySelector(selector) : null;
}

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

        // Detect format from container-scoped badge
        const formatBadge = getScoped(editorContainer, '[data-role="format-badge"], .badge.bg-primary');
        const currentFormat = formatBadge ? formatBadge.textContent.toLowerCase() : 'markdown';
        
        // Create editor instance with container-scoped element lookups
        const instance = {
            id: editorId,
            container: editorContainer,
            textarea: textarea,
            previewElement: getScoped(editorContainer, '[data-role="preview"], #preview-content'),
            wordCountElement: getScoped(editorContainer, '[data-role="word-count"], #word-count'),
            charCountElement: getScoped(editorContainer, '[data-role="char-count"], #char-count'),
            statusElement: getScoped(editorContainer, '[data-role="status-text"], #status-text'),
            hiddenField: getScoped(editorContainer, 'textarea[name="Body"], input[name="Body"], [data-role="body"], #body-textarea'),
            lastContent: '',
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
        // Immediate sync with instance-specific form field on every input
        syncWithFormTextarea(instance, textarea.value);
        
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
        
        // Sync with instance-specific form field
        syncWithFormTextarea(instance, content);
        
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
                // Simple, direct highlighting without complex error handling
                const codeBlocks = previewElement.querySelectorAll('pre code[class*="language-"]');
                codeBlocks.forEach(block => {
                    try {
                        // Simple highlighting - let Prism.js handle it
                        Prism.highlightElement(block);
                    } catch (blockError) {
                        // Silently continue with other blocks
                        console.debug('Skipping problematic code block');
                    }
                });
            } catch (prismError) {
                // Silently skip highlighting if there are issues
                console.debug('Prism.js highlighting skipped');
            }
        }
    } catch (error) {
        console.error('Error updating preview:', error);
        if (previewElement) {
            previewElement.innerHTML = '<em class="text-danger">Preview error</em>';
        }
    }
}

// Sync editor content with instance-specific hidden form field
function syncWithFormTextarea(instance, content) {
    try {
        const hiddenField = instance.hiddenField;
        if (hiddenField) {
            hiddenField.value = content || '';
            // Also trigger change event to ensure form validation updates
            hiddenField.dispatchEvent(new Event('input', { bubbles: true }));
            hiddenField.dispatchEvent(new Event('change', { bubbles: true }));
            console.log(`🔄 [${instance.id}] Synced content to form field:`, content?.length || 0, 'characters');
        } else {
            console.warn(`⚠️ [${instance.id}] Hidden form field not found for syncing`);
        }
    } catch (error) {
        console.error(`❌ [${instance.id}] Error syncing with form field:`, error);
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
        
        // Preserve the code exactly as written, only escaping HTML entities
        const escapedCode = code
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
        
        const codeBlockHtml = `<pre><code${languageClass}>${escapedCode}</code></pre>`;
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

// Instance-specific function for toolbar buttons
window.insertMarkdownFor = function(editorId, before, after) {
    const instance = editorInstances.get(editorId);
    if (!instance) {
        console.error('Editor instance not found for insertMarkdownFor:', editorId);
        return;
    }

    wrapSelection(instance.textarea, before, after);
    updatePreview(instance);
    updateStats(instance);
};

// Legacy global function for toolbar buttons (fallback using focus)
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

    console.warn(`Using legacy insertMarkdown - prefer insertMarkdownFor('${activeId}', ...) for multi-editor setups`);
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

// Update specific editor format
window.updateEditorFormat = function(editorId, newFormat) {
    const instance = editorInstances.get(editorId);
    if (!instance) {
        console.error('Editor instance not found for updateEditorFormat:', editorId);
        return;
    }
    
    instance.format = newFormat.toLowerCase();
    // Trigger a preview update to reflect the new format
    updatePreview(instance);
    console.log(`Updated editor ${editorId} to format:`, newFormat);
};

// Legacy global format update (for compatibility)
window.updateAllEditorsFormat = function(newFormat) {
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

// Collaboration features
let remoteCursors = new Map();
let isApplyingRemoteOperation = false;

// Set editor content programmatically (for collaboration sync)
window.setEnhancedEditorContent = function(editorId, content) {
    const instance = editorInstances.get(editorId);
    if (!instance) {
        console.error('Editor instance not found:', editorId);
        return;
    }
    
    isApplyingRemoteOperation = true;
    instance.textarea.value = content;
    updatePreview(instance);
    updateStats(instance);
    isApplyingRemoteOperation = false;
    
    console.log('✅ Set editor content for collaboration sync');
};

// Apply insert operation from remote user
window.applyInsertOperation = function(editorId, position, text) {
    const instance = editorInstances.get(editorId);
    if (!instance) {
        console.error('Editor instance not found:', editorId);
        return;
    }
    
    isApplyingRemoteOperation = true;
    
    const textarea = instance.textarea;
    const currentValue = textarea.value;
    const newValue = currentValue.slice(0, position) + text + currentValue.slice(position);
    
    textarea.value = newValue;
    updatePreview(instance);
    updateStats(instance);
    
    isApplyingRemoteOperation = false;
    
    console.log(`✅ Applied remote insert: "${text}" at position ${position}`);
};

// Apply delete operation from remote user
window.applyDeleteOperation = function(editorId, position, length) {
    const instance = editorInstances.get(editorId);
    if (!instance) {
        console.error('Editor instance not found:', editorId);
        return;
    }
    
    isApplyingRemoteOperation = true;
    
    const textarea = instance.textarea;
    const currentValue = textarea.value;
    const newValue = currentValue.slice(0, position) + currentValue.slice(position + length);
    
    textarea.value = newValue;
    updatePreview(instance);
    updateStats(instance);
    
    isApplyingRemoteOperation = false;
    
    console.log(`✅ Applied remote delete: ${length} characters at position ${position}`);
};

// Apply replace operation from remote user
window.applyReplaceOperation = function(editorId, selectionStart, selectionEnd, newText) {
    const instance = editorInstances.get(editorId);
    if (!instance) {
        console.error('Editor instance not found:', editorId);
        return;
    }
    
    isApplyingRemoteOperation = true;
    
    const textarea = instance.textarea;
    const currentValue = textarea.value;
    
    console.log(`🔄 Applying replace operation:`);
    console.log(`  Current: "${currentValue}"`);
    console.log(`  Replace range ${selectionStart}-${selectionEnd} ("${currentValue.slice(selectionStart, selectionEnd)}") with "${newText}"`);
    
    const newValue = currentValue.slice(0, selectionStart) + newText + currentValue.slice(selectionEnd);
    
    console.log(`  Result: "${newValue}"`);
    
    textarea.value = newValue;
    updatePreview(instance);
    updateStats(instance);
    
    isApplyingRemoteOperation = false;
    
    console.log(`✅ Applied remote replace: ${selectionStart}-${selectionEnd} -> "${newText}"`);
};

// Get current cursor position
window.getEditorCursorPosition = function(editorId) {
    const instance = editorInstances.get(editorId);
    if (!instance) {
        console.error('Editor instance not found:', editorId);
        return [0, 0];
    }
    
    const textarea = instance.textarea;
    return [textarea.selectionStart, textarea.selectionEnd];
};

// Update remote cursor visualization for specific editor
window.updateRemoteCursor = function(editorId, userId, start, end, userColor, displayName) {
    try {
        console.log(`🎯 [${editorId}] Remote cursor from ${displayName} (${userId}): ${start}-${end}`);
        
        const instance = editorInstances.get(editorId);
        if (!instance) {
            console.error('Editor instance not found for cursor update:', editorId);
            return;
        }
        
        remoteCursors.set(`${editorId}_${userId}`, { 
            editorId,
            userId,
            start, 
            end, 
            userColor, 
            displayName,
            lastUpdate: Date.now() 
        });
        
        renderRemoteCursor(editorId, userId, start, end, userColor, displayName);
        
    } catch (error) {
        console.error(`Failed to update remote cursor for editor ${editorId}:`, error);
    }
};

// Remove remote cursor when user leaves specific editor
window.removeRemoteCursor = function(editorId, userId) {
    try {
        const key = `${editorId}_${userId}`;
        remoteCursors.delete(key);
        console.log(`🎯 [${editorId}] Removed remote cursor for ${userId}`);
        
        // Remove visual cursor indicators from the specific editor
        const overlay = document.getElementById(`remote-cursor-overlay-${editorId}`);
        if (overlay) {
            const cursorElement = overlay.querySelector(`[data-user-id="${userId}"]`);
            if (cursorElement) {
                cursorElement.remove();
            }
        }
        
    } catch (error) {
        console.error(`Failed to remove remote cursor for editor ${editorId}:`, error);
    }
};

// Legacy function - remove from all editors
window.removeRemoteCursorFromAll = function(userId) {
    try {
        // Remove from all editor instances
        for (const [editorId] of editorInstances) {
            const key = `${editorId}_${userId}`;
            remoteCursors.delete(key);
            
            const overlay = document.getElementById(`remote-cursor-overlay-${editorId}`);
            if (overlay) {
                const cursorElement = overlay.querySelector(`[data-user-id="${userId}"]`);
                if (cursorElement) {
                    cursorElement.remove();
                }
            }
        }
        
        console.log(`🎯 Removed remote cursor for ${userId} from all editors`);
        
    } catch (error) {
        console.error('Failed to remove remote cursor from all editors:', error);
    }
};

// Render remote cursor at specific position
function renderRemoteCursor(editorId, userId, start, end, userColor, displayName) {
    try {
        const overlay = document.getElementById(`remote-cursor-overlay-${editorId}`);
        const textarea = document.getElementById(editorId);
        
        if (!overlay) {
            console.error('❌ Cursor overlay not found:', `remote-cursor-overlay-${editorId}`);
            return;
        }
        
        if (!textarea) {
            console.error('❌ Textarea not found:', editorId);
            return;
        }
        
        console.log(`🎯 Rendering cursor for ${displayName} (${userId}) at ${start}-${end} with color ${userColor}`);
        
        // Remove existing cursor for this user
        const existingCursor = overlay.querySelector(`[data-user-id="${userId}"]`);
        if (existingCursor) {
            existingCursor.remove();
        }
        
        // Calculate cursor position
        const position = getTextPosition(textarea, start);
        if (!position) {
            console.error('❌ Could not calculate cursor position for', start);
            return;
        }
        
        console.log(`📍 Calculated position:`, position);
        
        // Create cursor element
        const cursorElement = document.createElement('div');
        cursorElement.className = 'remote-cursor';
        cursorElement.setAttribute('data-user-id', userId);
        cursorElement.style.color = userColor;
        cursorElement.style.left = position.left + 'px';
        cursorElement.style.top = position.top + 'px';
        cursorElement.style.height = position.height + 'px';
        
        // Create cursor line
        const cursorLine = document.createElement('div');
        cursorLine.className = 'remote-cursor-line';
        cursorElement.appendChild(cursorLine);
        
        // Create cursor label
        const cursorLabel = document.createElement('div');
        cursorLabel.className = 'remote-cursor-label';
        cursorLabel.textContent = displayName;
        cursorElement.appendChild(cursorLabel);
        
        // Handle selection highlighting
        if (start !== end) {
            renderRemoteSelection(overlay, textarea, start, end, userColor, userId);
        }
        
        // Add cursor to overlay with fade-in effect
        overlay.appendChild(cursorElement);
        
        // Trigger fade-in animation with movement class
        setTimeout(() => {
            cursorElement.classList.add('visible', 'moving');
        }, 10); // Small delay to ensure DOM is ready
        
        // Show label temporarily for new cursors and indicate active state
        cursorElement.classList.add('active');
        setTimeout(() => {
            cursorElement.classList.remove('active');
        }, 2000);
        
        setTimeout(() => {
            cursorElement.classList.remove('moving');
        }, 300); // Remove moving class after fade animation
        
        console.log(`✅ Rendered cursor for ${displayName} at position ${start}-${end}`, cursorElement);
        console.log(`📍 Cursor element style:`, {
            left: cursorElement.style.left,
            top: cursorElement.style.top,
            height: cursorElement.style.height,
            color: cursorElement.style.color
        });
        
    } catch (error) {
        console.error('Failed to render remote cursor:', error);
    }
}

// Render remote text selection (SIMPLIFIED)
function renderRemoteSelection(overlay, textarea, start, end, userColor, userId) {
    try {
        console.log(`🎨 Rendering selection for ${userId}: ${start}-${end}`);
        
        // Remove existing selection for this user
        const existingSelection = overlay.querySelector(`[data-selection-user-id="${userId}"]`);
        if (existingSelection) {
            existingSelection.remove();
        }
        
        if (start === end) {
            console.log(`⚠️ No selection - start equals end (${start})`);
            return; // No selection
        }
        
        const startPos = getTextPosition(textarea, start);
        const endPos = getTextPosition(textarea, end);
        
        if (!startPos || !endPos) {
            console.log(`❌ Could not calculate selection positions`);
            return;
        }
        
        console.log(`📍 Selection positions:`, { startPos, endPos });
        
        // Create selection highlight
        const selectionElement = document.createElement('div');
        selectionElement.className = 'remote-selection';
        selectionElement.setAttribute('data-selection-user-id', userId);
        selectionElement.style.color = userColor;
        
        // Always render as single-line for now (simplified)
        const highlight = document.createElement('div');
        highlight.className = 'remote-selection-highlight';
        
        // Calculate selection width
        let selectionWidth;
        if (startPos.top === endPos.top) {
            // Same line - use actual width difference
            selectionWidth = Math.abs(endPos.left - startPos.left);
        } else {
            // Multi-line - for now, just show from start to end of line
            const textareaWidth = textarea.clientWidth - (parseInt(window.getComputedStyle(textarea).paddingLeft) || 0) * 2;
            selectionWidth = Math.max(50, textareaWidth - startPos.left);
        }
        
        highlight.style.left = Math.min(startPos.left, endPos.left) + 'px';
        highlight.style.top = startPos.top + 'px';
        highlight.style.width = selectionWidth + 'px';
        highlight.style.height = startPos.height + 'px';
        highlight.style.backgroundColor = userColor;
        highlight.style.opacity = '0.7';
        
        selectionElement.appendChild(highlight);
        overlay.appendChild(selectionElement);
        
        // Trigger fade-in animation with movement class for selections
        setTimeout(() => {
            selectionElement.classList.add('visible', 'moving');
        }, 10);
        
        setTimeout(() => {
            selectionElement.classList.remove('moving');
        }, 300); // Remove moving class after fade animation
        
        console.log(`✅ Rendered selection highlight:`, {
            left: highlight.style.left,
            top: highlight.style.top,
            width: highlight.style.width,
            height: highlight.style.height
        });
        
    } catch (error) {
        console.error('❌ Failed to render remote selection:', error);
    }
}

// Calculate text position in pixels relative to textarea (SIMPLIFIED)
function getTextPosition(textarea, textIndex) {
    try {
        const computedStyle = window.getComputedStyle(textarea);
        const lineHeight = parseInt(computedStyle.lineHeight) || 20;
        const fontSize = parseInt(computedStyle.fontSize) || 14;
        const charWidth = fontSize * 0.6; // Approximate character width for monospace
        
        // Get textarea padding
        const paddingLeft = parseInt(computedStyle.paddingLeft) || 0;
        const paddingTop = parseInt(computedStyle.paddingTop) || 0;
        
        // Simple line-by-line calculation
        const textUpToCursor = textarea.value.substring(0, textIndex);
        const lines = textUpToCursor.split('\n');
        const currentLine = lines.length - 1;
        const charInLine = lines[lines.length - 1].length;
        
        // Cursor should appear at the LEFT edge of the current character position
        // If user is at position 5, cursor should be BEFORE character 5 (after character 4)
        const position = {
            left: paddingLeft + (charInLine * charWidth) - 2, // Adjust left by 2px to account for cursor width
            top: paddingTop + (currentLine * lineHeight),
            height: lineHeight
        };
        
        console.log(`📍 Simple position calc for index ${textIndex}:`, position);
        console.log(`📍 Line ${currentLine}, Char ${charInLine} in line`);
        
        // Bounds checking - ensure cursor stays within textarea
        const maxTop = textarea.clientHeight - lineHeight;
        const maxLeft = textarea.clientWidth - 10;
        
        position.top = Math.min(Math.max(position.top, 0), maxTop);
        position.left = Math.min(Math.max(position.left, 0), maxLeft);
        
        return position;
        
    } catch (error) {
        console.error('❌ Failed to calculate text position:', error);
        
        // Ultimate fallback - just put cursor at top-left
        return {
            left: 10,
            top: 10,
            height: 20
        };
    }
}

// Check if Blazor is connected (for collaboration readiness)
window.checkBlazorConnection = function() {
    try {
        // Check if Blazor SignalR connection is working
        return typeof Blazor !== 'undefined' && Blazor.defaultReconnectionHandler;
    } catch (error) {
        return false;
    }
};

// Mark content as committed (for collaboration coordination)
window.markContentAsCommitted = function() {
    console.log('✅ Content marked as committed - collaboration aware');
    // This could be extended to notify other collaborators
};

// Enhanced event listener setup for collaboration
function setupCollaborativeEventListeners(instance) {
    const { textarea, componentRef } = instance;
    
    if (!componentRef) return;
    
    let lastContent = textarea.value;
    let lastSelectionStart = textarea.selectionStart;
    let lastSelectionEnd = textarea.selectionEnd;
    
    // Detect text changes for operational transform
    textarea.addEventListener('input', function(e) {
        if (isApplyingRemoteOperation) {
            return; // Don't send operations for remote changes
        }
        
        const currentContent = textarea.value;
        const currentSelectionStart = textarea.selectionStart;
        const currentSelectionEnd = textarea.selectionEnd;
        
        // Detect the type of operation that occurred
        const operation = detectOperation(lastContent, currentContent, lastSelectionStart, lastSelectionEnd, currentSelectionStart);
        
        // Send operation to Blazor component for collaboration
        if (operation && componentRef) {
            try {
                console.log(`📝 [${instance.id}] Detected ${operation.type} operation:`, operation);
                
                if (operation.type === 'replace') {
                    componentRef.invokeMethodAsync('OnTextReplace', 
                        operation.selectionStart, 
                        operation.selectionEnd, 
                        operation.newText);
                } else {
                    componentRef.invokeMethodAsync('OnTextChange', 
                        currentContent, 
                        operation.position, 
                        operation.type, 
                        operation.text);
                }
            } catch (error) {
                console.error(`[${instance.id}] Failed to send text change to Blazor:`, error);
            }
        }
        
        lastContent = currentContent;
        lastSelectionStart = currentSelectionStart;
        lastSelectionEnd = currentSelectionEnd;
    });
    
    // Track cursor position changes
    textarea.addEventListener('selectionchange', function() {
        if (isApplyingRemoteOperation) return;
        
        // Cursor position changes are handled by the timer in the Blazor component
    });
}

// Enhanced operation detection for collaborative editing
function detectOperation(oldText, newText, oldSelectionStart, oldSelectionEnd, newCursorPos) {
    const oldLength = oldText.length;
    const newLength = newText.length;
    
    // Check if this was a selection replacement
    if (oldSelectionStart !== oldSelectionEnd) {
        // User had text selected - check if it was replaced
        const selectedText = oldText.slice(oldSelectionStart, oldSelectionEnd);
        const beforeSelection = oldText.slice(0, oldSelectionStart);
        const afterSelection = oldText.slice(oldSelectionEnd);
        
        // Check if the selection was replaced with new text
        if (newText.startsWith(beforeSelection) && newText.endsWith(afterSelection)) {
            // Calculate what was inserted to replace the selection
            const expectedLength = beforeSelection.length + afterSelection.length;
            const insertedLength = newText.length - expectedLength;
            const insertedText = newText.slice(oldSelectionStart, oldSelectionStart + insertedLength);
            
            console.log(`🔍 Selection replacement detected:`);
            console.log(`  Old: "${oldText}" (selected "${selectedText}" at ${oldSelectionStart}-${oldSelectionEnd})`);
            console.log(`  New: "${newText}" (inserted "${insertedText}")`);
            
            return {
                type: 'replace',
                selectionStart: oldSelectionStart,
                selectionEnd: oldSelectionEnd,
                newText: insertedText,
                oldText: selectedText
            };
        }
    }
    
    // Fallback to simple insert/delete detection
    if (newLength > oldLength) {
        // Text was inserted
        const insertPos = findInsertPosition(oldText, newText);
        const insertedText = newText.slice(insertPos, insertPos + (newLength - oldLength));
        return {
            type: 'insert',
            position: insertPos,
            text: insertedText
        };
    } else if (newLength < oldLength) {
        // Text was deleted
        const deletePos = findDeletePosition(oldText, newText);
        const deletedText = oldText.slice(deletePos, deletePos + (oldLength - newLength));
        return {
            type: 'delete',
            position: deletePos,
            text: deletedText
        };
    }
    
    return null; // No change detected
}

// Helper function to find where text was inserted
function findInsertPosition(oldText, newText) {
    let i = 0;
    while (i < oldText.length && i < newText.length && oldText[i] === newText[i]) {
        i++;
    }
    return i;
}

// Helper function to find where text was deleted
function findDeletePosition(oldText, newText) {
    let i = 0;
    while (i < oldText.length && i < newText.length && oldText[i] === newText[i]) {
        i++;
    }
    return i;
}

// Update the initEnhancedEditor function to include collaborative features
const originalInitEnhancedEditor = window.initEnhancedEditor;
window.initEnhancedEditor = function(editorId, initialContent, componentRef) {
    const success = originalInitEnhancedEditor(editorId, initialContent, componentRef);
    
    if (success && componentRef) {
        const instance = editorInstances.get(editorId);
        if (instance) {
            // Setup collaborative event listeners
            setupCollaborativeEventListeners(instance);
            console.log('✅ Collaborative features initialized for editor:', editorId);
        }
    }
    
    return success;
};

console.log('Enhanced editor JavaScript with collaboration features loaded');