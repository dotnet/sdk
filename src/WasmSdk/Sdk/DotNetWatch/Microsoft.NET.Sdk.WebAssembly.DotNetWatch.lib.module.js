// Activates the dotnet-watch browser tools client. Added to the app by the SDK during dotnet-watch builds.

export function onRuntimeConfigLoaded(config) {
    if (config.debugLevel === 0) {
        return;
    }

    // Signals to Microsoft.DotNet.HotReload.WebAssembly.Browser.lib.module.js that the app is watched.
    // Do not use __ASPNETCORE_BROWSER_TOOLS here: older runtimes interpret it as a request
    // to load the legacy application-hosted blazor-hotreload.js module.
    config.environmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"] ??= "debug";
    config.environmentVariables["__DOTNET_WATCH_BROWSER_TOOLS"] ??= "true";
}

export async function onRuntimeReady() {
    // Started once the runtime is up so that the Hot Reload apply API is available before updates are replayed.
    // The configuration module is app hosted and sits next to this initializer, so resolving it relative to
    // import.meta.url keeps it correct under any static web asset base path or fingerprinting scheme.
    try {
        await import(new URL('./Microsoft.NET.Sdk.WebAssembly.DotNetWatch.BrowserTools.Config.js', import.meta.url).href);
    } catch (error) {
        console.debug('Unable to load the dotnet-watch browser tools.', error);
    }
}
