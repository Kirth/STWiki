// page-lock.js
// Simple page lock form submission

// Function to submit lock form with confirmation
function submitLockForm(confirmMessage) {
    if (confirm(confirmMessage)) {
        document.getElementById('lock-form').submit();
    }
}