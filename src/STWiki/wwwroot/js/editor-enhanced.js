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
        const editorContainer = textarea.closest('.editor-container') || textarea.parentElement;
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
            format: currentFormat,
            isApplyingRemoteOperation: false  // Per-instance collaboration state
        };

        editorInstances.set(editorId, instance);

        // Set initial content - but first check what's already there
        const existingContent = textarea.value || '';
        const parameterContent = initialContent || '';
        
        console.log(`🔍 Editor initialization content comparison for ${editorId}:`);
        console.log(`📝 Existing textarea content length: ${existingContent.length}`);
        console.log(`📋 Parameter content length: ${parameterContent.length}`);
        
        if (existingContent.length > 100) {
            console.log(`📝 Existing content preview: "${existingContent.substring(0, 100)}..."`);
        } else if (existingContent.length > 0) {
            console.log(`📝 Existing content full: "${existingContent}"`);
        }
        
        if (parameterContent.length > 100) {
            console.log(`📋 Parameter content preview: "${parameterContent.substring(0, 100)}..."`);
        } else if (parameterContent.length > 0) {
            console.log(`📋 Parameter content full: "${parameterContent}"`);
        }
        
        // Use the longer/more complete content, but prefer parameter if they're equal
        let finalContent = parameterContent;
        if (existingContent.length > parameterContent.length) {
            console.log(`⚠️ Existing content is longer than parameter - using existing content`);
            finalContent = existingContent;
        } else if (existingContent.length === parameterContent.length && existingContent !== parameterContent) {
            console.log(`⚠️ Contents have same length but differ - using parameter content`);
        }
        
        textarea.value = finalContent;
        console.log(`✅ Final content set to ${finalContent.length} characters`);

        // Setup event listeners
        setupEventListeners(instance);
        
        // Setup keyboard shortcuts  
        setupKeyboardShorts(instance);
        
        // Setup drag and drop
        setupDragAndDrop(instance);
        
        // Initial preview update
        updatePreview(instance).catch(console.error);
        updateStats(instance);

        console.log('✅ Enhanced editor initialized successfully with drag-and-drop support');
        // Set original content for change tracking
        setOriginalContent(initialContent);

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
    textarea.addEventListener('input', function(e) {
        // Check for auto-completion triggers
        handleAutoCompletion(instance, e);
        
        // Immediate sync with instance-specific form field on every input
    syncWithFormTextarea(instance, textarea.value);

    // Check for changes to update unsaved state
    checkForUnsavedChanges();

    clearTimeout(instance.updateTimeout);
    instance.updateTimeout = setTimeout(async () => {
      await updatePreview(instance).catch(console.error);
      updateStats(instance);
    }, 300);
  });

  // Tab key for indentation
  textarea.addEventListener('keydown', function (e) {
    if (e.key === 'Tab') {
      e.preventDefault();
      const start = textarea.selectionStart;
      const end = textarea.selectionEnd;

      textarea.value = textarea.value.substring(0, start) + '    ' + textarea.value.substring(end);
      textarea.selectionStart = textarea.selectionEnd = start + 4;

      updatePreview(instance).catch(console.error);
      updateStats(instance);
    }
  });

  // Auto-pair brackets and quotes (with wiki template awareness)
  textarea.addEventListener('keydown', function (e) {
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
        const before = textarea.value.substring(0, start);
        const after = textarea.value.substring(end);

        // Enhanced handling for brackets with wiki syntax awareness
        if (e.key === '[') {
          // Check if we just typed '[' and the previous char was also '['
          const justTypedDoubleBracket = before.endsWith('[');

          if (justTypedDoubleBracket) {
            // User is typing '[[' - complete it with ']]' and position cursor inside
            e.preventDefault();
            textarea.value = before + '[]]' + after;
            textarea.selectionStart = textarea.selectionEnd = start + 1;
            console.log('🔧 Auto-completed [[ with ]]');
            updatePreview(instance).catch(console.error);
            updateStats(instance);
            return;
          }

          // Don't auto-pair single bracket if we're already inside wiki template syntax
          const precedingText = before.slice(-10);
          const followingText = after.slice(0, 10);

          if (precedingText.includes('[[') && !precedingText.includes(']]')) {
            console.log('🔧 Skipping single bracket auto-pair inside wiki template');
            return; // Let the keypress happen normally
          }
        }

        if (e.key === ']') {
          // Smart closing bracket handling
          if (after.startsWith(']')) {
            // Skip over existing closing bracket instead of adding another
            e.preventDefault();
            textarea.selectionStart = textarea.selectionEnd = start + 1;
            console.log('🔧 Jumped over existing closing bracket');
            return;
          }

          // Check if this would complete a wiki template
          const openBrackets = (before.match(/\[/g) || []).length;
          const closeBrackets = (before.match(/\]/g) || []).length;

          if (openBrackets - closeBrackets === 2) {
            // This would complete a [[ ]] pair, so add the second ]
            e.preventDefault();
            textarea.value = before + ']]' + after;
            textarea.selectionStart = textarea.selectionEnd = start + 2;
            console.log('🔧 Auto-completed ]] for wiki template');
            updatePreview(instance).catch(console.error);
            updateStats(instance);
            return;
          }
        }

        // Enhanced curly brace handling for templates
        if (e.key === '{') {
          const justTypedDoubleBrace = before.endsWith('{');

          if (justTypedDoubleBrace) {
            // User is typing '{{' - complete it with '}}' and position cursor inside
            e.preventDefault();
            textarea.value = before + '{}}' + after;
            textarea.selectionStart = textarea.selectionEnd = start + 1;
            console.log('🔧 Auto-completed {{ with }}');
            updatePreview(instance).catch(console.error);
            updateStats(instance);
            return;
          }
        }

        if (e.key === '}') {
          // Smart closing brace handling
          if (after.startsWith('}')) {
            // Skip over existing closing brace instead of adding another
            e.preventDefault();
            textarea.selectionStart = textarea.selectionEnd = start + 1;
            console.log('🔧 Jumped over existing closing brace');
            return;
          }

          // Check if this would complete a template
          const openBraces = (before.match(/\{/g) || []).length;
          const closeBraces = (before.match(/\}/g) || []).length;

          if (openBraces - closeBraces === 2) {
            // This would complete a {{ }} pair, so add the second }
            e.preventDefault();
            textarea.value = before + '}}' + after;
            textarea.selectionStart = textarea.selectionEnd = start + 2;
            console.log('🔧 Auto-completed }} for template');
            updatePreview(instance).catch(console.error);
            updateStats(instance);
            return;
          }
        }

        e.preventDefault();
        textarea.value = before + e.key + pairs[e.key] + after;
        textarea.selectionStart = textarea.selectionEnd = start + 1;

        updatePreview(instance).catch(console.error);
        updateStats(instance);
      }
    }
  });

  // Handle auto-completion dropdown keyboard navigation
  textarea.addEventListener('keydown', function (e) {
    const dropdown = instance.autoCompleteDropdown;
    if (!dropdown || dropdown.classList.contains('d-none')) return;

    const items = dropdown.querySelectorAll('.autocomplete-item');
    const selectedIndex = instance.selectedAutoCompleteIndex || 0;

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        instance.selectedAutoCompleteIndex = Math.min(selectedIndex + 1, items.length - 1);
        updateAutoCompleteSelection(instance);
        break;
      case 'ArrowUp':
        e.preventDefault();
        instance.selectedAutoCompleteIndex = Math.max(selectedIndex - 1, 0);
        updateAutoCompleteSelection(instance);
        break;
      case 'Enter':
      case 'Tab':
        e.preventDefault();
        if (items[instance.selectedAutoCompleteIndex]) {
          selectAutoCompleteItem(instance, items[instance.selectedAutoCompleteIndex]);
        }
        break;
      case 'Escape':
        e.preventDefault();
        hideAutoComplete(instance);
        break;
    }
  });

  // Hide auto-complete when clicking outside
  document.addEventListener('click', function (e) {
    if (instance.autoCompleteDropdown && !instance.container.contains(e.target)) {
      hideAutoComplete(instance);
    }
  });
}

