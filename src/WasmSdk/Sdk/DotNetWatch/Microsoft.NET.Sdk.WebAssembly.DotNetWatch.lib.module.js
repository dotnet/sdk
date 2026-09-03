// Activates the dotnet-watch browser tools client. Added to the app by the SDK during dotnet-watch builds.

export function onRuntimeConfigLoaded(config) {
    if (config.debugLevel === 0) {
        return;
    }

    // Signals to Microsoft.DotNet.HotReload.WebAssembly.Browser.lib.module.js that the app is watched.
    config.environmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"] ??= "debug";
    config.environmentVariables["__ASPNETCORE_BROWSER_TOOLS"] ??= "true";
}

export async function onRuntimeReady() {
    // Started once the runtime is up so that the Hot Reload apply API is available before updates are replayed.
    try {
        await import('/_framework/dotnet-browser-tools/browser-tools-bootstrap.js');
    } catch (error) {
        console.debug('Unable to load the dotnet-watch browser tools.', error);
    }
}
