let isHotReloadEnabled = false;
let refreshConfig = null;

export async function onRuntimeConfigLoaded(config) {
    // Try to fetch browser-refresh config to determine if hot reload is available.
    // This replaces the previous approach of detecting an injected <script> tag.
    try {
        const configResponse = await fetch('/_framework/browser-refresh-config');
        if (configResponse.ok) {
            refreshConfig = await configResponse.json();
        }
    } catch {
        // Config endpoint not available — hot reload middleware may not be loaded.
    }

    // Enable hot reload if either the config endpoint responded or the injected script tag is present (hosted scenario fallback).
    const hasConfigEndpoint = refreshConfig && refreshConfig.webSocketUrls;
    const hasInjectedScript = globalThis.window?.document?.querySelector("script[src*='aspnetcore-browser-refresh']");

    if (config.debugLevel !== 0 && (hasConfigEndpoint || hasInjectedScript)) {
        isHotReloadEnabled = true;

        if (!config.environmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"]) {
            config.environmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug";
        }
        if (!config.environmentVariables["__ASPNETCORE_BROWSER_TOOLS"]) {
            config.environmentVariables["__ASPNETCORE_BROWSER_TOOLS"] = "true";
        }
    }

    // Disable HotReload built-into the Blazor WebAssembly runtime
    config.environmentVariables["__BLAZOR_WEBASSEMBLY_LEGACY_HOTRELOAD"] = "false";
}

export async function onRuntimeReady({ getAssemblyExports }) {
    if (!isHotReloadEnabled) {
        return;
    }

    const exports = await getAssemblyExports("Microsoft.DotNet.HotReload.WebAssembly.Browser");
    await exports.Microsoft.DotNet.HotReload.WebAssembly.Browser.WebAssemblyHotReload.InitializeAsync(document.baseURI);

    if (!window.Blazor) {
        window.Blazor = {};
    }
    if (!window.Blazor._internal) {
        window.Blazor._internal = {};
    }

    window.Blazor._internal.applyHotReloadDeltas = (deltas, loggingLevel) => {
        const result = exports.Microsoft.DotNet.HotReload.WebAssembly.Browser.WebAssemblyHotReload.ApplyHotReloadDeltas(JSON.stringify(deltas), loggingLevel);
        return result ? JSON.parse(result) : [];
    };

    window.Blazor._internal.getApplyUpdateCapabilities = () => {
        return exports.Microsoft.DotNet.HotReload.WebAssembly.Browser.WebAssemblyHotReload.GetApplyUpdateCapabilities() ?? '';
    };

    // Establish WebSocket connection for hot reload communication.
    // Only attempt if we have config from the endpoint. In hosted scenarios where
    // config fetch failed, the injected script (WebSocketScriptInjection.js) handles the connection.
    if (refreshConfig && refreshConfig.webSocketUrls) {
        await initializeBrowserRefreshConnection();
    }
}

// ---- Browser refresh / hot reload WebSocket communication ----
// Ported from WebSocketScriptInjection.js

const hotReloadActiveKey = '_dotnet_watch_hot_reload_active';
const scriptInjectedSentinel = '_dotnet_watch_ws_injected';