// Handle auto-completion logic
function handleAutoCompletion(instance, e) {
  const { textarea } = instance;
  const cursorPos = textarea.selectionStart;
  const textBeforeCursor = textarea.value.substring(0, cursorPos);

  // Check if we just typed '[['
  if (textBeforeCursor.endsWith('[[')) {
    showPageAutoComplete(instance, cursorPos);
  }
  // Check if we just typed 'media:' after '[['
  else if (textBeforeCursor.match(/\[\[media:$/i)) {
    showMediaAutoComplete(instance, cursorPos);
  }
  // If we're typing and have an open dropdown, filter results
  else if (instance.autoCompleteDropdown && !instance.autoCompleteDropdown.classList.contains('d-none')) {
    const macroStart = findMacroStart(textBeforeCursor);
    if (macroStart !== -1) {
      const macroContent = textBeforeCursor.substring(macroStart + 2); // Skip '[['
      filterAutoCompleteResults(instance, macroContent);
    } else {
      hideAutoComplete(instance);
    }
  }
  // Hide dropdown if we moved outside macro context
  else if (instance.autoCompleteDropdown && !instance.autoCompleteDropdown.classList.contains('d-none')) {
    const macroStart = findMacroStart(textBeforeCursor);
    if (macroStart === -1) {
      hideAutoComplete(instance);
    }
  }
}

// Find the start position of the current macro
function findMacroStart(text) {
  let pos = text.length - 1;
  let bracketCount = 0;

  while (pos >= 0) {
    if (text.substring(pos, pos + 2) === ']]') {
      return -1; // Already closed macro
    }
    if (text.substring(pos, pos + 2) === '[[') {
      return pos;
    }
    pos--;
  }
  return -1;
}

// Show page auto-completion dropdown
async function showPageAutoComplete(instance, cursorPos) {
  try {
    // Fetch page suggestions from the API
    const response = await fetch('/api/pages/suggestions');
    if (!response.ok) return;

    const pages = await response.json();
    showAutoCompleteDropdown(instance, cursorPos, pages, 'page');
  } catch (error) {
    console.error('Failed to fetch page suggestions:', error);
  }
}

// Show media auto-completion dropdown
async function showMediaAutoComplete(instance, cursorPos) {
  try {
    // Fetch media suggestions from the API
    const response = await fetch('/api/media/suggestions');
    if (!response.ok) return;

    const mediaFiles = await response.json();
    showAutoCompleteDropdown(instance, cursorPos, mediaFiles, 'media');
  } catch (error) {
    console.error('Failed to fetch media suggestions:', error);
  }
}

// Show the auto-completion dropdown
function showAutoCompleteDropdown(instance, cursorPos, items, type) {
  const { textarea, container } = instance;

  // Create dropdown if it doesn't exist
  if (!instance.autoCompleteDropdown) {
    instance.autoCompleteDropdown = createAutoCompleteDropdown(instance);
    container.appendChild(instance.autoCompleteDropdown);
  }

  // Populate dropdown with items
  const dropdown = instance.autoCompleteDropdown;
  const itemsList = dropdown.querySelector('.autocomplete-items');
  itemsList.innerHTML = '';

  items.forEach((item, index) => {
    const itemElement = document.createElement('div');
    itemElement.className = 'autocomplete-item';
    itemElement.setAttribute('data-index', index);

    if (type === 'page') {
      itemElement.innerHTML = `
                <div class="autocomplete-title">${escapeHtml(item.title || item.name)}</div>
                <div class="autocomplete-subtitle text-muted">${escapeHtml(item.path || item.url || '')}</div>
            `;
      itemElement.setAttribute('data-value', item.name || item.title);
    } else if (type === 'media') {
      itemElement.innerHTML = `
                <div class="autocomplete-title">${escapeHtml(item.filename)}</div>
                <div class="autocomplete-subtitle text-muted">${item.size ? formatFileSize(item.size) : ''}</div>
            `;
      itemElement.setAttribute('data-value', item.filename);
    }

    itemElement.addEventListener('click', function () {
      selectAutoCompleteItem(instance, itemElement);
    });

    itemsList.appendChild(itemElement);
  });

  // Position dropdown
  positionAutoCompleteDropdown(instance, cursorPos);

  // Show dropdown
  dropdown.classList.remove('d-none');
  instance.selectedAutoCompleteIndex = 0;
  updateAutoCompleteSelection(instance);
  instance.autoCompleteType = type;
  instance.autoCompleteItems = items;
}

// Create auto-completion dropdown element
function createAutoCompleteDropdown(instance) {
  const dropdown = document.createElement('div');
  dropdown.className = 'autocomplete-dropdown d-none';
  dropdown.innerHTML = `
        <div class="autocomplete-items"></div>
        <div class="autocomplete-footer text-muted">
            <small>Use ↑↓ to navigate, Enter to select, Esc to cancel</small>
        </div>
    `;
  return dropdown;
}

// Position the auto-completion dropdown
function positionAutoCompleteDropdown(instance, cursorPos) {
  const { textarea, autoCompleteDropdown } = instance;

  // Calculate cursor position
  const position = getTextPosition(textarea, cursorPos);
  if (!position) return;

  // Position dropdown below cursor
  autoCompleteDropdown.style.position = 'absolute';
  autoCompleteDropdown.style.left = position.left + 'px';
  autoCompleteDropdown.style.top = (position.top + position.height + 5) + 'px';
  autoCompleteDropdown.style.zIndex = '1000';
}

// Update visual selection in auto-completion dropdown
function updateAutoCompleteSelection(instance) {
  const dropdown = instance.autoCompleteDropdown;
  if (!dropdown) return;

  const items = dropdown.querySelectorAll('.autocomplete-item');
  items.forEach((item, index) => {
    if (index === instance.selectedAutoCompleteIndex) {
      item.classList.add('selected');
    } else {
      item.classList.remove('selected');
    }
  });
}

// Select an auto-completion item
function selectAutoCompleteItem(instance, itemElement) {
  const { textarea } = instance;
  const value = itemElement.getAttribute('data-value');
  const type = instance.autoCompleteType;

  const cursorPos = textarea.selectionStart;
  const textBeforeCursor = textarea.value.substring(0, cursorPos);
  const macroStart = findMacroStart(textBeforeCursor);

  if (macroStart === -1) return;

  // Build the replacement text
  let replacement = '';
  if (type === 'page') {
    replacement = `[[${value}]]`;
  } else if (type === 'media') {
    replacement = `[[media:${value}]]`;
  }

  // Replace the macro in progress with the completed macro
  const beforeMacro = textarea.value.substring(0, macroStart);
  const afterCursor = textarea.value.substring(cursorPos);
  const newValue = beforeMacro + replacement + afterCursor;

  textarea.value = newValue;
  textarea.selectionStart = textarea.selectionEnd = macroStart + replacement.length;

  // Update preview and hide dropdown
  updatePreview(instance).catch(console.error);
  updateStats(instance);
  syncWithFormTextarea(instance, newValue);
  hideAutoComplete(instance);

  // Focus back on textarea
  textarea.focus();
}

// Filter auto-completion results based on user input
function filterAutoCompleteResults(instance, searchTerm) {
  const dropdown = instance.autoCompleteDropdown;
  if (!dropdown || !instance.autoCompleteItems) return;

  const items = instance.autoCompleteItems;
  const filteredItems = items.filter(item => {
    const searchableText = (item.title || item.name || item.filename || '').toLowerCase();
    return searchableText.includes(searchTerm.toLowerCase());
  });

  // Re-populate dropdown with filtered results
  showAutoCompleteDropdown(instance, instance.textarea.selectionStart, filteredItems, instance.autoCompleteType);
}

// Hide auto-completion dropdown
function hideAutoComplete(instance) {
  if (instance.autoCompleteDropdown) {
    instance.autoCompleteDropdown.classList.add('d-none');
    instance.selectedAutoCompleteIndex = 0;
    instance.autoCompleteType = null;
    instance.autoCompleteItems = null;
  }
}

// Helper function to format file sizes
function formatFileSize(bytes) {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
}

// Setup keyboard shortcuts
function setupKeyboardShorts(instance) {
  const { textarea } = instance;

  textarea.addEventListener('keydown', function (e) {
    if (e.ctrlKey || e.metaKey) {
      switch (e.key.toLowerCase()) {
        case 'b':
          e.preventDefault();
          wrapSelection(textarea, '**', '**');
          updatePreview(instance).catch(console.error);
          updateStats(instance);
          break;
        case 'i':
          e.preventDefault();
          wrapSelection(textarea, '*', '*');
          updatePreview(instance).catch(console.error);
          updateStats(instance);
          break;
        case 'k':
          e.preventDefault();
          insertLink(textarea);
          updatePreview(instance).catch(console.error);
          updateStats(instance);
          break;
      }
    }
  });
}

// Update the preview (markdown or HTML) - async version for proper media URL resolution
async function updatePreview(instance) {
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
      // For markdown content, convert to HTML using async version for proper media URLs
      html = await markdownToHtmlAsync(content);
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
      const previousValue = hiddenField.value || '';
      hiddenField.value = content || '';

      // Also trigger change event to ensure form validation updates
      hiddenField.dispatchEvent(new Event('input', { bubbles: true }));
      hiddenField.dispatchEvent(new Event('change', { bubbles: true }));

      console.log(`🔄 [${instance.id}] Synced content to form field: ${content?.length || 0} characters`);

      if (previousValue.length !== (content?.length || 0)) {
        console.log(`📏 [${instance.id}] Form field content length changed: ${previousValue.length} → ${content?.length || 0}`);
      }

      if (content && content.length > 100) {
        console.log(`📝 [${instance.id}] Synced content preview: "${content.substring(0, 100)}..."`);
      } else if (content && content.length > 0) {
        console.log(`📝 [${instance.id}] Synced content full: "${content}"`);
      }
    } else {
      console.warn(`⚠️ [${instance.id}] Hidden form field not found for syncing`);
      console.log(`🔍 [${instance.id}] Available selectors in container:`, instance.container ?
        Array.from(instance.container.querySelectorAll('*')).map(el => `${el.tagName}${el.id ? '#' + el.id : ''}${el.className ? '.' + el.className.split(' ').join('.') : ''}`).slice(0, 10) : 'no container');
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

// Process wiki macros (page links, media, templates) - async version
async function processWikiMacrosAsync(text, macroBlocks, macroPlaceholder) {
  // Store all async operations
  const mediaPromises = [];
  const mediaReplacements = [];

  // First pass: identify and extract media macros for async processing
  text = text.replace(/\[\[([^\]]+)\]\]/g, function (match, content) {
    // Split on | for parameters (e.g., [[PageName|Display Text]])
    const parts = content.split('|');
    const pageName = parts[0].trim();
    const displayText = parts[1] ? parts[1].trim() : pageName;

    // Check if this is a media macro
    if (pageName.toLowerCase().startsWith('media:')) {
      // Queue for async processing
      const placeholder = macroPlaceholder + macroBlocks.length;
      macroBlocks.push(''); // Reserve slot
      const mediaIndex = macroBlocks.length - 1;

      // Store the promise and replacement info
      mediaPromises.push(processMediaMacroAsync(match, content));
      mediaReplacements.push(mediaIndex);

      return placeholder;
    }

    // Regular page link
    const pageHtml = `<a href="/${encodeURIComponent(pageName)}" class="wiki-link" title="Go to page: ${escapeHtml(pageName)}">${escapeHtml(displayText)}</a>`;
    macroBlocks.push(pageHtml);
    return macroPlaceholder + (macroBlocks.length - 1);
  });

  // Process {{template-name}} templates (basic placeholder for now)
  text = text.replace(/\{\{([^}]+)\}\}/g, function (match, templateName) {
    const templateHtml = `<span class="wiki-template text-muted fst-italic" title="Template: ${escapeHtml(templateName)}">{{${escapeHtml(templateName)}}}</span>`;
    macroBlocks.push(templateHtml);
    return macroPlaceholder + (macroBlocks.length - 1);
  });

  // Wait for all media macros to resolve
  if (mediaPromises.length > 0) {
    try {
      const resolvedMedia = await Promise.all(mediaPromises);
      // Update the macro blocks with resolved media HTML
      for (let i = 0; i < resolvedMedia.length; i++) {
        const mediaIndex = mediaReplacements[i];
        macroBlocks[mediaIndex] = resolvedMedia[i];
      }
    } catch (error) {
      console.error('Failed to resolve some media macros:', error);
      // Fallback to synchronous processing for failed items
      for (let i = 0; i < mediaReplacements.length; i++) {
        if (!macroBlocks[mediaReplacements[i]]) {
          macroBlocks[mediaReplacements[i]] = '<span class="text-danger">Failed to load media</span>';
        }
      }
    }
  }

  return text;
}

// Process wiki macros (page links, media, templates) - synchronous fallback
function processWikiMacros(text, macroBlocks, macroPlaceholder) {
  // Process [[page-name]] page links
  text = text.replace(/\[\[([^\]]+)\]\]/g, function (match, content) {
    // Split on | for parameters (e.g., [[PageName|Display Text]])
    const parts = content.split('|');
    const pageName = parts[0].trim();
    const displayText = parts[1] ? parts[1].trim() : pageName;

    // Check if this is a media macro
    if (pageName.toLowerCase().startsWith('media:')) {
      const mediaHtml = processMediaMacro(match, content);
      macroBlocks.push(mediaHtml);
      return macroPlaceholder + (macroBlocks.length - 1);
    }

    // Regular page link
    const pageHtml = `<a href="/${encodeURIComponent(pageName)}" class="wiki-link" title="Go to page: ${escapeHtml(pageName)}">${escapeHtml(displayText)}</a>`;
    macroBlocks.push(pageHtml);
    return macroPlaceholder + (macroBlocks.length - 1);
  });

  // Process {{template-name}} templates (basic placeholder for now)
  text = text.replace(/\{\{([^}]+)\}\}/g, function (match, templateName) {
    const templateHtml = `<span class="wiki-template text-muted fst-italic" title="Template: ${escapeHtml(templateName)}">{{${escapeHtml(templateName)}}}</span>`;
    macroBlocks.push(templateHtml);
    return macroPlaceholder + (macroBlocks.length - 1);
  });

  return text;
}

