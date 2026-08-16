// Kept in its own file rather than inline in index.html so the Content-Security-Policy
// never has to allow 'unsafe-inline' for scripts.
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
}
