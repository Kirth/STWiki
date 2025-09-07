// view-page.js
// JavaScript for wiki view page functionality

// Auto-highlight all code blocks on page load
document.addEventListener('DOMContentLoaded', function() {
    console.log('🎨 Applying syntax highlighting to view page...');
    Prism.highlightAll();
});

// Copy page URL to clipboard
function copyPageUrl(event) {
    if (event != null) { 
        event.preventDefault(); 
    }
    
    const url = window.location.href;
    navigator.clipboard.writeText(url).then(function() {
        const toast = document.createElement('div');
        toast.className = 'toast-notification';
        toast.textContent = 'Page URL copied to clipboard!';
        toast.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: var(--bs-success);
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
    }).catch(function(err) {
        console.error('Could not copy text: ', err);
        alert('Failed to copy URL to clipboard');
    });
}