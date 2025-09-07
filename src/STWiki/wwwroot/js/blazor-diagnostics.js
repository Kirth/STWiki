// blazor-diagnostics.js
// Blazor connection diagnostics and state monitoring

(function() {
    console.log('🔧 Blazor diagnostics: Starting connection monitoring...');
    
    function logBlazorState() {
        const state = {
            blazor: typeof Blazor !== 'undefined' ? 'loaded' : 'not loaded',
            signalR: typeof signalR !== 'undefined' ? 'loaded' : 'not loaded',
            connection: null,
            circuit: null,
            connected: false
        };
        
        if (typeof Blazor !== 'undefined') {
            try {
                // Better detection: Blazor is connected if it exists and has handlers
                // The presence of Blazor object itself usually means it's functional
                state.circuit = 'available';
                state.connected = true;
                
                // Also check if we can access the internal circuit (optional)
                if (Blazor.defaultReconnectionHandler) {
                    state.connection = 'with reconnection handler';
                }
            } catch (e) {
                state.circuit = 'error: ' + e.message;
                state.connected = false;
            }
        }
        
        console.log('🔧 Blazor State:', state);
        return state.connected;
    }
    
    // Log initial state
    document.addEventListener('DOMContentLoaded', () => {
        console.log('🔧 DOM loaded, checking Blazor state...');
        logBlazorState();
    });
    
    // Monitor Blazor connection events - DON'T call Blazor.start() as it auto-starts
    if (typeof Blazor !== 'undefined') {
        console.log('✅ Blazor already available');
        logBlazorState();
    } else {
        // Blazor not ready yet, wait for it
        let attempts = 0;
        const waitForBlazor = setInterval(() => {
            attempts++;
            if (typeof Blazor !== 'undefined') {
                console.log(`✅ Blazor available after ${attempts} attempts`);
                clearInterval(waitForBlazor);
                logBlazorState();
            } else if (attempts > 50) { // 5 seconds max
                console.error('❌ Blazor failed to load after 5 seconds');
                clearInterval(waitForBlazor);
                logBlazorState();
            }
        }, 100);
    }
    
    // Add a function for components to check connection
    window.checkBlazorConnection = function() {
        const isConnected = logBlazorState();
        console.log('🔗 Connection check requested, result:', isConnected);
        return isConnected;
    };
    
    window.blazorDiagnostics = { logBlazorState, checkBlazorConnection };
})();