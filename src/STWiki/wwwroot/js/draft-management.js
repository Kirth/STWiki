// draft-management.js
// Draft discard functionality and unsaved changes detection

(function() {
'use strict';

console.log('🚀 Script block started - setting up draft management...');

// Local state for tracking unsaved changes
let hasUnsavedChanges = false;
let originalContent = '';
let currentPageId = null;

// Initialize draft management with the page ID
window.initializeDraftManagement = function(pageId) {
    console.log('📝 Initializing draft management for page:', pageId);
    currentPageId = pageId;
    
    // Set up unsaved changes detection
    setupChangeTracking();
    setupBeforeUnloadWarning();
    
    console.log('📝 Defining full discardDraft function with race condition prevention...');
    
    window.discardDraft = async function discardDraft() {
        console.log('🎯 discardDraft function called!');
        if (!confirm('Are you sure you want to discard your draft? This will permanently remove any uncommitted changes and reload the last committed version.')) {
            return;
        }
        
        if (!pageId) {
            alert('Cannot discard draft - page ID not found');
            return;
        }
        
        // Step 1: Coordinate with Blazor editor to pause autosave and collaboration
        try {
            const editorContainer = document.querySelector('.editor-container');
            if (editorContainer && editorContainer._blazorComponentRef) {
                console.log('🚫 Calling BeginDiscardDraft to pause automatic operations...');
                await editorContainer._blazorComponentRef.invokeMethodAsync('BeginDiscardDraft');
                console.log('✅ Automatic operations paused');
            }
        } catch (error) {
            console.warn('Could not coordinate with Blazor editor:', error);
            // Continue anyway - the server-side changes are most important
        }
        
        // Step 2: Call API to discard draft
        try {
            const response = await fetch(`/api/wiki/${pageId}/discard-draft`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
            });
            
            const data = await response.json();
            
            if (data.message && data.content !== undefined) {
                // Hide the draft indicator
                const draftIndicator = document.getElementById('draft-indicator');
                if (draftIndicator) {
                    draftIndicator.style.display = 'none';
                }
                
                // Step 3: Update editor content immediately
                try {
                    console.log('Updating editor content with discarded draft result...');
                    
                    // Method 1: Use the new wiki editor content setter if available
                    const editorContainer = document.querySelector('.editor-container');
                    if (editorContainer && window.setWikiEditorContent) {
                        const containerId = editorContainer.id || 'editor-container';
                        console.log('Using setWikiEditorContent for container:', containerId);
                        window.setWikiEditorContent(containerId, data.content);
                        console.log('✅ Updated editor content using setWikiEditorContent');
                    }
                    // Method 2: Fallback to enhanced editor if available
                    else if (window.setEnhancedEditorContent) {
                        // Find the correct enhanced editor textarea
                        const editorTextarea = document.querySelector('textarea[id*="simple-editor-"]') || 
                                             document.querySelector('textarea[id*="editor-"]') ||
                                             document.querySelector('[data-role="editor"]');
                        
                        if (editorTextarea) {
                            console.log('Using setEnhancedEditorContent for editor:', editorTextarea.id);
                            window.setEnhancedEditorContent(editorTextarea.id, data.content);
                            console.log('✅ Updated editor content using setEnhancedEditorContent');
                        }
                    }
                    
                    // Always update the hidden textarea (for form submission)
                    const bodyTextarea = document.getElementById('body-textarea');
                    if (bodyTextarea) {
                        bodyTextarea.value = data.content;
                        console.log('✅ Updated hidden body textarea');
                    }
                    
                    // Also sync the form field (ASP.NET Core model binding target)
                    const bodyField = document.querySelector('textarea[name="Body"]');
                    if (bodyField && bodyField !== bodyTextarea) {
                        bodyField.value = data.content;
                        console.log('✅ Updated form Body field');
                    }
                    
                    console.log('✅ Updated all editor fields with reverted content');
                } catch (error) {
                    console.error('Could not update editor content directly:', error);
                }
                
                // Step 4: Signal completion to Blazor editor
                try {
                    const editorContainer = document.querySelector('.editor-container');
                    if (editorContainer && editorContainer._blazorComponentRef) {
                        console.log('✅ Calling EndDiscardDraft to signal completion...');
                        await editorContainer._blazorComponentRef.invokeMethodAsync('EndDiscardDraft');
                        console.log('✅ Draft discard coordination completed');
                    }
                } catch (error) {
                    console.warn('Could not signal completion to Blazor editor:', error);
                }
                
                // Show success message
                window.showToast(data.message, 'success');
                
                // Optional: Reload page to ensure everything is fully synced (reduced delay since we updated content directly)
                setTimeout(() => {
                    window.location.href = window.location.href;
                }, 800);
            } else if (data.error) {
                window.showToast(data.error, 'error');
            }
        } catch (error) {
            console.error('Error discarding draft:', error);
            
            // Even on error, signal completion to Blazor editor to restore normal operations
            try {
                const editorContainer = document.querySelector('.editor-container');
                if (editorContainer && editorContainer._blazorComponentRef) {
                    console.log('❌ Error occurred - calling EndDiscardDraft to restore operations...');
                    await editorContainer._blazorComponentRef.invokeMethodAsync('EndDiscardDraft');
                    console.log('✅ Normal operations restored after error');
                }
            } catch (coordError) {
                console.warn('Could not restore operations after error:', coordError);
            }
            
            window.showToast('Failed to discard draft', 'error');
        }
    };
    
    console.log('✅ Full discardDraft function defined. Type:', typeof window.discardDraft);
};

// Set up change tracking for the editor
function setupChangeTracking() {
    console.log('🎯 Setting up change tracking...');
    
    // Get initial content from editor
    setTimeout(() => {
        const editorContainer = document.querySelector('.editor-container');
        if (editorContainer && window.getWikiEditorContent) {
            const containerId = editorContainer.id || 'editor-container';
            originalContent = window.getWikiEditorContent(containerId) || '';
            console.log('📝 Original content captured:', originalContent.length, 'characters');
        }
        
        // Set up periodic change checking
        setInterval(checkForChanges, 1000); // Check every second
    }, 1000); // Wait for editor to be fully initialized
}

// Check if editor content has changed
function checkForChanges() {
    const editorContainer = document.querySelector('.editor-container');
    if (!editorContainer || !window.getWikiEditorContent) return;
    
    const containerId = editorContainer.id || 'editor-container';
    const currentContent = window.getWikiEditorContent(containerId) || '';
    
    const wasChanged = hasUnsavedChanges;
    hasUnsavedChanges = currentContent !== originalContent;
    
    // Log when state changes
    if (hasUnsavedChanges !== wasChanged) {
        console.log('🔄 Change state updated:', hasUnsavedChanges ? 'HAS UNSAVED CHANGES' : 'NO UNSAVED CHANGES');
        updateChangeIndicator();
    }
}

// Update visual indicator of unsaved changes
function updateChangeIndicator() {
    const form = document.querySelector('form');
    const saveButton = document.querySelector('button[type="submit"]');
    
    if (hasUnsavedChanges) {
        // Add visual indicators
        if (form) {
            form.style.border = '2px solid orange';
            form.style.borderRadius = '5px';
        }
        if (saveButton) {
            saveButton.style.backgroundColor = '#fd7e14';
            saveButton.textContent = saveButton.textContent.replace(/^(Create|Save)/, '$1*');
        }
    } else {
        // Remove visual indicators
        if (form) {
            form.style.border = '';
            form.style.borderRadius = '';
        }
        if (saveButton) {
            saveButton.style.backgroundColor = '';
            saveButton.textContent = saveButton.textContent.replace(/\*$/, '');
        }
    }
}

// Set up beforeunload warning
function setupBeforeUnloadWarning() {
    console.log('⚠️ Setting up beforeunload warning...');
    
    window.addEventListener('beforeunload', function(e) {
        if (hasUnsavedChanges) {
            console.log('⚠️ Preventing navigation - unsaved changes detected');
            // Standard way to trigger browser's leave confirmation dialog
            e.preventDefault();
            e.returnValue = 'You have unsaved changes. Are you sure you want to leave?';
            return 'You have unsaved changes. Are you sure you want to leave?';
        }
    });
}

// Mark content as saved (called after successful save operations)
window.markContentAsSaved = function() {
    console.log('✅ Content marked as saved');
    hasUnsavedChanges = false;
    
    // Update original content to current content
    const editorContainer = document.querySelector('.editor-container');
    if (editorContainer && window.getWikiEditorContent) {
        const containerId = editorContainer.id || 'editor-container';
        originalContent = window.getWikiEditorContent(containerId) || '';
    }
    
    updateChangeIndicator();
};

// Accept the current draft (just hide the prompt)
window.acceptDraft = function() {
    console.log('✅ User accepted draft content');
    hideDraftPrompt();
    window.showToast('Continuing with draft content', 'success');
};

// Hide the draft prompt
window.hideDraftPrompt = function() {
    console.log('👁️ Hiding draft prompt');
    const draftIndicator = document.getElementById('draft-indicator');
    if (draftIndicator) {
        draftIndicator.style.display = 'none';
    }
};

window.showToast = function showToast(message, type = 'success') {
    const toast = document.createElement('div');
    toast.className = 'toast-notification';
    toast.textContent = message;
    toast.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        background: ${type === 'success' ? '#198754' : '#dc3545'};
        color: white;
        padding: 0.75rem 1rem;
        border-radius: 0.375rem;
        z-index: 1050;
        animation: slideIn 0.3s ease-out;
    `;
    document.body.appendChild(toast);
    
    setTimeout(() => {
        toast.style.animation = 'slideOut 0.3s ease-out';
        setTimeout(() => document.body.removeChild(toast), 300);
    }, 3000);
}

// Add the slideIn/slideOut animations
if (!document.getElementById('draft-animations')) {
    const style = document.createElement('style');
    style.id = 'draft-animations';
    style.textContent = `
        @keyframes slideIn {
            from { transform: translateX(100%); opacity: 0; }
            to   { transform: translateX(0);    opacity: 1; }
        }
        @keyframes slideOut {
            from { transform: translateX(0);    opacity: 1; }
            to   { transform: translateX(100%); opacity: 0; }
        }
    `;
    document.head.appendChild(style);
}

})(); // End of IIFE