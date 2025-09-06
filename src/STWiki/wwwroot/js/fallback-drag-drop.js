// fallback-drag-drop.js
// Fallback drag & drop for file uploads when Blazor editor isn't available

document.addEventListener('DOMContentLoaded', function() {
    console.log('🔧 Setting up fallback drag-and-drop for edit page...');
    
    // Check if we have the enhanced editor working
    setTimeout(function() {
        const blazorEditor = document.querySelector('textarea[id*="simple-editor-"]');
        if (blazorEditor) {
            console.log('✅ Blazor editor found, drag-and-drop should be handled by enhanced editor');
            return;
        }
        
        console.log('⚠️ No Blazor editor found, setting up fallback drag-and-drop on body textarea');
        setupFallbackDragAndDrop();
    }, 2000); // Give Blazor time to initialize
});

function setupFallbackDragAndDrop() {
    const bodyTextarea = document.getElementById('body-textarea');
    const editorContainer = document.getElementById('editor-container');
    
    if (!bodyTextarea || !editorContainer) {
        console.error('❌ Required elements not found for fallback drag-and-drop');
        return;
    }
    
    // Make textarea visible for drag-and-drop
    bodyTextarea.classList.remove('d-none');
    bodyTextarea.style.minHeight = '400px';
    bodyTextarea.style.fontFamily = 'Monaco, Menlo, Ubuntu Mono, monospace';
    bodyTextarea.style.fontSize = '14px';
    bodyTextarea.placeholder = 'Start writing your wiki content... (drag images here to upload)';
    
    // Create overlay
    const overlay = document.createElement('div');
    overlay.className = 'drag-drop-overlay d-none';
    overlay.innerHTML = `
        <div class="drag-drop-content">
            <i class="bi bi-cloud-arrow-up display-1 text-primary"></i>
            <h4 class="mt-3">Drop images here to upload</h4>
            <p class="text-muted">Supported formats: JPG, PNG, GIF, WebP</p>
        </div>
    `;
    editorContainer.style.position = 'relative';
    editorContainer.appendChild(overlay);
    
    let dragCounter = 0;
    
    // Use the same global drag state system as the enhanced editor
    if (!window.globalFileDragState) {
        window.globalFileDragState = {
            isDragging: false,
            instances: new Set()
        };
        
        // Add global document listeners only once
        document.addEventListener('dragenter', function(e) {
            if (e.dataTransfer && e.dataTransfer.types.includes('Files')) {
                window.globalFileDragState.isDragging = true;
            }
        }, false);
        
        document.addEventListener('dragleave', function(e) {
            // Only reset if leaving the document entirely
            if (!e.relatedTarget || e.relatedTarget.nodeName === 'HTML') {
                window.globalFileDragState.isDragging = false;
                // Hide all overlays
                window.globalFileDragState.instances.forEach(instanceData => {
                    instanceData.overlay.classList.add('d-none');
                    instanceData.overlay.classList.remove('drag-over');
                    instanceData.dragCounter = 0;
                });
            }
        }, false);
        
        document.addEventListener('drop', function(e) {
            window.globalFileDragState.isDragging = false;
        }, false);
    }
    
    // Register this fallback instance
    const fallbackInstanceData = { overlay, dragCounter: 0 };
    window.globalFileDragState.instances.add(fallbackInstanceData);
    
    // Add event listeners
    bodyTextarea.addEventListener('dragenter', handleDragEnter);
    bodyTextarea.addEventListener('dragover', handleDragOver);
    bodyTextarea.addEventListener('dragleave', handleDragLeave);
    bodyTextarea.addEventListener('drop', handleDrop);
    
    editorContainer.addEventListener('dragenter', handleDragEnter);
    editorContainer.addEventListener('dragover', handleDragOver);
    editorContainer.addEventListener('dragleave', handleDragLeave);
    editorContainer.addEventListener('drop', handleDrop);
    
    function handleDragEnter(e) {
        e.preventDefault();
        e.stopPropagation();
        fallbackInstanceData.dragCounter++;
        
        // Only show overlay if we're dragging files and we're over the editor area
        if (window.globalFileDragState.isDragging && e.dataTransfer.types.includes('Files')) {
            console.log('📁 Fallback: Files detected over editor, showing overlay');
            overlay.classList.remove('d-none');
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
        fallbackInstanceData.dragCounter--;
        
        if (fallbackInstanceData.dragCounter <= 0) {
            console.log('🔼 Fallback: Drag leave editor area, hiding overlay');
            fallbackInstanceData.dragCounter = 0;
            overlay.classList.add('d-none');
        }
    }
    
    function handleDrop(e) {
        console.log('🎯 Fallback drop detected!');
        e.preventDefault();
        e.stopPropagation();
        fallbackInstanceData.dragCounter = 0;
        window.globalFileDragState.isDragging = false;
        
        overlay.classList.add('d-none');
        
        const files = Array.from(e.dataTransfer.files);
        const imageFiles = files.filter(file => file.type.startsWith('image/'));
        
        if (imageFiles.length === 0) {
            alert('Please drop image files only');
            return;
        }
        
        // Show upload modal
        showFallbackUploadModal(imageFiles[0]);
    }
    
    console.log('✅ Fallback drag-and-drop setup complete');
}

function showFallbackUploadModal(file) {
    // Remove existing modal if it exists
    const existingModal = document.getElementById('fallback-upload-modal');
    if (existingModal) {
        existingModal.remove();
    }
    
    // Create modal HTML outside main form
    const modal = document.createElement('div');
    modal.id = 'fallback-upload-modal';
    modal.className = 'modal fade';
    modal.innerHTML = `
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">Upload Image</h5>
                    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                </div>
                <div class="modal-body">
                    <div id="fallback-preview" class="text-center mb-3 d-none">
                        <img id="fallback-preview-img" src="" alt="Preview" class="img-fluid rounded" style="max-height: 200px;">
                    </div>
                    <!-- Separate form for fallback upload modal -->
                    <form id="fallback-upload-form">
                        <div class="mb-3">
                            <label class="form-label">File Name <span class="text-danger">*</span></label>
                            <input type="text" class="form-control" id="fallback-filename" required>
                            <div class="form-text">The name to use for the uploaded file (without extension)</div>
                        </div>
                        <div class="row mb-3">
                            <div class="col-md-6">
                                <label class="form-label">Display Size</label>
                                <select class="form-select" id="fallback-size">
                                    <option value="">Default (600px)</option>
                                    <option value="thumb">Thumbnail (150px)</option>
                                    <option value="300">Small (300px)</option>
                                    <option value="500">Medium (500px)</option>
                                    <option value="full">Full Size</option>
                                </select>
                            </div>
                            <div class="col-md-6">
                                <label class="form-label">Alignment</label>
                                <select class="form-select" id="fallback-align">
                                    <option value="">Default</option>
                                    <option value="left">Left</option>
                                    <option value="center">Center</option>
                                    <option value="right">Right</option>
                                </select>
                            </div>
                        </div>
                        <div class="mb-3">
                            <label class="form-label">Description/Caption</label>
                            <textarea class="form-control" id="fallback-description" rows="2"></textarea>
                        </div>
                        <div class="mb-3">
                            <label class="form-label">Alt Text</label>
                            <input type="text" class="form-control" id="fallback-alttext">
                        </div>
                    </form>
                    <div class="progress d-none" id="fallback-progress">
                        <div class="progress-bar" role="progressbar"></div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                    <button type="button" class="btn btn-primary" id="fallback-upload-btn">Upload & Insert</button>
                </div>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
    
    // Set up modal content
    const preview = document.getElementById('fallback-preview');
    const previewImg = document.getElementById('fallback-preview-img');
    const filenameInput = document.getElementById('fallback-filename');
    const descriptionInput = document.getElementById('fallback-description');
    const altTextInput = document.getElementById('fallback-alttext');
    const sizeSelect = document.getElementById('fallback-size');
    const alignSelect = document.getElementById('fallback-align');
    const uploadBtn = document.getElementById('fallback-upload-btn');
    
    // Show preview
    const reader = new FileReader();
    reader.onload = function(e) {
        previewImg.src = e.target.result;
        preview.classList.remove('d-none');
    };
    reader.readAsDataURL(file);
    
    // Set default filename
    filenameInput.value = file.name.replace(/\.[^/.]+$/, '');
    descriptionInput.value = '';
    altTextInput.value = '';
    sizeSelect.value = '';
    alignSelect.value = '';
    
    // Handle upload with form validation
    uploadBtn.onclick = function() {
        const uploadForm = document.getElementById('fallback-upload-form');
        if (uploadForm.checkValidity()) {
            uploadFallbackImage(file, {
                filename: filenameInput.value.trim(),
                description: descriptionInput.value.trim(),
                altText: altTextInput.value.trim(),
                size: sizeSelect.value,
                align: alignSelect.value
            });
        } else {
            uploadForm.reportValidity();
        }
    };
    
    // Clean up modal when hidden
    modal.addEventListener('hidden.bs.modal', function() {
        this.remove();
    });
    
    // Show modal
    new bootstrap.Modal(modal).show();
}

async function uploadFallbackImage(file, metadata) {
    const progressContainer = document.getElementById('fallback-progress');
    const progressBar = progressContainer.querySelector('.progress-bar');
    const uploadBtn = document.getElementById('fallback-upload-btn');
    
    progressContainer.classList.remove('d-none');
    uploadBtn.disabled = true;
    
    try {
        const formData = new FormData();
        formData.append('file', file);
        
        if (metadata.filename) {
            formData.append('filename', metadata.filename);
        }
        if (metadata.description) formData.append('description', metadata.description);
        if (metadata.altText) formData.append('altText', metadata.altText);
        
        const response = await fetch('/api/media/upload', {
            method: 'POST',
            body: formData
        });
        
        if (!response.ok) {
            throw new Error('Upload failed');
        }
        
        const result = await response.json();
        progressBar.style.width = '100%';
        
        // Insert into textarea with parameters
        const bodyTextarea = document.getElementById('body-textarea');
        const cursorPos = bodyTextarea.selectionStart;
        const content = bodyTextarea.value;
        
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
        
        const newContent = content.substring(0, cursorPos) + mediaTemplate + content.substring(cursorPos);
        bodyTextarea.value = newContent;
        bodyTextarea.focus();
        bodyTextarea.selectionStart = bodyTextarea.selectionEnd = cursorPos + mediaTemplate.length;
        
        // Close modal
        bootstrap.Modal.getInstance(document.getElementById('fallback-upload-modal')).hide();
        
        // Show success message
        alert(`Image "${result.fileName}" uploaded and inserted successfully!`);
        
    } catch (error) {
        console.error('Upload failed:', error);
        alert('Upload failed: ' + error.message);
    } finally {
        progressContainer.classList.add('d-none');
        uploadBtn.disabled = false;
    }
}