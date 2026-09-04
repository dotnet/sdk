let isHotReloadEnabled = false;
let refreshConfig = null;

const hotReloadActiveKey = '_dotnet_watch_hot_reload_active';
const launchUrlConfigParameter = '__dotnet_watch';
const launchUrlConfigStorageKeyPrefix = '__dotnet_watch_browser_refresh_config:';
const managedCodeUpdatesStorageKeyPrefix = '__dotnet_watch_managed_code_updates:';
const scriptInjectedSentinel = '_dotnet_watch_ws_injected';

export async function onRuntimeConfigLoaded(config) {
    refreshConfig = readBrowserRefreshConfig();

    const hasInjectedScript = globalThis.window?.document?.querySelector("script[src*='aspnetcore-browser-refresh']");
    if (refreshConfig && hasInjectedScript) {
        clearBrowserRefreshConfig();
        refreshConfig = null;
    }

    if (config.debugLevel !== 0 && (refreshConfig || hasInjectedScript)) {
        isHotReloadEnabled = true;

        if (!config.environmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"]) {
            config.environmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug";
        }
        if (!config.environmentVariables["__ASPNETCORE_BROWSER_TOOLS"]) {
            config.environmentVariables["__ASPNETCORE_BROWSER_TOOLS"] = "true";
        }
    }

    // Disable Hot Reload built into the Blazor WebAssembly runtime.
    config.environmentVariables["__BLAZOR_WEBASSEMBLY_LEGACY_HOTRELOAD"] = "false";
}

export async function onRuntimeReady({ getAssemblyExports }) {
    if (!isHotReloadEnabled) {
        return;
    }

    const exports = await getAssemblyExports("Microsoft.DotNet.HotReload.WebAssembly.Browser");
    await exports.Microsoft.DotNet.HotReload.WebAssembly.Browser.WebAssemblyHotReload.InitializeAsync(
        document.baseURI,
        refreshConfig === null);

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

    if (refreshConfig) {
        try {
            applyPreviousManagedCodeUpdates();
        } catch (error) {
            console.debug('Unable to apply previous dotnet-watch managed code updates.', error);
        }

        try {
            await initializeBrowserRefreshConnection();
        } catch (error) {
            clearBrowserRefreshConfig();
            delete window[scriptInjectedSentinel];
            console.debug('Unable to initialize the dotnet-watch browser refresh connection.', error);
        }
    }
}

function readBrowserRefreshConfig() {
    if (!globalThis.window?.location) {
        return null;
    }

    const fragmentParts = window.location.hash.substring(1).split('&');
    const configPartIndex = fragmentParts.findIndex(part => part.startsWith(`${launchUrlConfigParameter}=`));

    if (configPartIndex >= 0) {
        try {
            const config = parseBrowserRefreshConfig(fragmentParts[configPartIndex].substring(launchUrlConfigParameter.length + 1));

            try {
                window.sessionStorage.setItem(getBrowserRefreshConfigStorageKey(), JSON.stringify(config));
                fragmentParts.splice(configPartIndex, 1);

                const fragment = fragmentParts.join('&');
                const cleanUrl = `${window.location.pathname}${window.location.search}${fragment ? `#${fragment}` : ''}`;
                window.history.replaceState(window.history.state, '', cleanUrl);
            } catch {
                // Keep the configuration in the fragment when session storage is unavailable.
            }

            return config;
        } catch (error) {
            console.debug('Ignoring invalid dotnet-watch browser refresh configuration.', error);
            return null;
        }
    }

    try {
        const storedConfig = window.sessionStorage.getItem(getBrowserRefreshConfigStorageKey());
        return storedConfig ? parseBrowserRefreshConfig(encodeURIComponent(storedConfig)) : null;
    } catch {
        return null;
    }
}