async function initializeBrowserRefreshConnection() {
    // Prevent double-connection if the injected script has already connected
    if (window.hasOwnProperty(scriptInjectedSentinel)) {
        return;
    }
    // Claim the sentinel early to prevent races with the injected script.
    // If connection fails, we clear it so the injected script can take over.
    window[scriptInjectedSentinel] = true;

    const webSocketUrls = refreshConfig.webSocketUrls.split(',');
    const sharedSecret = await getSecret(refreshConfig.serverKey);
    let connection;
    for (const url of webSocketUrls) {
        try {
            connection = await getWebSocket(url, sharedSecret);
            break;
        } catch (ex) {
            console.debug(ex);
        }
    }
    if (!connection) {
        console.debug('Unable to establish a connection to the browser refresh server.');
        // Clear sentinel so the injected script can still connect as fallback
        delete window[scriptInjectedSentinel];
        return;
    }

    let waiting = false;

    connection.onmessage = function (message) {
        const payload = JSON.parse(message.data);
        const action = {
            'Reload': () => reload(),
            'Wait': () => wait(),
            'UpdateStaticFile': () => updateStaticFile(payload.path),
            'ApplyManagedCodeUpdates': () => applyManagedCodeUpdates(connection, sharedSecret, payload.sharedSecret, payload.updateId, payload.deltas, payload.responseLoggingLevel),
            'ReportDiagnostics': () => reportDiagnostics(payload.diagnostics),
            'GetApplyUpdateCapabilities': () => getApplyUpdateCapabilities(connection),
            'RefreshBrowser': () => refreshBrowser()
        };

        if (payload.type && action.hasOwnProperty(payload.type)) {
            action[payload.type]();
        } else {
            console.error('Unknown payload:', message.data);
        }
    };

    connection.onerror = function (event) { console.debug('dotnet-watch reload socket error.', event); };
    connection.onclose = function () { console.debug('dotnet-watch reload socket closed.'); };
    connection.onopen = function () { console.debug('dotnet-watch reload socket connected.'); };

    function wait() {
        console.debug('Waiting for application to rebuild.');
        if (waiting) {
            return;
        }
        waiting = true;
        const glyphs = ['☱', '☲', '☴'];
        const title = document.title;
        let i = 0;
        setInterval(function () { document.title = glyphs[i++ % glyphs.length] + ' ' + title; }, 240);
    }
}

function updateStaticFile(path) {
    if (path && path.endsWith('.css')) {
        updateCssByPath(path);
    } else {
        console.debug(`File change detected to file ${path}. Reloading page...`);
        location.reload();
    }
}

async function updateCssByPath(path) {
    const styleElement = document.querySelector(`link[href^="${path}"]`) ||
        document.querySelector(`link[href^="${document.baseURI}${path}"]`);

    // Receive a Clear-site-data header.
    await fetch('/_framework/clear-browser-cache');

    if (!styleElement || !styleElement.parentNode) {
        console.debug('Unable to find a stylesheet to update. Updating all local css files.');
        updateAllLocalCss();
    }

    updateCssElement(styleElement);
}

function updateAllLocalCss() {
    [...document.querySelectorAll('link')]
        .filter(l => l.baseURI === document.baseURI)
        .forEach(e => updateCssElement(e));
}

function updateCssElement(styleElement) {
    if (!styleElement || styleElement.loading) {
        return;
    }

    const newElement = styleElement.cloneNode();
    const href = styleElement.href;
    newElement.href = href.split('?', 1)[0] + `?nonce=${Date.now()}`;

    styleElement.loading = true;
    newElement.loading = true;
    newElement.addEventListener('load', function () {
        newElement.loading = false;
        styleElement.remove();
    });

    styleElement.parentNode.insertBefore(newElement, styleElement.nextSibling);
}

function getMessageAndStack(error) {
    const message = error.message || '<unknown error>';
    let messageAndStack = error.stack || message;
    if (!messageAndStack.includes(message)) {
        messageAndStack = message + "\n" + messageAndStack;
    }
    return messageAndStack;
}

function getApplyUpdateCapabilities(connection) {
    let applyUpdateCapabilities;
    try {
        applyUpdateCapabilities = window.Blazor._internal.getApplyUpdateCapabilities();
    } catch (error) {
        applyUpdateCapabilities = "!" + getMessageAndStack(error);
    }
    connection.send(applyUpdateCapabilities);
}

function applyDeltas_legacy(deltas) {
    let apply = window.Blazor?._internal?.applyHotReload;
    if (apply) {
        deltas.forEach(d => {
            if (apply.length == 5) {
                apply(d.moduleId, d.metadataDelta, d.ilDelta, d.pdbDelta, d.updatedTypes);
            } else {
                apply(d.moduleId, d.metadataDelta, d.ilDelta, d.pdbDelta);
            }
        });
    }
}

