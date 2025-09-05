class MediaLibrary {
    constructor() {
        this.selectedFiles = [];
        this.currentPage = 1;
        this.hasMore = true;
        this.isLoading = false;
        this.currentMediaId = null;
        this.uploadCompleteCallback = null;
        
        this.initializeEventListeners();
        
        // Only load media if we're on the media library page (has media grid)
        const mediaGrid = document.getElementById('mediaGrid');
        if (mediaGrid) {
            this.loadMedia();
        }
    }

    initializeEventListeners() {
        const dropzone = document.getElementById('dropzone');
        const fileInput = document.getElementById('fileInput');
        const searchInput = document.getElementById('searchInput');
        
        if (!dropzone || !fileInput) {
            console.warn('⚠️ [MediaLibrary] Required elements missing, skipping event listeners');
            return;
        }

        // Drag and drop events
        dropzone.addEventListener('dragover', (e) => {
            e.preventDefault();
            dropzone.classList.add('border-primary', 'bg-light');
        });

        dropzone.addEventListener('dragleave', () => {
            dropzone.classList.remove('border-primary', 'bg-light');
        });

        dropzone.addEventListener('drop', (e) => {
            e.preventDefault();
            dropzone.classList.remove('border-primary', 'bg-light');
            this.handleFiles(e.dataTransfer.files);
        });

        // File input change
        fileInput.addEventListener('change', (e) => {
            this.handleFiles(e.target.files);
        });

        // Click to select files (but avoid conflict with button)
        dropzone.addEventListener('click', (e) => {
            // Only trigger if clicking the dropzone itself, not the button inside it
            if (e.target === dropzone || dropzone.contains(e.target) && !e.target.closest('button')) {
                fileInput.click();
            }
        });

        // Search functionality (only if search input exists)
        if (searchInput) {
            searchInput.addEventListener('input', this.debounce(() => {
                this.searchMedia();
            }, 300));
        }
    }

    handleFiles(files) {
        this.selectedFiles = Array.from(files);
        this.displaySelectedFiles();
        document.getElementById('uploadBtn').disabled = this.selectedFiles.length === 0;
    }

    displaySelectedFiles() {
        const fileList = document.getElementById('fileList');
        fileList.innerHTML = '';

        if (this.selectedFiles.length === 0) return;

        this.selectedFiles.forEach((file, index) => {
            const fileItem = document.createElement('div');
            fileItem.className = 'card mb-2';
            fileItem.innerHTML = `
                <div class="card-body py-2">
                    <div class="d-flex align-items-center">
                        <i class="bi bi-${this.getFileIcon(file.type)} me-2"></i>
                        <div class="flex-grow-1">
                            <div class="fw-medium">${this.escapeHtml(file.name)}</div>
                            <small class="text-muted">${this.formatFileSize(file.size)} • ${file.type}</small>
                        </div>
                        <button type="button" class="btn btn-sm btn-outline-danger" onclick="mediaLibrary.removeFile(${index})">
                            <i class="bi bi-x"></i>
                        </button>
                    </div>
                    <div class="mt-2">
                        <input type="text" class="form-control form-control-sm mb-1" 
                               placeholder="Filename" 
                               data-file-index="${index}" 
                               data-field="filename"
                               value="${this.escapeHtml(this.getFilenameWithoutExtension(file.name))}"
                               required>
                        <input type="text" class="form-control form-control-sm mb-1" 
                               placeholder="Description (optional)" 
                               data-file-index="${index}" 
                               data-field="description"
                               maxlength="1000">
                        <input type="text" class="form-control form-control-sm" 
                               placeholder="Alt text (optional)" 
                               data-file-index="${index}" 
                               data-field="altText"
                               maxlength="500">
                    </div>
                </div>
            `;
            fileList.appendChild(fileItem);
        });
    }

    async uploadFiles() {
        if (this.selectedFiles.length === 0) return;

        const uploadBtn = document.getElementById('uploadBtn');
        const progressContainer = document.getElementById('uploadProgress');
        const progressBar = progressContainer.querySelector('.progress-bar');
        const statusDiv = document.getElementById('uploadStatus');

        uploadBtn.disabled = true;
        progressContainer.style.display = 'block';
        
        let completed = 0;
        let failed = 0;
        const total = this.selectedFiles.length;
        const uploadedFiles = []; // Collect successful uploads for callback

        for (const [index, file] of this.selectedFiles.entries()) {
            try {
                statusDiv.textContent = `Uploading ${file.name}...`;
                
                const formData = new FormData();
                formData.append('file', file);
                
                const filenameInput = document.querySelector(`input[data-file-index="${index}"][data-field="filename"]`);
                const descriptionInput = document.querySelector(`input[data-file-index="${index}"][data-field="description"]`);
                const altTextInput = document.querySelector(`input[data-file-index="${index}"][data-field="altText"]`);
                
                if (filenameInput?.value?.trim()) {
                    formData.append('filename', filenameInput.value.trim());
                }
                if (descriptionInput?.value) {
                    formData.append('description', descriptionInput.value);
                }
                if (altTextInput?.value) {
                    formData.append('altText', altTextInput.value);
                }

                const response = await fetch('/api/media/upload', {
                    method: 'POST',
                    body: formData
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Upload failed');
                }

                const uploadResult = await response.json();
                uploadedFiles.push({
                    originalFile: file,
                    fileName: uploadResult.fileName,
                    url: uploadResult.url,
                    id: uploadResult.id,
                    filename: filenameInput?.value?.trim() || '',
                    description: descriptionInput?.value || '',
                    altText: altTextInput?.value || ''
                });

                completed++;
            } catch (error) {
                console.error(`Failed to upload ${file.name}:`, error);
                failed++;
                this.showErrorMessage(`Failed to upload ${file.name}: ${error.message}`);
            }

            const progress = ((completed + failed) / total) * 100;
            progressBar.style.width = `${progress}%`;
        }

        const message = failed > 0 
            ? `Uploaded ${completed} of ${total} files (${failed} failed)`
            : `Successfully uploaded ${completed} file${completed !== 1 ? 's' : ''}`;
        
        statusDiv.textContent = message;
        
        if (completed > 0) {
            setTimeout(() => {
                const modalElement = document.getElementById('uploadModal');
                const modal = bootstrap.Modal.getInstance(modalElement);
                modal.hide();
                this.resetUploadForm();
                
                // Call callback if provided (for editor integration)
                if (this.uploadCompleteCallback) {
                    console.log('📤 [MediaLibrary] Invoking upload complete callback with', uploadedFiles.length, 'files');
                    this.uploadCompleteCallback(uploadedFiles);
                    this.uploadCompleteCallback = null;
                }
                
                this.refreshMedia();
            }, 2000);
        } else {
            uploadBtn.disabled = false;
        }
    }

    resetUploadForm() {
        this.selectedFiles = [];
        document.getElementById('fileList').innerHTML = '';
        document.getElementById('uploadProgress').style.display = 'none';
        document.getElementById('uploadBtn').disabled = true;
        document.querySelector('.progress-bar').style.width = '0%';
        document.getElementById('fileInput').value = '';
    }

    async loadMedia(reset = false) {
        if (this.isLoading) return;
        
        this.isLoading = true;
        const loadMoreBtn = document.getElementById('loadMoreBtn');
        const loadingSpinner = document.getElementById('loadingSpinner');
        
        loadMoreBtn.style.display = 'none';
        loadingSpinner.style.display = reset ? 'block' : 'none';

        try {
            const searchTerm = document.getElementById('searchInput').value;
            const url = `/api/media?page=${this.currentPage}&pageSize=20${searchTerm ? `&search=${encodeURIComponent(searchTerm)}` : ''}`;
            
            const response = await fetch(url);
            if (!response.ok) throw new Error('Failed to load media');
            
            const data = await response.json();
            
            if (reset) {
                document.getElementById('mediaGrid').innerHTML = '';
                document.getElementById('noMediaMessage').style.display = 'none';
            }
            
            if (data.items.length === 0 && reset) {
                document.getElementById('noMediaMessage').style.display = 'block';
            } else {
                this.displayMediaItems(data.items);
                this.hasMore = data.hasMore;
                
                if (this.hasMore) {
                    loadMoreBtn.style.display = 'block';
                }
            }
            
        } catch (error) {
            console.error('Failed to load media:', error);
            this.showErrorMessage('Failed to load media files');
        } finally {
            this.isLoading = false;
            loadingSpinner.style.display = 'none';
        }
    }

    displayMediaItems(items) {
        const grid = document.getElementById('mediaGrid');
        
        items.forEach(item => {
            const col = document.createElement('div');
            col.className = 'col-xl-2 col-lg-3 col-md-4 col-sm-6';
            
            const isImage = item.contentType.startsWith('image/');
            const thumbnailUrl = item.thumbnailUrl || item.url;
            
            col.innerHTML = `
                <div class="card h-100 media-item" data-id="${item.id}">
                    <div class="card-img-top media-thumbnail" style="height: 200px; background: #f8f9fa; display: flex; align-items: center; justify-content: center; cursor: pointer;" onclick="mediaLibrary.showMediaDetails('${item.id}')">
                        ${isImage ? 
                            `<img src="${thumbnailUrl}" alt="${this.escapeHtml(item.fileName)}" class="img-fluid" style="max-height: 100%; max-width: 100%; object-fit: cover;">` :
                            `<i class="bi bi-${this.getFileIcon(item.contentType)} display-4 text-muted"></i>`
                        }
                    </div>
                    <div class="card-body p-2">
                        <h6 class="card-title small mb-1 text-truncate" title="${this.escapeHtml(item.fileName)}">${this.escapeHtml(item.fileName)}</h6>
                        <div class="d-flex justify-content-between align-items-center">
                            <small class="text-muted">${this.formatFileSize(item.fileSize)}</small>
                            <div class="btn-group btn-group-sm">
                                <button type="button" class="btn btn-outline-primary btn-sm" onclick="mediaLibrary.copyMediaLink('${item.id}', '${this.escapeHtml(item.fileName)}')" title="Copy Link">
                                    <i class="bi bi-link"></i>
                                </button>
                                <button type="button" class="btn btn-outline-secondary btn-sm" onclick="mediaLibrary.showMediaDetails('${item.id}')" title="Details">
                                    <i class="bi bi-info-circle"></i>
                                </button>
                                <button type="button" class="btn btn-outline-danger btn-sm" onclick="mediaLibrary.deleteMedia('${item.id}')" title="Delete">
                                    <i class="bi bi-trash"></i>
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            
            grid.appendChild(col);
        });
    }

    async showMediaDetails(mediaId) {
        this.currentMediaId = mediaId;
        const modalElement = document.getElementById('mediaDetailsModal');
        const modal = new bootstrap.Modal(modalElement);
        
        // Find the media item in the current grid
        const mediaItem = document.querySelector(`[data-id="${mediaId}"]`);
        if (!mediaItem) return;

        const fileName = mediaItem.querySelector('.card-title').textContent;
        const fileSize = mediaItem.querySelector('.text-muted').textContent.split(' • ')[0];
        
        document.getElementById('mediaDetailsContent').innerHTML = `
            <div class="mb-3">
                <label for="mediaDescription" class="form-label">Description</label>
                <textarea class="form-control" id="mediaDescription" rows="3" placeholder="Enter description..."></textarea>
            </div>
            <div class="mb-3">
                <label for="mediaAltText" class="form-label">Alt Text</label>
                <input type="text" class="form-control" id="mediaAltText" placeholder="Enter alt text...">
            </div>
            <div class="mb-3">
                <strong>File Name:</strong> ${this.escapeHtml(fileName)}<br>
                <strong>File Size:</strong> ${fileSize}<br>
                <strong>Media Link:</strong> <code>[[media:${this.escapeHtml(fileName)}]]</code>
            </div>
        `;
        
        modal.show();
    }

    async saveMediaDetails() {
        if (!this.currentMediaId) return;

        const description = document.getElementById('mediaDescription').value;
        const altText = document.getElementById('mediaAltText').value;

        try {
            const response = await fetch(`/api/media/${this.currentMediaId}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ description, altText })
            });

            if (!response.ok) throw new Error('Failed to update media');

            const modalElement = document.getElementById('mediaDetailsModal');
            const modal = bootstrap.Modal.getInstance(modalElement);
            modal.hide();
            
            this.showSuccessMessage('Media details updated successfully');
        } catch (error) {
            console.error('Failed to update media:', error);
            this.showErrorMessage('Failed to update media details');
        }
    }

    async deleteMediaFile() {
        if (!this.currentMediaId || !confirm('Are you sure you want to delete this media file?')) return;

        try {
            const response = await fetch(`/api/media/${this.currentMediaId}`, { 
                method: 'DELETE' 
            });
            
            if (response.ok) {
                const modalElement = document.getElementById('mediaDetailsModal');
                const modal = bootstrap.Modal.getInstance(modalElement);
                modal.hide();
                
                // Remove from DOM
                const mediaItem = document.querySelector(`[data-id="${this.currentMediaId}"]`).closest('.col-xl-2');
                mediaItem.remove();
                
                this.showSuccessMessage('Media file deleted successfully');
            } else {
                throw new Error('Failed to delete media file');
            }
        } catch (error) {
            console.error('Delete failed:', error);
            this.showErrorMessage('Failed to delete media file');
        }
    }

    searchMedia() {
        this.currentPage = 1;
        this.hasMore = true;
        this.loadMedia(true);
    }

    refreshMedia() {
        this.currentPage = 1;
        this.hasMore = true;
        this.loadMedia(true);
    }

    loadMoreMedia() {
        if (!this.hasMore || this.isLoading) return;
        this.currentPage++;
        this.loadMedia();
    }

    copyMediaLink(mediaId, fileName) {
        const link = `[[media:${fileName}]]`;
        navigator.clipboard.writeText(link).then(() => {
            this.showSuccessMessage('Media link copied to clipboard');
        }).catch(() => {
            // Fallback for older browsers
            const textArea = document.createElement('textarea');
            textArea.value = link;
            document.body.appendChild(textArea);
            textArea.select();
            document.execCommand('copy');
            document.body.removeChild(textArea);
            this.showSuccessMessage('Media link copied to clipboard');
        });
    }

    async deleteMedia(mediaId) {
        if (!confirm('Are you sure you want to delete this media file?')) return;
        
        try {
            const response = await fetch(`/api/media/${mediaId}`, { method: 'DELETE' });
            if (response.ok) {
                const mediaItem = document.querySelector(`[data-id="${mediaId}"]`).closest('.col-xl-2');
                mediaItem.remove();
                this.showSuccessMessage('Media file deleted successfully');
            } else {
                throw new Error('Failed to delete media file');
            }
        } catch (error) {
            console.error('Delete failed:', error);
            this.showErrorMessage('Failed to delete media file');
        }
    }

    getFileIcon(contentType) {
        if (contentType.startsWith('image/')) return 'file-earmark-image';
        if (contentType.includes('pdf')) return 'file-earmark-pdf';
        if (contentType.includes('word') || contentType.includes('document')) return 'file-earmark-word';
        if (contentType.includes('excel') || contentType.includes('spreadsheet')) return 'file-earmark-excel';
        if (contentType.includes('powerpoint') || contentType.includes('presentation')) return 'file-earmark-ppt';
        if (contentType.startsWith('text/')) return 'file-earmark-text';
        return 'file-earmark';
    }

    getFilenameWithoutExtension(fileName) {
        const lastDotIndex = fileName.lastIndexOf('.');
        return lastDotIndex > 0 ? fileName.substring(0, lastDotIndex) : fileName;
    }

    formatFileSize(bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }

    removeFile(index) {
        this.selectedFiles.splice(index, 1);
        this.displaySelectedFiles();
        document.getElementById('uploadBtn').disabled = this.selectedFiles.length === 0;
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    showSuccessMessage(message) {
        this.showToast(message, 'success');
    }

    showErrorMessage(message) {
        this.showToast(message, 'danger');
    }

    showToast(message, type) {
        const toastContainer = document.getElementById('toast-container') || this.createToastContainer();
        
        const toast = document.createElement('div');
        toast.className = `toast align-items-center text-white bg-${type} border-0`;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">${message}</div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        `;
        
        toastContainer.appendChild(toast);
        
        const bsToast = new bootstrap.Toast(toast);
        bsToast.show();
        
        toast.addEventListener('hidden.bs.toast', () => {
            toast.remove();
        });
    }

    createToastContainer() {
        const container = document.createElement('div');
        container.id = 'toast-container';
        container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
        container.style.zIndex = '1055';
        document.body.appendChild(container);
        return container;
    }
}