function parseBrowserRefreshConfig(encodedConfig) {
    const config = JSON.parse(decodeURIComponent(encodedConfig));
    if (typeof config?.webSocketUrls !== 'string' || !config.webSocketUrls ||
        typeof config?.serverKey !== 'string' || !config.serverKey) {
        throw new Error('Invalid browser refresh configuration.');
    }

    for (const endpoint of config.webSocketUrls.split(',')) {
        const endpointUrl = new URL(endpoint);
        if ((endpointUrl.protocol !== 'ws:' && endpointUrl.protocol !== 'wss:') ||
            !isTrustedBrowserRefreshHost(endpointUrl.hostname, window.location.hostname)) {
            throw new Error(`Untrusted browser refresh endpoint '${endpoint}'.`);
        }
    }

    return config;
}

function isTrustedBrowserRefreshHost(endpointHost, applicationHost) {
    return endpointHost === applicationHost ||
        (isLoopbackHost(endpointHost) && isLoopbackHost(applicationHost));
}

function isLoopbackHost(host) {
    return host === 'localhost' || host === '::1' || host === '[::1]' ||
        /^127\.(?:\d{1,3}\.){2}\d{1,3}$/.test(host);
}

function getBrowserRefreshConfigStorageKey() {
    return launchUrlConfigStorageKeyPrefix + document.baseURI;
}

function clearBrowserRefreshConfig() {
    try {
        window.sessionStorage.removeItem(getBrowserRefreshConfigStorageKey());
        clearManagedCodeUpdates();
    } catch {
    }
}

function getManagedCodeUpdatesStorageKey() {
    return managedCodeUpdatesStorageKeyPrefix + document.baseURI + refreshConfig.serverKey;
}

function readManagedCodeUpdates() {
    try {
        const value = window.sessionStorage.getItem(getManagedCodeUpdatesStorageKey());
        return value ? JSON.parse(value) : [];
    } catch (error) {
        console.debug('Unable to read previous dotnet-watch managed code updates.', error);
        clearManagedCodeUpdates();
        return [];
    }
}

function rememberManagedCodeUpdate(update) {
    try {
        const updates = readManagedCodeUpdates();
        if (!updates.some(previousUpdate => previousUpdate.id === update.id)) {
            updates.push(update);
            window.sessionStorage.setItem(getManagedCodeUpdatesStorageKey(), JSON.stringify(updates));
        }
    } catch (error) {
        console.debug('Unable to remember the dotnet-watch managed code update.', error);
    }
}

function clearManagedCodeUpdates() {
    try {
        window.sessionStorage.removeItem(getManagedCodeUpdatesStorageKey());
    } catch (error) {
        console.debug('Unable to clear previous dotnet-watch managed code updates.', error);
    }
}

function applyPreviousManagedCodeUpdates() {
    for (const update of readManagedCodeUpdates()) {
        const { applyError } = applyManagedCodeDeltas(update.deltas, update.responseLoggingLevel);
        if (applyError) {
            clearManagedCodeUpdates();
            throw applyError;
        }
    }
}

