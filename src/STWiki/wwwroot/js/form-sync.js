// form-sync.js
// Sync editor content with hidden textarea on form submission

document.addEventListener('DOMContentLoaded', function() {
    const form = document.querySelector('form');
    if (form) {
        form.addEventListener('submit', function(e) {
            try {
                console.log('🔄 Form submission detected, syncing editor content...');
                
                const bodyTextarea = document.getElementById('body-textarea');
                if (!bodyTextarea) {
                    console.error('❌ Body textarea not found');
                    return;
                }
                
                console.log('📋 Current body textarea value before sync:', bodyTextarea.value.length, 'characters');
                
                // Try multiple methods to find the active editor content
                let editorContent = '';
                let syncMethod = '';
                
                // Method 1: Use the new wiki editor content getter if available
                const editorContainer = document.querySelector('.editor-container');
                if (editorContainer && window.getWikiEditorContent) {
                    const containerId = editorContainer.id || 'editor-container';
                    editorContent = window.getWikiEditorContent(containerId) || '';
                    if (editorContent) {
                        syncMethod = `getWikiEditorContent: ${containerId}`;
                    }
                }
                
                // Method 2: Look for enhanced editor textarea in editor container
                if (!editorContent && editorContainer) {
                    // Try various selectors for the editor textarea
                    const selectors = [
                        'textarea[id*="simple-editor-"]',
                        'textarea[id*="editor-"]', 
                        'textarea.enhanced-editor',
                        'textarea:not(#body-textarea)',
                        'textarea'
                    ];
                    
                    for (const selector of selectors) {
                        const activeEditor = editorContainer.querySelector(selector);
                        if (activeEditor && activeEditor.id !== 'body-textarea') {
                            editorContent = activeEditor.value || '';
                            syncMethod = `container selector: ${selector}`;
                            break;
                        }
                    }
                }
                
                // Method 3: Use the enhanced editor global function if available
                if (!editorContent && typeof getEnhancedEditorContent === 'function') {
                    // Try to find any editor instance
                    const editorTextareas = document.querySelectorAll('textarea:not(#body-textarea)');
                    for (const textarea of editorTextareas) {
                        if (textarea.id && textarea.id.includes('editor')) {
                            editorContent = getEnhancedEditorContent(textarea.id) || '';
                            syncMethod = `getEnhancedEditorContent: ${textarea.id}`;
                            break;
                        }
                    }
                }
                
                // Method 4: Try to get content from any visible textarea that's not the hidden one
                if (!editorContent) {
                    const visibleTextareas = Array.from(document.querySelectorAll('textarea:not(#body-textarea)'))
                        .filter(ta => ta.offsetParent !== null && ta.value && ta.value.trim());
                        
                    if (visibleTextareas.length > 0) {
                        editorContent = visibleTextareas[0].value;
                        syncMethod = `visible textarea: ${visibleTextareas[0].id || 'unnamed'}`;
                    }
                }
                
                // Update the form textarea
                if (editorContent) {
                    bodyTextarea.value = editorContent;
                    console.log(`✅ Synced editor content to form textarea (${syncMethod}):`, editorContent.length, 'characters');
                } else {
                    console.warn('⚠️ No editor content found to sync, form textarea remains:', bodyTextarea.value.length, 'characters');
                    
                    // Debug: list all textareas
                    const allTextareas = document.querySelectorAll('textarea');
                    console.log('📋 All textareas found:', Array.from(allTextareas).map(ta => ({
                        id: ta.id,
                        className: ta.className,
                        visible: ta.offsetParent !== null,
                        contentLength: ta.value?.length || 0
                    })));
                }
                
                // Client-side validation: ensure Body field has content
                if (!bodyTextarea.value || bodyTextarea.value.trim().length === 0) {
                    console.error('❌ Body field is empty after sync, preventing form submission');
                    e.preventDefault();
                    
                    // Show user-friendly error message
                    alert('Error: Content appears to be empty. Please ensure your content is properly loaded in the editor before saving.');
                    
                    // Try one more time to force sync content
                    setTimeout(() => {
                        console.log('🔄 Attempting emergency content sync...');
                        
                        // Try to force sync again
                        let emergencyContent = '';
                        if (window.getWikiEditorContent && editorContainer) {
                            const containerId = editorContainer.id || 'editor-container';
                            emergencyContent = window.getWikiEditorContent(containerId) || '';
                        }
                        
                        if (emergencyContent) {
                            bodyTextarea.value = emergencyContent;
                            console.log('✅ Emergency sync successful, content length:', emergencyContent.length);
                            alert('Content has been recovered. Please try saving again.');
                        } else {
                            console.log('❌ Emergency sync failed - no content available');
                        }
                    }, 100);
                    
                    return false;
                }
            } catch (error) {
                console.error('❌ Error syncing editor content:', error);
            }
        });
    }
});

// Function called when Blazor editor commits changes
window.markContentAsCommitted = function() {
    console.log('📝 Content committed via API - updating form button');
    
    const saveButton = document.querySelector('button[type="submit"]');
    if (saveButton) {
        // Change button text to indicate content was already saved
        const originalText = saveButton.textContent;
        saveButton.textContent = 'Already Saved via Editor';
        saveButton.classList.add('btn-secondary');
        saveButton.classList.remove('btn-success');
        
        // Add warning class to form to indicate potential conflict
        const form = document.querySelector('form');
        if (form) {
            form.style.border = '2px solid orange';
            form.style.borderRadius = '5px';
            form.style.padding = '10px';
            
            // Add warning message if not already present
            let warningDiv = document.getElementById('commit-warning');
            if (!warningDiv) {
                warningDiv = document.createElement('div');
                warningDiv.id = 'commit-warning';
                warningDiv.className = 'alert alert-warning';
                warningDiv.innerHTML = '<strong>⚠️ Content already saved!</strong> You used "Commit Changes" in the editor. Using "Save Changes" again may overwrite your API changes.';
                form.insertBefore(warningDiv, form.firstChild);
            }
        }
        
        // Restore button after 10 seconds
        setTimeout(() => {
            saveButton.textContent = originalText;
            saveButton.classList.remove('btn-secondary');
            saveButton.classList.add('btn-success');
        }, 10000);
    }
};