// Process media macro with parameters (async version for API calls)
async function processMediaMacroAsync(match, content) {
  // Split on | for parameters
  const parts = content.split('|');
  const mediaPath = parts[0].trim().substring(6); // Remove 'media:' prefix

  // Parse parameters
  const params = {};
  for (let i = 1; i < parts.length; i++) {
    const part = parts[i].trim();
    if (part.includes('=')) {
      const [key, value] = part.split('=', 2);
      params[key.trim()] = value.trim();
    } else {
      // Parameter without value (e.g., just "thumb")
      params[part] = true;
    }
  }

  // Build media HTML based on parameters
  let mediaHtml = '';
  const fileName = escapeHtml(mediaPath);

  // Try to get the proper media URL from the API
  let mediaUrl = `/media/${encodeURIComponent(mediaPath)}`; // Fallback URL

  try {
    // Search for the media file by filename to get its ID
    const searchResponse = await fetch(`/api/media?search=${encodeURIComponent(mediaPath)}&pageSize=1`);
    if (searchResponse.ok) {
      const searchData = await searchResponse.json();
      if (searchData.items && searchData.items.length > 0) {
        const mediaFile = searchData.items.find(item =>
          item.fileName.toLowerCase() === mediaPath.toLowerCase()
        );
        if (mediaFile) {
          // Determine if we need a specific size
          let sizeParam = '';
          if (params.display === 'thumb' || params.thumb === true) {
            sizeParam = '?size=150';
          } else if (params.size && !isNaN(params.size)) {
            sizeParam = `?size=${params.size}`;
          }

          // Use the API endpoint that redirects to presigned URLs
          mediaUrl = `/api/media/${mediaFile.id}${sizeParam}`;
        }
      }
    }
  } catch (error) {
    console.warn('Failed to resolve media URL for', mediaPath, '- using fallback URL');
  }

  // Determine display size
  let width = '';
  let cssClass = 'wiki-media';

  if (params.display === 'thumb' || params.thumb === true) {
    width = '150px';
    cssClass += ' wiki-media-thumb';
  } else if (params.display === 'full' || params.full === true) {
    cssClass += ' wiki-media-full';
  } else if (params.size) {
    width = params.size + 'px';
    cssClass += ' wiki-media-sized';
  } else {
    width = '600px'; // Default size
    cssClass += ' wiki-media-default';
  }

  // Determine alignment
  if (params.align) {
    cssClass += ` text-${params.align}`;
  }

  // Build the HTML structure
  const altText = params.alt || params.caption || fileName;
  const title = params.caption || fileName;
  const style = width ? `max-width: ${width}; height: auto;` : '';

  if (params.caption) {
    // Media with caption (figure)
    mediaHtml = `<figure class="${cssClass}">
            <img src="${mediaUrl}" alt="${escapeHtml(altText)}" title="${escapeHtml(title)}" style="${style}" class="img-fluid">
            <figcaption class="wiki-media-caption text-muted small">${escapeHtml(params.caption)}</figcaption>
        </figure>`;
  } else {
    // Simple image
    mediaHtml = `<img src="${mediaUrl}" alt="${escapeHtml(altText)}" title="${escapeHtml(title)}" style="${style}" class="img-fluid ${cssClass}">`;
  }

  return mediaHtml;
}

