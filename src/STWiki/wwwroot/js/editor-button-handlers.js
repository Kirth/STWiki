// editor-button-handlers.js
// Handle EditorSimple button clicks via event delegation

document.addEventListener('click', function(e) {
    const button = e.target.closest('button[data-action]');
    if (!button || button.disabled) {
        console.log('Button click ignored - no action or disabled:', button);
        return;
    }
    
    const action = button.dataset.action;
    console.log('🔘 Button clicked with action:', action);
    
    const editorContainer = button.closest('.editor-container');
    if (!editorContainer) {
        console.error('Editor container not found');
        return;
    }
    
    // Find the .NET component reference (set by initEnhancedEditor)
    const componentRef = editorContainer._blazorComponentRef;
    if (!componentRef) {
        console.error('EditorSimple component reference not found');
        return;
    }
    
    console.log('🚀 Calling .NET method for action:', action);
    
    // Call the appropriate .NET method
    switch (action) {
        case 'save-draft':
            componentRef.invokeMethodAsync('HandleSaveDraft')
                .then(() => console.log('✅ HandleSaveDraft completed'))
                .catch(err => console.error('❌ HandleSaveDraft failed:', err));
            break;
        case 'commit-changes':
            componentRef.invokeMethodAsync('HandleCommitChanges')
                .then(() => console.log('✅ HandleCommitChanges completed'))
                .catch(err => console.error('❌ HandleCommitChanges failed:', err));
            break;
        case 'insert-bold':
            componentRef.invokeMethodAsync('HandleInsertBold')
                .then(() => console.log('✅ HandleInsertBold completed'))
                .catch(err => console.error('❌ HandleInsertBold failed:', err));
            break;
        default:
            console.warn('Unknown action:', action);
    }
});