async function applyManagedCodeUpdates(connection, sharedSecret, serverSecret, updateId, deltas, responseLoggingLevel) {
    if (sharedSecret && (serverSecret != sharedSecret.encodedSharedSecret)) {
        throw 'Unable to validate the server. Rejecting apply-update payload.';
    }

    console.debug('Applying managed code updates.');

    const AgentMessageSeverity_Error = 2;
    let applyError = undefined;
    let log = [];

    try {
        let applyDeltas = window.Blazor?._internal?.applyHotReloadDeltas;
        if (applyDeltas) {
            let wasmDeltas = deltas.map(delta => ({
                "moduleId": delta.moduleId,
                "metadataDelta": delta.metadataDelta,
                "ilDelta": delta.ilDelta,
                "pdbDelta": delta.pdbDelta,
                "updatedTypes": delta.updatedTypes,
            }));
            log = applyDeltas(wasmDeltas, responseLoggingLevel);
        } else {
            applyDeltas_legacy(deltas);
        }
    } catch (error) {
        console.warn(error);
        applyError = error;
        log.push({ "message": getMessageAndStack(error), "severity": AgentMessageSeverity_Error });
    }

    try {
        let body = JSON.stringify({ "id": updateId, "deltas": deltas });
        await fetch('/_framework/blazor-hotreload', { method: 'post', headers: { 'content-type': 'application/json' }, body: body });
    } catch (error) {
        console.warn(error);
        applyError = error;
        log.push({ "message": getMessageAndStack(error), "severity": AgentMessageSeverity_Error });
    }

    connection.send(JSON.stringify({ "success": !applyError, "log": log }));

    if (!applyError) {
        displayChangesAppliedToast();
    }
}

function reportDiagnostics(diagnostics) {
    console.debug('Reporting Hot Reload diagnostics.');

    document.querySelectorAll('#dotnet-compile-error').forEach(el => el.remove());

    if (diagnostics.length == 0) {
        return;
    }

    const el = document.body.appendChild(document.createElement('div'));
    el.id = 'dotnet-compile-error';
    el.setAttribute('style', 'z-index:1000000; position:fixed; top: 0; left: 0; right: 0; bottom: 0; background-color: rgba(0,0,0,0.5); color:black; overflow: scroll;');
    diagnostics.forEach(error => {
        const item = el.appendChild(document.createElement('div'));
        item.setAttribute('style', 'border: 2px solid red; padding: 8px; background-color: #faa;');
        const message = item.appendChild(document.createElement('div'));
        message.setAttribute('style', 'font-weight: bold');
        message.textContent = error.Message;
        item.appendChild(document.createElement('div')).textContent = error;
    });
}

function displayChangesAppliedToast() {
    document.querySelectorAll('#dotnet-compile-error').forEach(el => el.remove());
    if (document.querySelector('#dotnet-hotreload-toast')) {
        return;
    }
    if (!window[hotReloadActiveKey]) {
        return;
    }
    const el = document.createElement('div');
    el.id = 'dotnet-hotreload-toast';
    el.innerHTML = "<svg style=\"filter: drop-shadow(0px 2px 1px rgb(0 0 0 / 0.4));\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 500 500\"><style><![CDATA[#hotreloaded-ellipse1 {animation: hotreloaded-ellipse1_c_o 1800ms linear 1 normal forwards}@keyframes hotreloaded-ellipse1_c_o { 0% {opacity: 0} 16.666667% {opacity: 1} 72.222222% {opacity: 1} 90% {opacity: 0} 100% {opacity: 0}} #hotreloaded-path1 {animation-name: hotreloaded-path1__m, hotreloaded-path1_c_o;animation-duration: 1800ms;animation-delay:100ms;animation-fill-mode: forwards;animation-timing-function: linear;animation-direction: normal;animation-iteration-count: 1;}@keyframes hotreloaded-path1__m { 0% {d: path('M126.151214,288.396852L196.625037,350.661591L320.793323,178.518242')} 16.666667% {d: path('M126.151214,288.396852L126.151214,288.396852L126.151214,288.396852')} 22.222222% {d: path('M126.151214,288.396852L196.625037,350.661591L196.625037,350.661591');animation-timing-function: cubic-bezier(0.42,0,0.58,1)} 33.333333% {d: path('M126.151214,288.396852L196.625037,350.661591L320.793323,178.518242')} 100% {d: path('M126.151214,288.396852L196.625037,350.661591L320.793323,178.518242')}}@keyframes hotreloaded-path1_c_o { 0% {opacity: 0} 16.666667% {opacity: 0} 22.222222% {opacity: 1} 72.222222% {opacity: 1} 90% {opacity: 0} 100% {opacity: 0}}]]></style><ellipse id=\"hotreloaded-ellipse1\" rx=\"212.808853\" ry=\"205.404598\" transform=\"matrix(0.982102 0 0 1.017504 251 238)\" opacity=\"0\" fill=\"rgb(120,120,120)\"/><path id=\"hotreloaded-path1\" d=\"M126.151214,288.396852L196.625037,350.661591L320.793323,178.518242\" transform=\"matrix(1 0 0 1 27.527732 -26.589916)\" opacity=\"0\" fill=\"none\" stroke=\"rgb(255,255,255)\" stroke-width=\"40\" stroke-linecap=\"round\"/></svg>";
    el.setAttribute('style', 'z-index: 1000000; width: 48px; height: 48px; position:fixed; top:5px; left: 5px');
    document.body.appendChild(el);
    window[hotReloadActiveKey] = false;
    setTimeout(() => el.remove(), 2000);
}