// Synchronous fallback version for processMediaMacro (backwards compatibility)
function processMediaMacro(match, content) {
  // Split on | for parameters
  const parts = content.split('|');
  const mediaPath = parts[0].trim().substring(6); // Remove 'media:' prefix

  // Parse parameters
  const params = {};
  for (let i = 1; i < parts.length; i++) {
    const part = parts[i].trim();
    if (part.includes('=')) {
      const [key, value] = part.split('=', 2);
      params[key.trim()] = value.trim();
    } else {
      // Parameter without value (e.g., just "thumb")
      params[part] = true;
    }
  }

  // Build media HTML based on parameters
  let mediaHtml = '';
  const fileName = escapeHtml(mediaPath);

  // For preview, use a placeholder that will be resolved by the async version
  const mediaUrl = `/media/${encodeURIComponent(mediaPath)}`;

  // Determine display size
  let width = '';
  let cssClass = 'wiki-media';

  if (params.display === 'thumb' || params.thumb === true) {
    width = '150px';
    cssClass += ' wiki-media-thumb';
  } else if (params.display === 'full' || params.full === true) {
    cssClass += ' wiki-media-full';
  } else if (params.size) {
    width = params.size + 'px';
    cssClass += ' wiki-media-sized';
  } else {
    width = '600px'; // Default size
    cssClass += ' wiki-media-default';
  }

  // Determine alignment
  if (params.align) {
    cssClass += ` text-${params.align}`;
  }

  // Build the HTML structure
  const altText = params.alt || params.caption || fileName;
  const title = params.caption || fileName;
  const style = width ? `max-width: ${width}; height: auto;` : '';

  if (params.caption) {
    // Media with caption (figure)
    mediaHtml = `<figure class="${cssClass}">
            <img src="${mediaUrl}" alt="${escapeHtml(altText)}" title="${escapeHtml(title)}" style="${style}" class="img-fluid">
            <figcaption class="wiki-media-caption text-muted small">${escapeHtml(params.caption)}</figcaption>
        </figure>`;
  } else {
    // Simple image
    mediaHtml = `<img src="${mediaUrl}" alt="${escapeHtml(altText)}" title="${escapeHtml(title)}" style="${style}" class="img-fluid ${cssClass}">`;
  }

  return mediaHtml;
}

// Helper function to escape HTML entities
function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

