// Activates the dotnet-watch browser tools client. Added to the app by the SDK during dotnet-watch builds.

// Signals to Microsoft.DotNet.HotReload.WebAssembly.Browser.lib.module.js that the app is being watched.
// Assigned during module evaluation so it is set before any initializer callback runs.
globalThis.__dotnetWatchBrowserTools = true;

// Started once the runtime is up so the Hot Reload apply API is available before updates are replayed.
export async function onRuntimeReady() {
    try {
        await import('/_framework/dotnet-browser-tools/browser-tools-bootstrap.js');
    } catch (error) {
        console.debug('Unable to load the dotnet-watch browser tools.', error);
    }
}