function refreshBrowser() {
    if (window.Blazor) {
        window[hotReloadActiveKey] = true;
        if (window.Blazor?._internal?.hotReloadApplied) {
            console.debug('Refreshing browser: WASM.');
            Blazor._internal.hotReloadApplied();
        } else {
            console.debug('Refreshing browser.');
            displayChangesAppliedToast();
        }
    } else {
        console.debug('Refreshing browser: Reloading.');
        location.reload();
    }
}

function reload() {
    console.debug('Reloading.');
    location.reload();
}

async function getSecret(serverKeyString) {
    if (!serverKeyString || !window.crypto || !window.crypto.subtle) {
        return null;
    }

    const secretBytes = window.crypto.getRandomValues(new Uint8Array(32));

    const binaryServerKey = str2ab(atob(serverKeyString));
    const serverKey = await window.crypto.subtle.importKey('spki', binaryServerKey, { name: "RSA-OAEP", hash: "SHA-256" }, false, ['encrypt']);
    const encrypted = await window.crypto.subtle.encrypt({ name: 'RSA-OAEP' }, serverKey, secretBytes);
    return {
        encryptedSharedSecret: btoa(String.fromCharCode(...new Uint8Array(encrypted))),
        encodedSharedSecret: btoa(String.fromCharCode(...secretBytes)),
    };

    function str2ab(str) {
        const buf = new ArrayBuffer(str.length);
        const bufView = new Uint8Array(buf);
        for (let i = 0, strLen = str.length; i < strLen; i++) {
            bufView[i] = str.charCodeAt(i);
        }
        return buf;
    }
}

function getWebSocket(url, sharedSecret) {
    return new Promise((resolve, reject) => {
        const encryptedSecret = sharedSecret && sharedSecret.encryptedSharedSecret;
        const protocol = encryptedSecret ? encodeURIComponent(encryptedSecret) : [];
        const webSocket = new WebSocket(url, protocol);
        let opened = false;

        function onOpen() {
            opened = true;
            clearEventListeners();
            resolve(webSocket);
        }

        function onClose(event) {
            if (opened) {
                return;
            }
            let error = 'WebSocket failed to connect.';
            if (event instanceof ErrorEvent) {
                error = event.error;
            }
            clearEventListeners();
            reject(error);
        }

        function clearEventListeners() {
            webSocket.removeEventListener('open', onOpen);
            webSocket.removeEventListener('close', onClose);
        }

        webSocket.addEventListener('open', onOpen);
        webSocket.addEventListener('close', onClose);
        if (window.Blazor?.removeEventListener && window.Blazor?.addEventListener) {
            webSocket.addEventListener('close', () => window.Blazor?.removeEventListener('enhancedload', displayChangesAppliedToast));
            window.Blazor?.addEventListener('enhancedload', displayChangesAppliedToast);
        }
    });
}
