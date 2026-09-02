const protocolVersion = 1;
const discoveryTimeoutMilliseconds = 1000;
let browserToolsSession;
let browserToolsRouteBase;
let useLegacyBrowserTools = false;

export async function onRuntimeConfigLoaded(config) {
    if (config.debugLevel !== 0 && globalThis.window?.document) {
        // The Gateway reserves this same-origin route outside the application's path base.
        browserToolsRouteBase = new URL('/_framework/dotnet-browser-tools/', document.baseURI);
        browserToolsSession = await discoverBrowserToolsSession(browserToolsRouteBase);
        useLegacyBrowserTools = !browserToolsSession &&
            !!document.querySelector("script[src*='aspnetcore-browser-refresh']");

        if (browserToolsSession || useLegacyBrowserTools) {
            config.environmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"] ??= "debug";
            config.environmentVariables["__ASPNETCORE_BROWSER_TOOLS"] ??= "true";
        }
    }

    // Disable HotReload built-into the Blazor WebAssembly runtime
    config.environmentVariables["__BLAZOR_WEBASSEMBLY_LEGACY_HOTRELOAD"] = "false";
}

export async function onRuntimeReady({ getAssemblyExports }) {
    if (!browserToolsSession && !useLegacyBrowserTools) {
        return;
    }

    const exports = await getAssemblyExports("Microsoft.DotNet.HotReload.WebAssembly.Browser");
    const hotReload = exports.Microsoft.DotNet.HotReload.WebAssembly.Browser.WebAssemblyHotReload;
    if (browserToolsSession) {
        await hotReload.InitializeBrowserToolsAsync(document.baseURI);
    } else {
        await hotReload.InitializeAsync(document.baseURI);
    }

    if (!window.Blazor) {
        window.Blazor = {};
    }

    if (!window.Blazor._internal) {
        window.Blazor._internal = {};
    }

    const applyManagedCodeUpdates = (deltas, loggingLevel) => {
        const result = exports.Microsoft.DotNet.HotReload.WebAssembly.Browser.WebAssemblyHotReload.ApplyHotReloadDeltas(JSON.stringify(deltas), loggingLevel);
        return result ? JSON.parse(result) : [];
    };

    const getApplyUpdateCapabilities = () =>
        exports.Microsoft.DotNet.HotReload.WebAssembly.Browser.WebAssemblyHotReload.GetApplyUpdateCapabilities() ?? '';

    window.Blazor._internal.applyHotReloadDeltas = applyManagedCodeUpdates;
    window.Blazor._internal.getApplyUpdateCapabilities = getApplyUpdateCapabilities;

    if (!browserToolsSession) {
        return;
    }

    const browserTools = await import(new URL('browser-tools.js', browserToolsRouteBase).href);
    const browserToolsConnection = await browserTools.connectBrowserTools(
        browserToolsSession,
        {
            applyManagedCodeUpdates,
            getApplyUpdateCapabilities
        },
        browserToolsRouteBase);

    const replay = await replayUpdates(browserToolsSession, browserToolsRouteBase, applyManagedCodeUpdates);
    if (!replay.success) {
        browserToolsConnection?.close();
        if (replay.generationMismatch) {
            location.reload();
        }

        return;
    }

    browserToolsConnection?.activate(replay.lastUpdateId);
}

async function discoverBrowserToolsSession(routeBase) {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), discoveryTimeoutMilliseconds);

    try {
        const response = await fetch(new URL('session.json', routeBase), {
            cache: 'no-store',
            headers: { 'accept': 'application/json' },
            signal: controller.signal
        });

        if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) {
            return undefined;
        }

        const descriptor = await response.json();
        return descriptor?.protocolVersion === protocolVersion &&
            typeof descriptor.sessionId === 'string' &&
            typeof descriptor.generationId === 'string' &&
            typeof descriptor.publicKey === 'string'
            ? descriptor
            : undefined;
    } catch (error) {
        if (error?.name !== 'AbortError') {
            console.debug('Browser tools provider discovery failed.', error);
        }

        return undefined;
    } finally {
        clearTimeout(timeout);
    }
}

async function replayUpdates(descriptor, routeBase, applyManagedCodeUpdates) {
    try {
        const response = await fetch(
            new URL(`updates/${encodeURIComponent(descriptor.generationId)}.json`, routeBase),
            { cache: 'no-store', headers: { 'accept': 'application/json' } });
        if (response.status === 409) {
            return { success: false, generationMismatch: true };
        }

        if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) {
            throw new Error(`Browser tools replay failed with status ${response.status}.`);
        }

        const batches = await response.json();
        for (const batch of batches) {
            applyManagedCodeUpdates(batch.deltas, 1);
        }

        return {
            success: true,
            lastUpdateId: batches.length === 0 ? undefined : batches[batches.length - 1].updateId
        };
    } catch (error) {
        console.warn('Unable to replay Hot Reload updates.', error);
        return { success: false };
    }
}
