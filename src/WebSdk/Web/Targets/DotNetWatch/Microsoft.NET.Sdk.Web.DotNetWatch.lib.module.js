const browserToolsBootstrapPath = '/_framework/dotnet-browser-tools/browser-tools-bootstrap.js';

export async function afterWebStarted() {
    if (!document.querySelector(`script[src*='${browserToolsBootstrapPath}']`)) {
        await import(browserToolsBootstrapPath);
    }
}