// Basic markdown to HTML conversion (async version for proper media URL resolution)
async function markdownToHtmlAsync(markdown) {
  let html = markdown;

  // Store code blocks temporarily to protect them from processing
  const codeBlocks = [];
  const codeBlockPlaceholder = '___CODE_BLOCK_PLACEHOLDER___';

  // Extract and protect code blocks first
  html = html.replace(/```(\w+)?\n?([\s\S]*?)```/g, function (match, lang, code) {
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

  html = html.replace(/`([^`]*)`/g, function (match, code) {
    const escapedCode = code
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');

    const inlineCodeHtml = `<code class="language-none">${escapedCode}</code>`;
    inlineCodeBlocks.push(inlineCodeHtml);
    return inlineCodePlaceholder + (inlineCodeBlocks.length - 1);
  });

  // Process wiki macros before escaping HTML (using async version for proper media URLs)
  const wikiMacroBlocks = [];
  const wikiMacroPlaceholder = '___WIKI_MACRO_PLACEHOLDER___';
  html = await processWikiMacrosAsync(html, wikiMacroBlocks, wikiMacroPlaceholder);

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

  // Restore wiki macro blocks first (before other content that might contain similar patterns)
  for (let i = 0; i < wikiMacroBlocks.length; i++) {
    html = html.replace(wikiMacroPlaceholder + i, wikiMacroBlocks[i]);
  }

  // Restore inline code blocks
  for (let i = 0; i < inlineCodeBlocks.length; i++) {
    html = html.replace(inlineCodePlaceholder + i, inlineCodeBlocks[i]);
  }

  // Restore code blocks last
  for (let i = 0; i < codeBlocks.length; i++) {
    html = html.replace(codeBlockPlaceholder + i, codeBlocks[i]);
  }

  return html;
}

// Basic markdown to HTML conversion (synchronous fallback)
function markdownToHtml(markdown) {
  let html = markdown;

  // Store code blocks temporarily to protect them from processing
  const codeBlocks = [];
  const codeBlockPlaceholder = '___CODE_BLOCK_PLACEHOLDER___';

  // Extract and protect code blocks first
  html = html.replace(/```(\w+)?\n?([\s\S]*?)```/g, function (match, lang, code) {
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

  html = html.replace(/`([^`]*)`/g, function (match, code) {
    const escapedCode = code
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');

    const inlineCodeHtml = `<code class="language-none">${escapedCode}</code>`;
    inlineCodeBlocks.push(inlineCodeHtml);
    return inlineCodePlaceholder + (inlineCodeBlocks.length - 1);
  });

  // Process wiki macros before escaping HTML (using synchronous version as fallback)
  const wikiMacroBlocks = [];
  const wikiMacroPlaceholder = '___WIKI_MACRO_PLACEHOLDER___';
  html = processWikiMacros(html, wikiMacroBlocks, wikiMacroPlaceholder);

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

  // Restore wiki macro blocks first (before other content that might contain similar patterns)
  for (let i = 0; i < wikiMacroBlocks.length; i++) {
    html = html.replace(wikiMacroPlaceholder + i, wikiMacroBlocks[i]);
  }

  // Restore inline code blocks
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
window.insertMarkdownFor = function (editorId, before, after) {
  const instance = editorInstances.get(editorId);
  if (!instance) {
    console.error('Editor instance not found for insertMarkdownFor:', editorId);
    return;
  }

  wrapSelection(instance.textarea, before, after);
  updatePreview(instance).catch(console.error);
  updateStats(instance);
};

// Legacy global function for toolbar buttons (fallback using focus)
window.insertMarkdown = function (before, after) {
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
  updatePreview(activeInstance).catch(console.error);
  updateStats(activeInstance);
};

// Get editor content
window.getEnhancedEditorContent = function (editorId) {
  const instance = editorInstances.get(editorId);
  if (!instance) {
    console.error('Editor instance not found:', editorId);
    return '';
  }
  return instance.textarea.value;
};

// Show status message
window.showEditorStatus = function (message) {
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
window.updateEditorFormat = function (editorId, newFormat) {
  const instance = editorInstances.get(editorId);
  if (!instance) {
    console.error('Editor instance not found for updateEditorFormat:', editorId);
    return;
  }

  instance.format = newFormat.toLowerCase();
  // Trigger a preview update to reflect the new format
  updatePreview(instance).catch(console.error);
  console.log(`Updated editor ${editorId} to format:`, newFormat);
};

// Legacy global format update (for compatibility)
window.updateAllEditorsFormat = function (newFormat) {
  for (const instance of editorInstances.values()) {
    instance.format = newFormat.toLowerCase();
    // Trigger a preview update to reflect the new format
    updatePreview(instance).catch(console.error);
  }
  console.log('Updated all editor instances to format:', newFormat);
};

// Clean up editor instance
window.destroyEnhancedEditor = function (editorId) {
  const instance = editorInstances.get(editorId);
  if (instance) {
    clearTimeout(instance.updateTimeout);

    // Clean up mirror div for this textarea if it exists
    if (mirrorDivCache.has(instance.textarea)) {
      const mirrorDiv = mirrorDivCache.get(instance.textarea);
      if (mirrorDiv && mirrorDiv.parentNode) {
        mirrorDiv.parentNode.removeChild(mirrorDiv);
      }
      mirrorDivCache.delete(instance.textarea);
    }

    editorInstances.delete(editorId);
    console.log('Enhanced editor destroyed:', editorId);
  }
};

// Track unsaved changes
let hasUnsavedChanges = false;
let originalContent = '';
let lastSavedContent = '';

// Set original content when editor is initialized
window.setOriginalContent = function (content) {
  originalContent = content || '';
  lastSavedContent = content || '';
  hasUnsavedChanges = false;
  console.log('📋 Set original content:', originalContent.length, 'characters');
};

// Mark content as saved (draft or committed)
window.markContentAsSaved = function () {
  const currentContent = getCurrentEditorContent();
  lastSavedContent = currentContent;
  hasUnsavedChanges = false;
  console.log('💾 Marked content as saved');
};

// Check if content has changed from last saved state
window.checkForUnsavedChanges = function () {
  const currentContent = getCurrentEditorContent();
  hasUnsavedChanges = currentContent !== lastSavedContent;
  console.log('🔍 Checking for changes:', hasUnsavedChanges, 'current:', currentContent.length, 'saved:', lastSavedContent.length);
  return hasUnsavedChanges;
};

// Get current editor content from any active editor
function getCurrentEditorContent() {
  // Try to get content from enhanced editor
  for (const [id, instance] of editorInstances) {
    if (instance && instance.textarea) {
      return instance.textarea.value || '';
    }
  }

  // Fallback to hidden textarea
  const bodyTextarea = document.getElementById('body-textarea');
  if (bodyTextarea) {
    return bodyTextarea.value || '';
  }

  return '';
}

// Auto-save functionality integration with change tracking
window.addEventListener('beforeunload', function (e) {
  // Clean up all editor instances
  for (const [id] of editorInstances) {
    destroyEnhancedEditor(id);
  }

  // Check for unsaved changes and show warning if needed
  if (checkForUnsavedChanges()) {
    const message = 'You have unsaved changes. Are you sure you want to leave?';
    e.preventDefault();
    e.returnValue = message;
    return message;
  }
});

// Collaboration features
let remoteCursors = new Map();

// Set editor content programmatically (for collaboration sync)
window.setEnhancedEditorContent = function (editorId, content) {
  const instance = editorInstances.get(editorId);
  if (!instance) {
    console.error('Editor instance not found:', editorId);
    return;
  }

  console.log(`🔄 [${editorId}] Setting content programmatically (length: ${content?.length})`);
  instance.isApplyingRemoteOperation = true;
  instance.textarea.value = content;
  updatePreview(instance).catch(console.error);
  updateStats(instance);
  instance.isApplyingRemoteOperation = false;

  console.log(`✅ [${editorId}] Content set successfully`);
};

// Apply insert operation from remote user
window.applyInsertOperation = function (editorId, position, text) {
  const instance = editorInstances.get(editorId);
  if (!instance) {
    console.error('Editor instance not found:', editorId);
    return;
  }

  console.log(`📥 [${editorId}] Applying remote insert: "${text}" at position ${position}`);
  instance.isApplyingRemoteOperation = true;

  const textarea = instance.textarea;
  const currentValue = textarea.value;
  const newValue = currentValue.slice(0, position) + text + currentValue.slice(position);

  textarea.value = newValue;
  updatePreview(instance).catch(console.error);
  updateStats(instance);

  instance.isApplyingRemoteOperation = false;

  console.log(`✅ [${editorId}] Applied remote insert successfully`);
};

// Apply delete operation from remote user
window.applyDeleteOperation = function (editorId, position, length) {
  const instance = editorInstances.get(editorId);
  if (!instance) {
    console.error('Editor instance not found:', editorId);
    return;
  }

  instance.isApplyingRemoteOperation = true;

  const textarea = instance.textarea;
  const currentValue = textarea.value;
  const newValue = currentValue.slice(0, position) + currentValue.slice(position + length);

  textarea.value = newValue;
  updatePreview(instance).catch(console.error);
  updateStats(instance);

  instance.isApplyingRemoteOperation = false;

  console.log(`✅ Applied remote delete: ${length} characters at position ${position}`);
};

// Apply replace operation from remote user
window.applyReplaceOperation = function (editorId, selectionStart, selectionEnd, newText) {
  const instance = editorInstances.get(editorId);
  if (!instance) {
    console.error('Editor instance not found:', editorId);
    return;
  }

  instance.isApplyingRemoteOperation = true;

  const textarea = instance.textarea;
  const currentValue = textarea.value;

  console.log(`🔄 Applying replace operation:`);
  console.log(`  Current: "${currentValue}"`);
  console.log(`  Replace range ${selectionStart}-${selectionEnd} ("${currentValue.slice(selectionStart, selectionEnd)}") with "${newText}"`);

  const newValue = currentValue.slice(0, selectionStart) + newText + currentValue.slice(selectionEnd);

  console.log(`  Result: "${newValue}"`);

  textarea.value = newValue;
  updatePreview(instance).catch(console.error);
  updateStats(instance);

  instance.isApplyingRemoteOperation = false;

  console.log(`✅ Applied remote replace: ${selectionStart}-${selectionEnd} -> "${newText}"`);
};

// Get current cursor position
window.getEditorCursorPosition = function (editorId) {
  const instance = editorInstances.get(editorId);
  if (!instance) {
    console.error('Editor instance not found:', editorId);
    return [0, 0];
  }

  const textarea = instance.textarea;
  return [textarea.selectionStart, textarea.selectionEnd];
};

// Update remote cursor visualization for specific editor
window.updateRemoteCursor = function (editorId, userId, start, end, userColor, displayName) {
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
window.removeRemoteCursor = function (editorId, userId) {
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
window.removeRemoteCursorFromAll = function (userId) {
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

    // Ensure start <= end
    if (start > end) {
      [start, end] = [end, start];
    }

    const startPos = getTextPosition(textarea, start);
    const endPos = getTextPosition(textarea, end);

    if (!startPos || !endPos) {
      console.log(`❌ Could not calculate selection positions`);
      return;
    }

    console.log(`📍 Selection positions:`, { startPos, endPos });

    // Create selection container
    const selectionElement = document.createElement('div');
    selectionElement.className = 'remote-selection';
    selectionElement.setAttribute('data-selection-user-id', userId);
    selectionElement.style.color = userColor;

    const textValue = textarea.value;
    const computedStyle = window.getComputedStyle(textarea);
    const textareaWidth = textarea.clientWidth -
      (parseInt(computedStyle.paddingLeft) || 0) -
      (parseInt(computedStyle.paddingRight) || 0) -
      (parseInt(computedStyle.borderLeftWidth) || 0) -
      (parseInt(computedStyle.borderRightWidth) || 0);

    // Check if this is a single-line or multi-line selection
    if (Math.abs(startPos.top - endPos.top) < startPos.height / 2) {
      // Single-line selection
      const highlight = createSelectionHighlight(
        Math.min(startPos.left, endPos.left),
        startPos.top,
        Math.abs(endPos.left - startPos.left),
        startPos.height,
        userColor
      );
      selectionElement.appendChild(highlight);
    } else {
      // Multi-line selection - render each line separately
      const textBeforeStart = textValue.substring(0, start);
      const selectedText = textValue.substring(start, end);
      const lines = selectedText.split('\n');

      let currentIndex = start;

      lines.forEach((line, lineIndex) => {
        if (lineIndex === 0) {
          // First line: from start position to end of line
          const lineEndPos = getTextPosition(textarea, currentIndex + line.length);
          const highlight = createSelectionHighlight(
            startPos.left,
            startPos.top,
            Math.max(lineEndPos.left - startPos.left, 0),
            startPos.height,
            userColor
          );
          selectionElement.appendChild(highlight);
        } else if (lineIndex === lines.length - 1) {
          // Last line: from beginning of line to end position
          const lineStartPos = getTextPosition(textarea, currentIndex);
          const highlight = createSelectionHighlight(
            lineStartPos.left,
            lineStartPos.top,
            Math.max(endPos.left - lineStartPos.left, 0),
            endPos.height,
            userColor
          );
          selectionElement.appendChild(highlight);
        } else {
          // Middle lines: full width
          const lineStartPos = getTextPosition(textarea, currentIndex);
          const highlight = createSelectionHighlight(
            lineStartPos.left,
            lineStartPos.top,
            textareaWidth - lineStartPos.left + parseInt(computedStyle.paddingLeft || 0),
            lineStartPos.height,
            userColor
          );
          selectionElement.appendChild(highlight);
        }

        // Move to next line (including the newline character except for the last line)
        currentIndex += line.length;
        if (lineIndex < lines.length - 1) {
          currentIndex += 1; // Add 1 for the newline character
        }
      });
    }

    overlay.appendChild(selectionElement);

    // Trigger fade-in animation
    setTimeout(() => {
      selectionElement.classList.add('visible', 'moving');
    }, 10);

    setTimeout(() => {
      selectionElement.classList.remove('moving');
    }, 300);

    console.log(`✅ Rendered multi-line selection for ${userId}`);

  } catch (error) {
    console.error('❌ Failed to render remote selection:', error);
  }
}

// Helper function to create individual selection highlight rectangles
function createSelectionHighlight(left, top, width, height, userColor) {
  const highlight = document.createElement('div');
  highlight.className = 'remote-selection-highlight';
  highlight.style.position = 'absolute';
  highlight.style.left = left + 'px';
  highlight.style.top = top + 'px';
  highlight.style.width = Math.max(width, 0) + 'px';
  highlight.style.height = height + 'px';
  highlight.style.backgroundColor = userColor;
  highlight.style.opacity = '0.3';
  highlight.style.pointerEvents = 'none';
  return highlight;
}

// Cache for mirror divs to avoid recreating them
const mirrorDivCache = new WeakMap();

// Create a mirror div that exactly matches the textarea's styling
function createMirrorDiv(textarea) {
  // Check cache first
  if (mirrorDivCache.has(textarea)) {
    const cachedDiv = mirrorDivCache.get(textarea);
    // Update width in case textarea was resized
    cachedDiv.style.width = (textarea.clientWidth -
      (parseInt(getComputedStyle(textarea).paddingLeft) || 0) -
      (parseInt(getComputedStyle(textarea).paddingRight) || 0) -
      (parseInt(getComputedStyle(textarea).borderLeftWidth) || 0) -
      (parseInt(getComputedStyle(textarea).borderRightWidth) || 0)) + 'px';
    return cachedDiv;
  }

  const computedStyle = window.getComputedStyle(textarea);
  const mirrorDiv = document.createElement('div');

  // Copy essential textarea styles for accurate text rendering
  const stylesToCopy = [
    'font-family', 'font-size', 'font-weight', 'font-style', 'font-variant',
    'line-height', 'letter-spacing', 'word-spacing', 'text-indent',
    'padding-top', 'padding-left', 'padding-right', 'padding-bottom',
    'border-top-width', 'border-left-width', 'border-right-width', 'border-bottom-width',
    'white-space', 'word-wrap', 'overflow-wrap', 'word-break',
    'text-transform', 'direction', 'tab-size'
  ];

  stylesToCopy.forEach(style => {
    const value = computedStyle.getPropertyValue(style);
    if (value) {
      mirrorDiv.style.setProperty(style, value);
    }
  });

  // Calculate the actual content width (excluding padding and borders)
  const contentWidth = textarea.clientWidth -
    (parseInt(computedStyle.paddingLeft) || 0) -
    (parseInt(computedStyle.paddingRight) || 0) -
    (parseInt(computedStyle.borderLeftWidth) || 0) -
    (parseInt(computedStyle.borderRightWidth) || 0);

  // Mirror-specific styles for precise positioning
  mirrorDiv.style.position = 'absolute';
  mirrorDiv.style.top = '-9999px';
  mirrorDiv.style.left = '-9999px';
  mirrorDiv.style.visibility = 'hidden';
  mirrorDiv.style.width = contentWidth + 'px';
  mirrorDiv.style.height = 'auto';
  mirrorDiv.style.overflow = 'visible';
  mirrorDiv.style.whiteSpace = 'pre-wrap';
  mirrorDiv.style.wordWrap = 'break-word';
  mirrorDiv.style.overflowWrap = 'break-word';
  mirrorDiv.style.boxSizing = 'content-box';

  // Ensure consistent rendering
  mirrorDiv.style.margin = '0';
  mirrorDiv.style.border = 'none';
  mirrorDiv.style.outline = 'none';
  mirrorDiv.style.resize = 'none';

  document.body.appendChild(mirrorDiv);

  // Cache the mirror div
  mirrorDivCache.set(textarea, mirrorDiv);

  return mirrorDiv;
}

// Calculate text position in pixels relative to textarea using mirror div technique
function getTextPosition(textarea, textIndex) {
  try {
    const textValue = textarea.value;

    // Clamp textIndex to valid range
    textIndex = Math.max(0, Math.min(textIndex, textValue.length));

    const mirrorDiv = createMirrorDiv(textarea);

    // Get the text up to the cursor position
    const textBeforeCursor = textValue.substring(0, textIndex);

    // Create a zero-width span element to mark the cursor position
    const cursorSpan = document.createElement('span');
    cursorSpan.style.position = 'absolute';
    cursorSpan.style.width = '0';
    cursorSpan.style.height = '1em';

    // Set mirror content with only text before cursor + cursor marker
    mirrorDiv.innerHTML = '';

    // Add text before cursor as plain text node to maintain exact formatting
    if (textBeforeCursor) {
      const textNode = document.createTextNode(textBeforeCursor);
      mirrorDiv.appendChild(textNode);
    }

    // Add the cursor marker span
    mirrorDiv.appendChild(cursorSpan);

    // Force layout computation
    mirrorDiv.offsetHeight;

    // Get the computed style for offsets
    const computedStyle = window.getComputedStyle(textarea);
    const paddingLeft = parseInt(computedStyle.paddingLeft) || 0;
    const paddingTop = parseInt(computedStyle.paddingTop) || 0;
    const borderLeft = parseInt(computedStyle.borderLeftWidth) || 0;
    const borderTop = parseInt(computedStyle.borderTopWidth) || 0;

    // Get textarea's bounding rect for absolute positioning reference
    const textareaRect = textarea.getBoundingClientRect();

    // Get the precise position of the cursor span
    const spanRect = cursorSpan.getBoundingClientRect();

    // Calculate position relative to the textarea's content area
    const position = {
      left: paddingLeft + (spanRect.left - textareaRect.left - borderLeft),
      top: paddingTop + (spanRect.top - textareaRect.top - borderTop),
      height: parseInt(computedStyle.lineHeight) || parseFloat(computedStyle.fontSize) || 16
    };

    // Ensure position is not negative
    position.left = Math.max(paddingLeft, position.left);
    position.top = Math.max(paddingTop, position.top);

    console.log(`📍 Mirror div position calc for index ${textIndex}:`, position);

    return position;

  } catch (error) {
    console.error('❌ Failed to calculate text position with mirror div:', error);

    // Fallback: use a more conservative approximation
    const computedStyle = window.getComputedStyle(textarea);
    const fontSize = parseInt(computedStyle.fontSize) || 14;
    const lineHeight = parseInt(computedStyle.lineHeight) || fontSize * 1.2;
    const paddingLeft = parseInt(computedStyle.paddingLeft) || 0;
    const paddingTop = parseInt(computedStyle.paddingTop) || 0;

    // Count newlines to estimate row
    const textValue = textarea.value;
    const textBeforeIndex = textValue.substring(0, textIndex);
    const lines = textBeforeIndex.split('\n');
    const row = lines.length - 1;

    // Estimate column position using average character width
    const lastLine = lines[lines.length - 1] || '';
    const avgCharWidth = fontSize * 0.6; // More conservative estimate
    const col = lastLine.length;

    return {
      left: paddingLeft + (col * avgCharWidth),
      top: paddingTop + (row * lineHeight),
      height: lineHeight
    };
  }
}

// Check if Blazor is connected (for collaboration readiness)
window.checkBlazorConnection = function () {
  try {
    // Check if Blazor SignalR connection is working
    return typeof Blazor !== 'undefined' && Blazor.defaultReconnectionHandler;
  } catch (error) {
    return false;
  }
};

// Setup drag and drop functionality
function setupDragAndDrop(instance) {
  const { textarea, container, id } = instance;

  if (!container) {
    console.warn(`Container not found for editor ${id}, skipping drag-drop setup`);
    return;
  }

  const overlay = document.getElementById(`drag-drop-overlay-${id}`);

  if (!overlay) {
    console.warn(`Drag-drop overlay not found for editor ${id}`);
    return;
  }

  console.log(`🎯 Setting up drag-and-drop for editor ${id}`);

  let dragCounter = 0;

  // Use a shared global state for file dragging detection
  if (!window.globalFileDragState) {
    window.globalFileDragState = {
      isDragging: false,
      instances: new Set()
    };

    // Add global document listeners only once
    document.addEventListener('dragenter', function (e) {
      if (e.dataTransfer && e.dataTransfer.types.includes('Files')) {
        window.globalFileDragState.isDragging = true;
      }
    }, false);

    document.addEventListener('dragleave', function (e) {
      // Only reset if leaving the document entirely
      if (!e.relatedTarget || e.relatedTarget.nodeName === 'HTML') {
        window.globalFileDragState.isDragging = false;
        // Hide all overlays
        window.globalFileDragState.instances.forEach(instanceData => {
          if (instanceData.overlay && instanceData.overlay.classList) {
            instanceData.overlay.classList.add('d-none');
            instanceData.overlay.classList.remove('drag-over');
          }
          instanceData.dragCounter = 0;
        });
      }
    }, false);

    document.addEventListener('drop', function (e) {
      window.globalFileDragState.isDragging = false;
    }, false);

    // Prevent default behaviors
    document.addEventListener('dragover', function (e) {
      e.preventDefault();
      e.stopPropagation();
    }, false);

    document.addEventListener('drop', function (e) {
      e.preventDefault();
      e.stopPropagation();
    }, false);
  }

  // Register this instance
  const instanceData = { overlay, dragCounter: 0 };
  window.globalFileDragState.instances.add(instanceData);

  // Handle events on the editor elements
  textarea.addEventListener('dragenter', handleDragEnter, false);
  textarea.addEventListener('dragover', handleDragOver, false);
  textarea.addEventListener('dragleave', handleDragLeave, false);
  textarea.addEventListener('drop', handleDrop, false);

  container.addEventListener('dragenter', handleDragEnter, false);
  container.addEventListener('dragover', handleDragOver, false);
  container.addEventListener('dragleave', handleDragLeave, false);
  container.addEventListener('drop', handleDrop, false);

  function preventDefaults(e) {
    e.preventDefault();
    e.stopPropagation();
  }

  function handleDragEnter(e) {
    e.preventDefault();
    e.stopPropagation();
    instanceData.dragCounter++;

    // Only show overlay if we're dragging files and we're over the editor area
    if (window.globalFileDragState.isDragging && e.dataTransfer.types.includes('Files')) {
      console.log('📁 Files detected over editor, showing overlay');
      overlay.classList.remove('d-none');
      overlay.classList.add('drag-over');
    }
  }

  function handleDragOver(e) {
    e.preventDefault();
    e.stopPropagation();

    if (window.globalFileDragState.isDragging && e.dataTransfer.types.includes('Files')) {
      e.dataTransfer.dropEffect = 'copy';
    }
  }

  function handleDragLeave(e) {
    e.preventDefault();
    e.stopPropagation();
    instanceData.dragCounter--;

    if (instanceData.dragCounter <= 0) {
      console.log('🔼 Drag leave editor area, hiding overlay');
      instanceData.dragCounter = 0;
      overlay.classList.add('d-none');
      overlay.classList.remove('drag-over');
    }
  }

  function handleDrop(e) {
    console.log('🎯 Drop detected!', e);
    e.preventDefault();
    e.stopPropagation();
    instanceData.dragCounter = 0;
    window.globalFileDragState.isDragging = false;

    overlay.classList.add('d-none');
    overlay.classList.remove('drag-over');

    const files = Array.from(e.dataTransfer.files);
    console.log('📋 Files dropped:', files);

    const imageFiles = files.filter(file => file.type.startsWith('image/'));

    if (imageFiles.length === 0) {
      showNotification('Please drop image files only', 'warning');
      return;
    }

    // Process the first image file
    if (imageFiles.length > 0) {
      console.log('🖼️ Processing image file:', imageFiles[0]);
      showUploadModal(imageFiles[0], instance);
    }
  }
}

// Show upload modal with file details
function showUploadModal(file, instance) {
  const modalId = `image-upload-modal-${instance.id}`;

  // Remove existing modal if it exists
  const existingModal = document.getElementById(modalId);
  if (existingModal) {
    existingModal.remove();
  }

  // Create modal HTML outside any form
  const modalHtml = `
        <div class="modal fade" id="${modalId}" tabindex="-1" aria-hidden="true">
            <div class="modal-dialog modal-lg">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title">Upload Image</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body">
                        <div id="upload-preview-${instance.id}" class="text-center mb-3 d-none">
                            <img id="preview-image-${instance.id}" src="" alt="Preview" class="img-fluid rounded" style="max-height: 200px;">
                        </div>
                        
                        <!-- Separate form for upload modal to isolate validation -->
                        <form id="upload-form-${instance.id}">
                            <div class="mb-3">
                                <label for="upload-filename-${instance.id}" class="form-label">File Name <span class="text-danger">*</span></label>
                                <input type="text" class="form-control" id="upload-filename-${instance.id}" placeholder="Enter filename (without extension)" required>
                                <div class="form-text">The file extension will be added automatically</div>
                            </div>
                            
                            <div class="row mb-3">
                                <div class="col-md-6">
                                    <label for="upload-size-${instance.id}" class="form-label">Display Size</label>
                                    <select class="form-select" id="upload-size-${instance.id}">
                                        <option value="">Default (600px)</option>
                                        <option value="thumb">Thumbnail (150px)</option>
                                        <option value="300">Small (300px)</option>
                                        <option value="500">Medium (500px)</option>
                                        <option value="full">Full Size</option>
                                    </select>
                                </div>
                                <div class="col-md-6">
                                    <label for="upload-align-${instance.id}" class="form-label">Alignment</label>
                                    <select class="form-select" id="upload-align-${instance.id}">
                                        <option value="">Default</option>
                                        <option value="left">Left</option>
                                        <option value="center">Center</option>
                                        <option value="right">Right</option>
                                    </select>
                                </div>
                            </div>
                            
                            <div class="mb-3">
                                <label for="upload-description-${instance.id}" class="form-label">Description/Caption</label>
                                <textarea class="form-control" id="upload-description-${instance.id}" rows="2" placeholder="Brief description or caption for the image"></textarea>
                            </div>
                            
                            <div class="mb-3">
                                <label for="upload-alttext-${instance.id}" class="form-label">Alt Text</label>
                                <input type="text" class="form-control" id="upload-alttext-${instance.id}" placeholder="Alternative text for accessibility">
                                <div class="form-text">Describes the image for screen readers and when the image fails to load</div>
                            </div>
                        </form>
                        
                        <div class="progress d-none" id="upload-progress-${instance.id}">
                            <div class="progress-bar" role="progressbar" style="width: 0%"></div>
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                        <button type="button" class="btn btn-primary" id="upload-confirm-${instance.id}">Upload & Insert</button>
                    </div>
                </div>
            </div>
        </div>
    `;

  // Add modal to body (outside any form)
  document.body.insertAdjacentHTML('beforeend', modalHtml);

  // Get references to the new elements
  const modal = new bootstrap.Modal(document.getElementById(modalId));
  const previewContainer = document.getElementById(`upload-preview-${instance.id}`);
  const previewImage = document.getElementById(`preview-image-${instance.id}`);
  const filenameInput = document.getElementById(`upload-filename-${instance.id}`);
  const descriptionInput = document.getElementById(`upload-description-${instance.id}`);
  const altTextInput = document.getElementById(`upload-alttext-${instance.id}`);
  const sizeSelect = document.getElementById(`upload-size-${instance.id}`);
  const alignSelect = document.getElementById(`upload-align-${instance.id}`);
  const confirmButton = document.getElementById(`upload-confirm-${instance.id}`);

  // Show preview
  const reader = new FileReader();
  reader.onload = function (e) {
    previewImage.src = e.target.result;
    previewContainer.classList.remove('d-none');
  };
  reader.readAsDataURL(file);

  // Set default filename (without extension)
  const nameWithoutExt = file.name.replace(/\.[^/.]+$/, '');
  filenameInput.value = nameWithoutExt;

  // Clear other fields
  descriptionInput.value = '';
  altTextInput.value = '';
  sizeSelect.value = '';
  alignSelect.value = '';

  // Handle upload confirmation with form validation
  confirmButton.onclick = function () {
    const uploadForm = document.getElementById(`upload-form-${instance.id}`);
    if (uploadForm.checkValidity()) {
      uploadImageFile(file, instance, {
        filename: filenameInput.value.trim(),
        description: descriptionInput.value.trim(),
        altText: altTextInput.value.trim(),
        size: sizeSelect.value,
        align: alignSelect.value
      });
      modal.hide();
    } else {
      // Show validation messages
      uploadForm.reportValidity();
    }
  };

  // Clean up modal when hidden
  document.getElementById(modalId).addEventListener('hidden.bs.modal', function () {
    this.remove();
  });

  modal.show();
}

// Upload image file and insert template
async function uploadImageFile(file, instance, metadata) {
  const progressContainer = document.getElementById(`upload-progress-${instance.id}`);
  const progressBar = progressContainer.querySelector('.progress-bar');

  progressContainer.classList.remove('d-none');
  progressBar.style.width = '0%';

  try {
    const formData = new FormData();

    // Use custom filename if provided, otherwise use original
    if (metadata.filename) {
      const extension = file.name.split('.').pop();
      const newFile = new File([file], `${metadata.filename}.${extension}`, { type: file.type });
      formData.append('file', newFile);
    } else {
      formData.append('file', file);
    }

    if (metadata.description) {
      formData.append('description', metadata.description);
    }
    if (metadata.altText) {
      formData.append('altText', metadata.altText);
    }

    const response = await fetch('/api/media/upload', {
      method: 'POST',
      body: formData
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || 'Upload failed');
    }

    const result = await response.json();
    progressBar.style.width = '100%';

    // Insert media template at cursor position with parameters
    const cursorPos = instance.textarea.selectionStart;
    const currentContent = instance.textarea.value;

    // Build media template with parameters
    let mediaTemplate = `[[media:${result.fileName}`;

    // Add size parameter if specified
    if (metadata.size) {
      if (metadata.size === 'thumb') {
        mediaTemplate += `|display=thumb`;
      } else if (metadata.size === 'full') {
        mediaTemplate += `|display=full`;
      } else {
        mediaTemplate += `|size=${metadata.size}`;
      }
    }

    // Add alignment parameter if specified
    if (metadata.align) {
      mediaTemplate += `|align=${metadata.align}`;
    }

    // Add caption if description is provided
    if (metadata.description) {
      mediaTemplate += `|caption=${metadata.description}`;
    }

    // Add alt text if provided
    if (metadata.altText) {
      mediaTemplate += `|alt=${metadata.altText}`;
    }

    mediaTemplate += `]]`;

    const newContent = currentContent.substring(0, cursorPos) +
      mediaTemplate +
      currentContent.substring(cursorPos);

    instance.textarea.value = newContent;
    instance.textarea.selectionStart = instance.textarea.selectionEnd = cursorPos + mediaTemplate.length;

    // Update preview and sync
    updatePreview(instance).catch(console.error);
    updateStats(instance);
    syncWithFormTextarea(instance, newContent);

    showNotification(`Image "${result.fileName}" uploaded and inserted successfully`, 'success');

  } catch (error) {
    console.error('Upload failed:', error);
    showNotification(`Upload failed: ${error.message}`, 'danger');
  } finally {
    progressContainer.classList.add('d-none');
  }
}

// Show notification
function showNotification(message, type) {
  const toast = document.createElement('div');
  toast.className = `toast align-items-center text-white bg-${type} border-0`;
  toast.setAttribute('role', 'alert');
  toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">${message}</div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
        </div>
    `;

  let container = document.getElementById('toast-container');
  if (!container) {
    container = document.createElement('div');
    container.id = 'toast-container';
    container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
    container.style.zIndex = '1055';
    document.body.appendChild(container);
  }

  container.appendChild(toast);

  const bsToast = new bootstrap.Toast(toast);
  bsToast.show();

  toast.addEventListener('hidden.bs.toast', () => {
    toast.remove();
  });
}

// Mark content as committed (for collaboration coordination)
window.markContentAsCommitted = function () {
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
  textarea.addEventListener('input', function (e) {
    if (instance.isApplyingRemoteOperation) {
      console.log(`⏭️ [${instance.id}] Skipping input event - applying remote operation`);
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
        console.log(`   Content before: "${lastContent}" (length: ${lastContent.length})`);
        console.log(`   Content after:  "${currentContent}" (length: ${currentContent.length})`);
        console.log(`   Selection was: ${lastSelectionStart}-${lastSelectionEnd}, now: ${currentSelectionStart}-${currentSelectionEnd}`);

        if (operation.type === 'replace') {
          console.log(`🔄 [${instance.id}] Calling OnTextReplace(${operation.selectionStart}, ${operation.selectionEnd}, "${operation.newText}")`);
          componentRef.invokeMethodAsync('OnTextReplace',
            operation.selectionStart,
            operation.selectionEnd,
            operation.newText);
        } else {
          console.log(`🔄 [${instance.id}] Calling OnTextChange("${currentContent}", ${operation.position}, "${operation.type}", "${operation.text}")`);
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
  textarea.addEventListener('selectionchange', function () {
    if (instance.isApplyingRemoteOperation) {
      console.log(`⏭️ [${instance.id}] Skipping cursor tracking - applying remote operation`);
      return;
    }

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
      console.log(`  Old: "${oldText}" (length: ${oldText.length}) (selected "${selectedText}" at ${oldSelectionStart}-${oldSelectionEnd})`);
      console.log(`  New: "${newText}" (length: ${newText.length}) (inserted "${insertedText}" length: ${insertedText.length})`);

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
window.initEnhancedEditor = function (editorId, initialContent, componentRef) {
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
