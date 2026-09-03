export async function onRuntimeConfigLoaded(config) {
    // Disable HotReload built-into the Blazor WebAssembly runtime
    config.environmentVariables["__BLAZOR_WEBASSEMBLY_LEGACY_HOTRELOAD"] = "false";
}

export async function onRuntimeReady({ getAssemblyExports, getConfig }) {
    // The dotnet-watch activation initializer sets this variable when the app is being watched.
    // Reading it here rather than in onRuntimeConfigLoaded makes the check independent of the order
    // in which the runtime evaluates library initializer modules.
    const config = getConfig();
    if (config.debugLevel === 0 || config.environmentVariables?.["__DOTNET_WATCH_BROWSER_TOOLS"] !== "true") {
        return;
    }

    const exports = await getAssemblyExports("Microsoft.DotNet.HotReload.WebAssembly.Browser");
    await exports.Microsoft.DotNet.HotReload.WebAssembly.Browser.WebAssemblyHotReload.InitializeAsync(document.baseURI);

    if (!window.Blazor) {
        window.Blazor = {};

        if (!window.Blazor._internal) {
            window.Blazor._internal = {};
        }
    }

    window.Blazor._internal.applyHotReloadDeltas = (deltas, loggingLevel) => {
        const result = exports.Microsoft.DotNet.HotReload.WebAssembly.Browser.WebAssemblyHotReload.ApplyHotReloadDeltas(JSON.stringify(deltas), loggingLevel);
        return result ? JSON.parse(result) : [];
    };

    window.Blazor._internal.getApplyUpdateCapabilities = () => {
        return exports.Microsoft.DotNet.HotReload.WebAssembly.Browser.WebAssemblyHotReload.GetApplyUpdateCapabilities() ?? '';
    };
}
