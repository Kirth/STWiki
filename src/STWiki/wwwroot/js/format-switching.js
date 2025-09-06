// format-switching.js
// Markdown/HTML format conversion functionality

document.addEventListener('DOMContentLoaded', function() {
    console.log('🔧 DOM loaded, initializing format switching...');
    
    // Declare variables outside try-catch for proper scope
    let switchToHtmlBtn, switchToMarkdownBtn, formatOptions, confirmBtn, cancelBtn, conversionDescription, bodyFormatSelect;
    let targetFormat = '';
    
    try {
        switchToHtmlBtn = document.getElementById('switch-to-html-btn');
        switchToMarkdownBtn = document.getElementById('switch-to-markdown-btn');
        formatOptions = document.getElementById('format-conversion-options');
        confirmBtn = document.getElementById('confirm-format-switch');
        cancelBtn = document.getElementById('cancel-format-switch');
        conversionDescription = document.getElementById('conversion-description');
        bodyFormatSelect = document.querySelector('select[name="BodyFormat"]');
        
        // Comprehensive debug logging
        console.log('🔧 Elements found:');
        console.log('  switchToHtmlBtn:', switchToHtmlBtn, switchToHtmlBtn ? 'visible: ' + getComputedStyle(switchToHtmlBtn).display : 'null');
        console.log('  switchToMarkdownBtn:', switchToMarkdownBtn, switchToMarkdownBtn ? 'visible: ' + getComputedStyle(switchToMarkdownBtn).display : 'null');
        console.log('  formatOptions:', formatOptions);
        console.log('  confirmBtn:', confirmBtn);
        console.log('  bodyFormatSelect:', bodyFormatSelect, bodyFormatSelect ? 'current value: ' + bodyFormatSelect.value : 'null');
        
        // Show conversion options when switching to HTML
        if (switchToHtmlBtn) {
            console.log('🔧 Attaching click listener to switchToHtmlBtn');
            switchToHtmlBtn.addEventListener('click', function(e) {
                console.log('🔘 HTML button clicked!', e);
                e.preventDefault();
                targetFormat = 'html';
                conversionDescription.textContent = 'Convert Markdown syntax to HTML tags';
                formatOptions.style.display = 'block';
                this.style.display = 'none';
            });
        } else {
            console.warn('⚠️ switchToHtmlBtn not found');
        }
        
        // Show conversion options when switching to Markdown
        if (switchToMarkdownBtn) {
            console.log('🔧 Attaching click listener to switchToMarkdownBtn');
            switchToMarkdownBtn.addEventListener('click', function(e) {
                console.log('🔘 MARKDOWN BUTTON CLICKED!', e);
                e.preventDefault();
                try {
                    targetFormat = 'markdown';
                    console.log('🔧 Set targetFormat to:', targetFormat);
                    
                    if (conversionDescription) {
                        conversionDescription.textContent = 'Convert HTML tags to Markdown syntax';
                        console.log('🔧 Updated conversion description');
                    } else {
                        console.error('❌ conversionDescription element not found');
                    }
                    
                    if (formatOptions) {
                        formatOptions.style.display = 'block';
                        console.log('🔧 Showed format options');
                    } else {
                        console.error('❌ formatOptions element not found');
                    }
                    
                    this.style.display = 'none';
                    console.log('🔧 Hid markdown button');
                } catch (error) {
                    console.error('❌ Error in markdown button click handler:', error);
                }
            });
            
            // Add a test click listener to verify the button can be clicked
            switchToMarkdownBtn.addEventListener('mousedown', function() {
                console.log('🖱️ Mouse down on markdown button');
            });
            switchToMarkdownBtn.addEventListener('mouseup', function() {
                console.log('🖱️ Mouse up on markdown button');
            });
            
        } else {
            console.warn('⚠️ switchToMarkdownBtn not found');
        }
        
    } catch (error) {
        console.error('❌ Error initializing format switching:', error);
    }
    
    // Global test function to manually trigger markdown button
    window.testMarkdownButton = function() {
        console.log('🧪 Manual test: Looking for markdown button...');
        const btn = document.getElementById('switch-to-markdown-btn');
        if (btn) {
            console.log('🧪 Found button, style display:', getComputedStyle(btn).display);
            console.log('🧪 Button disabled?', btn.disabled);
            console.log('🧪 Button visible?', btn.offsetParent !== null);
            console.log('🧪 Triggering click...');
            btn.click();
        } else {
            console.error('🧪 Button not found');
        }
    };
    
    // Test for event conflicts - add a global click listener
    document.addEventListener('click', function(e) {
        if (e.target.id === 'switch-to-markdown-btn') {
            console.log('🌍 Global click listener caught markdown button click', e);
        }
    });
    
    // Confirm format switch
    if (confirmBtn) {
        confirmBtn.addEventListener('click', function() {
            console.log('🔘 Confirm button clicked, targetFormat:', targetFormat);
            const convertContent = document.querySelector('input[name="conversionOption"]:checked').value === 'convert';
            
            if (convertContent) {
                // Call server-side conversion
                convertAndSwitchFormat(targetFormat);
            } else {
                // Just switch format without conversion
                switchFormatOnly(targetFormat);
            }
        });
    } else {
        console.error('❌ confirmBtn not found');
    }
    
    // Cancel format switch
    if (cancelBtn) {
        cancelBtn.addEventListener('click', function() {
            console.log('🔘 Cancel button clicked');
            formatOptions.style.display = 'none';
            // Show the appropriate button again
            if (targetFormat === 'html') {
                switchToHtmlBtn.style.display = 'inline-block';
            } else {
                switchToMarkdownBtn.style.display = 'inline-block';
            }
        });
    } else {
        console.error('❌ cancelBtn not found');
    }
    
    function switchFormatOnly(newFormat) {
        console.log('🔧 switchFormatOnly called with:', newFormat);
        
        // Update the hidden select
        if (bodyFormatSelect) {
            bodyFormatSelect.value = newFormat;
            console.log('🔧 Updated hidden select to:', newFormat);
        } else {
            console.error('❌ bodyFormatSelect not found');
        }
        
        // Update UI
        updateFormatUI(newFormat);
        
        // Hide options
        if (formatOptions) {
            formatOptions.style.display = 'none';
            console.log('🔧 Hid format options');
        } else {
            console.error('❌ formatOptions not found when trying to hide');
        }
    }
    
    function convertAndSwitchFormat(newFormat) {
        // Get current content from the enhanced editor
        const editorTextarea = document.querySelector('textarea[id*="simple-editor-"]');
        const bodyTextarea = document.getElementById('body-textarea');
        
        let currentContent = '';
        if (editorTextarea) {
            currentContent = editorTextarea.value || '';
        } else if (bodyTextarea) {
            currentContent = bodyTextarea.value || '';
        }
        
        if (!currentContent.trim()) {
            // No content to convert, just switch format
            switchFormatOnly(newFormat);
            return;
        }
        
        // Show loading state
        confirmBtn.innerHTML = '<i class="bi bi-hourglass-split"></i> Converting...';
        confirmBtn.disabled = true;
        
        // Call conversion API
        fetch('/api/convert-content', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                content: currentContent,
                fromFormat: bodyFormatSelect.value,
                toFormat: newFormat
            })
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // Update content in the enhanced editor
                if (editorTextarea) {
                    editorTextarea.value = data.convertedContent;
                    // Trigger input event to update preview and sync
                    editorTextarea.dispatchEvent(new Event('input', { bubbles: true }));
                }
                if (bodyTextarea) {
                    bodyTextarea.value = data.convertedContent;
                }
                
                // Switch format
                switchFormatOnly(newFormat);
            } else {
                alert('Conversion failed: ' + (data.error || 'Unknown error'));
            }
        })
        .catch(error => {
            console.error('Conversion error:', error);
            alert('Conversion failed. Please try again or switch format without conversion.');
        })
        .finally(() => {
            // Restore button state
            confirmBtn.innerHTML = '<i class="bi bi-check-circle"></i> Confirm Format Switch';
            confirmBtn.disabled = false;
        });
    }
    
    function updateFormatUI(newFormat) {
        // Update badge
        const badge = document.querySelector('.badge.bg-primary');
        badge.textContent = newFormat.toUpperCase();
        
        // Update editor format for preview rendering
        if (typeof updateEditorFormat !== 'undefined') {
            updateEditorFormat(newFormat);
        }
        
        // Show/hide buttons based on new format
        if (newFormat === 'markdown') {
            // Show "Change to HTML" button, hide "Change to Markdown" button
            if (switchToHtmlBtn) {
                switchToHtmlBtn.style.display = 'inline-block';
            }
            if (switchToMarkdownBtn) {
                switchToMarkdownBtn.style.display = 'none';
            }
        } else {
            // Show "Change to Markdown" button, hide "Change to HTML" button  
            if (switchToMarkdownBtn) {
                switchToMarkdownBtn.style.display = 'inline-block';
            }
            if (switchToHtmlBtn) {
                switchToHtmlBtn.style.display = 'none';
            }
        }
        
        // Note: We don't modify innerHTML to preserve event listeners
        // The buttons already have the correct text and icons
    }
});