// Global functions for onclick handlers
function searchMedia() {
    mediaLibrary.searchMedia();
}

function loadMoreMedia() {
    mediaLibrary.loadMoreMedia();
}

function uploadFiles() {
    mediaLibrary.uploadFiles();
}

function saveMediaDetails() {
    mediaLibrary.saveMediaDetails();
}

function deleteMediaFile() {
    mediaLibrary.deleteMediaFile();
}

// Initialize when page loads
let mediaLibrary;
document.addEventListener('DOMContentLoaded', () => {
    // Check if we have the required elements (they exist on both media library page and edit pages)
    const dropzone = document.getElementById('dropzone');
    const fileInput = document.getElementById('fileInput');
    
    if (dropzone && fileInput) {
        mediaLibrary = new MediaLibrary();
        console.log('✅ [MediaLibrary] Initialized successfully');
    } else {
        console.log('⚠️ [MediaLibrary] Required elements not found, skipping initialization');
    }
});

// Global function to open upload modal with pre-selected files (for editor integration)
window.openUploadModalWithFiles = function(files, callback) {
    // Ensure media library is initialized
    if (!mediaLibrary) {
        console.log('📁 [MediaLibrary] Not initialized yet, initializing now...');
        
        // Check if we have the required elements
        const dropzone = document.getElementById('dropzone');
        const fileInput = document.getElementById('fileInput');
        
        if (dropzone && fileInput) {
            mediaLibrary = new MediaLibrary();
            console.log('✅ [MediaLibrary] Initialized on-demand');
        } else {
            console.error('❌ [MediaLibrary] Cannot initialize - required elements not found');
            return;
        }
    }
    
    console.log('📁 [MediaLibrary] Opening upload modal with', files.length, 'pre-selected files');
    
    // Set the callback for when upload completes
    mediaLibrary.uploadCompleteCallback = callback;
    
    // Set the selected files
    mediaLibrary.selectedFiles = Array.from(files);
    
    // Display the files in the form
    mediaLibrary.displaySelectedFiles();
    
    // Enable the upload button
    document.getElementById('uploadBtn').disabled = false;
    
    // Open the modal
    const uploadModal = new bootstrap.Modal(document.getElementById('uploadModal'));
    uploadModal.show();
    
    console.log('✅ [MediaLibrary] Upload modal opened with pre-selected files');
};