async function initializeBrowserRefreshConnection() {
    if (window.hasOwnProperty(scriptInjectedSentinel)) {
        return;
    }

    window[scriptInjectedSentinel] = true;

    const webSocketUrls = refreshConfig.webSocketUrls.split(',');
    const sharedSecret = await getSecret(refreshConfig.serverKey);
    let connection;
    for (const url of webSocketUrls) {
        try {
            connection = await getWebSocket(url, sharedSecret);
            break;
        } catch (error) {
            console.debug(error);
        }
    }

    if (!connection) {
        throw new Error('Unable to establish a connection to the browser refresh server.');
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
    console.debug('dotnet-watch reload socket connected.');

    function wait() {
        console.debug('Waiting for application to rebuild.');
        if (waiting) {
            return;
        }

        waiting = true;
        const glyphs = ['.', '..', '...'];
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

function updateCssByPath(path) {
    const styleElement = document.querySelector(`link[href^="${path}"]`) ||
        document.querySelector(`link[href^="${document.baseURI}${path}"]`);

    if (!styleElement || !styleElement.parentNode) {
        console.debug('Unable to find a stylesheet to update. Updating all local css files.');
        updateAllLocalCss();
    }

    updateCssElement(styleElement);
}

function updateAllLocalCss() {
    [...document.querySelectorAll('link')]
        .filter(link => link.baseURI === document.baseURI)
        .forEach(element => updateCssElement(element));
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

function applyDeltasLegacy(deltas) {
    const apply = window.Blazor?._internal?.applyHotReload;
    if (apply) {
        deltas.forEach(delta => {
            if (apply.length === 5) {
                apply(delta.moduleId, delta.metadataDelta, delta.ilDelta, delta.pdbDelta, delta.updatedTypes);
            } else {
                apply(delta.moduleId, delta.metadataDelta, delta.ilDelta, delta.pdbDelta);
            }
        });
    }
}

function applyManagedCodeUpdates(connection, sharedSecret, serverSecret, updateId, deltas, responseLoggingLevel) {
    if (sharedSecret && serverSecret !== sharedSecret.encodedSharedSecret) {
        throw new Error('Unable to validate the server. Rejecting apply-update payload.');
    }

    console.debug('Applying managed code updates.');

    const { applyError, log } = applyManagedCodeDeltas(deltas, responseLoggingLevel);
    if (!applyError) {
        rememberManagedCodeUpdate({ "id": updateId, "deltas": deltas, "responseLoggingLevel": responseLoggingLevel });
    }

    connection.send(JSON.stringify({ "success": !applyError, "log": log }));

    if (!applyError) {
        displayChangesAppliedToast();
    }
}

function applyManagedCodeDeltas(deltas, responseLoggingLevel) {
    const agentMessageSeverityError = 2;
    let applyError;
    let log = [];

    try {
        const applyDeltas = window.Blazor?._internal?.applyHotReloadDeltas;
        if (applyDeltas) {
            const wasmDeltas = deltas.map(delta => ({
                "moduleId": delta.moduleId,
                "metadataDelta": delta.metadataDelta,
                "ilDelta": delta.ilDelta,
                "pdbDelta": delta.pdbDelta,
                "updatedTypes": delta.updatedTypes,
            }));
            log = applyDeltas(wasmDeltas, responseLoggingLevel);
        } else {
            applyDeltasLegacy(deltas);
        }
    } catch (error) {
        console.warn(error);
        applyError = error;
        log.push({ "message": getMessageAndStack(error), "severity": agentMessageSeverityError });
    }

    return { applyError, log };
}

function reportDiagnostics(diagnostics) {
    console.debug('Reporting Hot Reload diagnostics.');

    document.querySelectorAll('#dotnet-compile-error').forEach(element => element.remove());

    if (diagnostics.length === 0) {
        return;
    }

    const element = document.body.appendChild(document.createElement('div'));
    element.id = 'dotnet-compile-error';
    element.setAttribute('style', 'z-index:1000000; position:fixed; top: 0; left: 0; right: 0; bottom: 0; background-color: rgba(0,0,0,0.5); color:black; overflow: scroll;');
    diagnostics.forEach(error => {
        const item = element.appendChild(document.createElement('div'));
        item.setAttribute('style', 'border: 2px solid red; padding: 8px; background-color: #faa;');
        const message = item.appendChild(document.createElement('div'));
        message.setAttribute('style', 'font-weight: bold');
        message.textContent = error.Message;
        item.appendChild(document.createElement('div')).textContent = error;
    });
}

function displayChangesAppliedToast() {
    document.querySelectorAll('#dotnet-compile-error').forEach(element => element.remove());
    if (document.querySelector('#dotnet-hotreload-toast') || !window[hotReloadActiveKey]) {
        return;
    }

    const element = document.createElement('div');
    element.id = 'dotnet-hotreload-toast';
    element.innerHTML = "<svg style=\"filter: drop-shadow(0px 2px 1px rgb(0 0 0 / 0.4));\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 500 500\"><style><![CDATA[#hotreloaded-ellipse1 {animation: hotreloaded-ellipse1_c_o 1800ms linear 1 normal forwards}@keyframes hotreloaded-ellipse1_c_o { 0% {opacity: 0} 16.666667% {opacity: 1} 72.222222% {opacity: 1} 90% {opacity: 0} 100% {opacity: 0}} #hotreloaded-path1 {animation-name: hotreloaded-path1__m, hotreloaded-path1_c_o;animation-duration: 1800ms;animation-delay:100ms;animation-fill-mode: forwards;animation-timing-function: linear;animation-direction: normal;animation-iteration-count: 1;}@keyframes hotreloaded-path1__m { 0% {d: path('M126.151214,288.396852L196.625037,350.661591L320.793323,178.518242')} 16.666667% {d: path('M126.151214,288.396852L126.151214,288.396852L126.151214,288.396852')} 22.222222% {d: path('M126.151214,288.396852L196.625037,350.661591L196.625037,350.661591');animation-timing-function: cubic-bezier(0.42,0,0.58,1)} 33.333333% {d: path('M126.151214,288.396852L196.625037,350.661591L320.793323,178.518242')} 100% {d: path('M126.151214,288.396852L196.625037,350.661591L320.793323,178.518242')}}@keyframes hotreloaded-path1_c_o { 0% {opacity: 0} 16.666667% {opacity: 0} 22.222222% {opacity: 1} 72.222222% {opacity: 1} 90% {opacity: 0} 100% {opacity: 0}}]]></style><ellipse id=\"hotreloaded-ellipse1\" rx=\"212.808853\" ry=\"205.404598\" transform=\"matrix(0.982102 0 0 1.017504 251 238)\" opacity=\"0\" fill=\"rgb(120,120,120)\"/><path id=\"hotreloaded-path1\" d=\"M126.151214,288.396852L196.625037,350.661591L320.793323,178.518242\" transform=\"matrix(1 0 0 1 27.527732 -26.589916)\" opacity=\"0\" fill=\"none\" stroke=\"rgb(255,255,255)\" stroke-width=\"40\" stroke-linecap=\"round\"/></svg>";
    element.setAttribute('style', 'z-index: 1000000; width: 48px; height: 48px; position:fixed; top:5px; left: 5px');
    document.body.appendChild(element);
    window[hotReloadActiveKey] = false;
    setTimeout(() => element.remove(), 2000);
}

function refreshBrowser() {
    if (window.Blazor) {
        window[hotReloadActiveKey] = true;
        if (window.Blazor?._internal?.hotReloadApplied) {
            console.debug('Refreshing browser: WASM.');
            window.Blazor._internal.hotReloadApplied();
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
    clearManagedCodeUpdates();
    location.reload();
}

async function getSecret(serverKeyString) {
    if (!serverKeyString || !window.crypto || !window.crypto.subtle) {
        throw new Error('Browser refresh requires WebCrypto and a server public key.');
    }

    const secretBytes = window.crypto.getRandomValues(new Uint8Array(32));
    const binaryServerKey = stringToArrayBuffer(atob(serverKeyString));
    const serverKey = await window.crypto.subtle.importKey('spki', binaryServerKey, { name: "RSA-OAEP", hash: "SHA-256" }, false, ['encrypt']);
    const encrypted = await window.crypto.subtle.encrypt({ name: 'RSA-OAEP' }, serverKey, secretBytes);
    return {
        encryptedSharedSecret: btoa(String.fromCharCode(...new Uint8Array(encrypted))),
        encodedSharedSecret: btoa(String.fromCharCode(...secretBytes)),
    };
}

function stringToArrayBuffer(value) {
    const buffer = new ArrayBuffer(value.length);
    const bufferView = new Uint8Array(buffer);
    for (let i = 0; i < value.length; i++) {
        bufferView[i] = value.charCodeAt(i);
    }

    return buffer;
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
