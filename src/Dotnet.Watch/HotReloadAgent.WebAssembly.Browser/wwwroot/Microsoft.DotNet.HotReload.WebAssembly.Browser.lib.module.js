export async function onRuntimeConfigLoaded(config) {
    // Disable HotReload built-into the Blazor WebAssembly runtime
    config.environmentVariables["__BLAZOR_WEBASSEMBLY_LEGACY_HOTRELOAD"] = "false";
}

export async function onRuntimeReady({ getAssemblyExports, getConfig }) {
    // The browser tools client replays the updates produced before the browser connected as soon as
    // its authenticated socket is established, and the provider sends that snapshot only once.
    // Publish the readiness signal synchronously, before the first await, so the client can wait for
    // the apply API instead of silently discarding the replayed deltas.
    const agent = hotReloadAgentSignal();
    try {
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
    } finally {
        // Release the client even when the agent is disabled or initialization failed, so a browser
        // waiting on the apply API is delayed only by work that can still produce it.
        agent.setReady();
    }
}

// Rendezvous between this agent and the dotnet-watch browser tools client. Both modules create it,
// because library initializer module evaluation order is not guaranteed, and only this module
// resolves it. Kept in sync with dotnet-watch-browser-tools.js.
function hotReloadAgentSignal() {
    const agent = globalThis.__DOTNET_WATCH_HOT_RELOAD_AGENT ||= {};
    if (!agent.ready) {
        agent.ready = new Promise(resolve => { agent.setReady = resolve; });
    }

    return agent;
}
