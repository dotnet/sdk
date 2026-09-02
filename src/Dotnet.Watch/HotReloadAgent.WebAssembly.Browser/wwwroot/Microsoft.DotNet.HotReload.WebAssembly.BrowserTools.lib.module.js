const hotReloadActiveKey = '_dotnet_watch_hot_reload_active';
const scriptInjectedSentinel = '_dotnet_watch_browser_tools_connected';

export async function connectBrowserTools(descriptor, callbacks, routeBase) {
    if (window.hasOwnProperty(scriptInjectedSentinel)) {
        return;
    }

    window[scriptInjectedSentinel] = true;

    const sharedSecret = await getSecret(descriptor.publicKey);
    const connectUrl = new URL('connect', routeBase);
    connectUrl.protocol = connectUrl.protocol === 'https:' ? 'wss:' : 'ws:';

    let connection;
    try {
        connection = await getWebSocket(connectUrl, sharedSecret);
    } catch (error) {
        delete window[scriptInjectedSentinel];
        console.debug('Unable to establish a connection to the browser tools server.', error);
        return;
    }

    let waiting = false;
    let active = false;
    let lastAppliedUpdateId;
    let messageQueue = Promise.resolve();
    const pendingMessages = [];

    connection.onmessage = message => {
        if (!active) {
            pendingMessages.push(message);
            return;
        }

        enqueueMessage(message);
    };

    function enqueueMessage(message) {
        messageQueue = messageQueue
            .then(() => handleMessage(message))
            .catch(error => console.error('Failed to process a browser tools message.', error));
    }

    async function handleMessage(message) {
        const payload = JSON.parse(message.data);
        const actions = {
            'Reload': () => reload(),
            'Wait': () => wait(),
            'UpdateStaticFile': () => updateStaticFile(payload.path),
            'ApplyManagedCodeUpdates': () => applyManagedCodeUpdates(payload),
            'ReportDiagnostics': () => reportDiagnostics(payload.diagnostics),
            'GetApplyUpdateCapabilities': () => getApplyUpdateCapabilities(),
            'RefreshBrowser': () => refreshBrowser()
        };

        if (payload.type && actions.hasOwnProperty(payload.type)) {
            await actions[payload.type]();
        } else {
            console.error('Unknown payload:', message.data);
        }
    }

    connection.onerror = event => console.debug('dotnet-watch browser tools socket error.', event);
    connection.onclose = () => {
        delete window[scriptInjectedSentinel];
        console.debug('dotnet-watch browser tools socket closed.');
    };
    connection.onopen = () => console.debug('dotnet-watch browser tools socket connected.');

    async function updateStaticFile(path) {
        if (path && path.endsWith('.css')) {
            await updateCssByPath(path);
        } else {
            console.debug(`File change detected to file ${path}. Reloading page...`);
            location.reload();
        }
    }

    async function updateCssByPath(path) {
        const relativePath = path.startsWith('wwwroot/') ? path.substring('wwwroot/'.length) : path.replace(/^\/+/, '');
        const stylesheetUrl = new URL(relativePath, document.baseURI);
        const styleElement = [...document.querySelectorAll('link[rel="stylesheet"]')]
            .find(link => new URL(link.href).pathname === stylesheetUrl.pathname);

        await fetch(new URL('clear-cache', routeBase), { cache: 'no-store' });

        if (!styleElement || !styleElement.parentNode) {
            console.debug('Unable to find a stylesheet to update. Updating all local css files.');
            updateAllLocalCss();
            return;
        }

        updateCssElement(styleElement);
    }

    function updateAllLocalCss() {
        [...document.querySelectorAll('link[rel="stylesheet"]')]
            .filter(link => new URL(link.href).origin === location.origin)
            .forEach(updateCssElement);
    }

    function updateCssElement(styleElement) {
        if (!styleElement || styleElement.loading) {
            return;
        }

        const newElement = styleElement.cloneNode();
        newElement.href = styleElement.href.split('?', 1)[0] + `?nonce=${Date.now()}`;

        styleElement.loading = true;
        newElement.loading = true;
        newElement.addEventListener('load', () => {
            newElement.loading = false;
            styleElement.remove();
        });

        styleElement.parentNode.insertBefore(newElement, styleElement.nextSibling);
    }

    function getApplyUpdateCapabilities() {
        let capabilities;
        try {
            capabilities = callbacks.getApplyUpdateCapabilities();
        } catch (error) {
            capabilities = '!' + getMessageAndStack(error);
        }

        connection.send(capabilities);
    }

    async function applyManagedCodeUpdates(payload) {
        if (payload.generationId !== descriptor.generationId) {
            connection.close();
            location.reload();
            return;
        }

        if (sharedSecret && payload.sharedSecret !== sharedSecret.encodedSharedSecret) {
            throw new Error('Unable to validate the server. Rejecting apply-update payload.');
        }

        if (lastAppliedUpdateId !== undefined && payload.updateId <= lastAppliedUpdateId) {
            connection.send(JSON.stringify({ success: true, log: [] }));
            return;
        }

        console.debug('Applying managed code updates.');

        let applyError;
        let log = [];
        try {
            log = callbacks.applyManagedCodeUpdates(payload.deltas, payload.responseLoggingLevel);
        } catch (error) {
            console.warn(error);
            applyError = error;
            log.push({ message: getMessageAndStack(error), severity: 2 });
        }

        connection.send(JSON.stringify({
            success: !applyError,
            log
        }));

        if (!applyError) {
            lastAppliedUpdateId = payload.updateId;
            displayChangesAppliedToast();
        }
    }

    function reportDiagnostics(diagnostics) {
        document.querySelectorAll('#dotnet-compile-error').forEach(element => element.remove());
        if (diagnostics.length === 0) {
            return;
        }

        const container = document.body.appendChild(document.createElement('div'));
        container.id = 'dotnet-compile-error';
        container.setAttribute('style', 'z-index:1000000; position:fixed; top:0; left:0; right:0; bottom:0; background-color:rgba(0,0,0,0.5); color:black; overflow:scroll;');
        diagnostics.forEach(diagnostic => {
            const item = container.appendChild(document.createElement('div'));
            item.setAttribute('style', 'border:2px solid red; padding:8px; background-color:#faa;');
            item.appendChild(document.createElement('div')).textContent = diagnostic;
        });
    }

    function refreshBrowser() {
        if (!window.Blazor) {
            location.reload();
            return;
        }

        window[hotReloadActiveKey] = true;
        if (window.Blazor?._internal?.hotReloadApplied) {
            Blazor._internal.hotReloadApplied();
        } else {
            displayChangesAppliedToast();
        }
    }

    function reload() {
        location.reload();
    }

    function wait() {
        if (waiting) {
            return;
        }

        waiting = true;
        const glyphs = ['.', '..', '...'];
        const title = document.title;
        let index = 0;
        setInterval(() => document.title = glyphs[index++ % glyphs.length] + ' ' + title, 240);
    }

    function displayChangesAppliedToast() {
        document.querySelectorAll('#dotnet-compile-error').forEach(element => element.remove());
        if (document.querySelector('#dotnet-hotreload-toast') || !window[hotReloadActiveKey]) {
            return;
        }

        const element = document.createElement('div');
        element.id = 'dotnet-hotreload-toast';
        element.textContent = 'HR';
        element.setAttribute('style', 'z-index:1000000; width:48px; height:48px; position:fixed; top:5px; left:5px; border-radius:24px; background:#787878; color:white; font:bold 32px sans-serif; text-align:center; line-height:48px; box-shadow:0 2px 2px rgb(0 0 0 / 0.4);');
        document.body.appendChild(element);
        window[hotReloadActiveKey] = false;
        setTimeout(() => element.remove(), 2000);
    }

    if (window.Blazor?.removeEventListener && window.Blazor?.addEventListener) {
        connection.addEventListener('close', () => window.Blazor?.removeEventListener('enhancedload', displayChangesAppliedToast));
        window.Blazor.addEventListener('enhancedload', displayChangesAppliedToast);
    }

    return {
        activate(replayedUpdateId) {
            lastAppliedUpdateId = replayedUpdateId;
            active = true;
            pendingMessages.splice(0).forEach(enqueueMessage);
        },
        close() {
            connection.close();
        }
    };
}

function getMessageAndStack(error) {
    const message = error?.message || '<unknown error>';
    const stack = error?.stack || message;
    return stack.includes(message) ? stack : message + '\n' + stack;
}

async function getSecret(serverKeyString) {
    if (!serverKeyString || !window.crypto?.subtle) {
        throw new Error('Web Crypto is required to authenticate the browser tools server.');
    }

    const secretBytes = window.crypto.getRandomValues(new Uint8Array(32));
    const binaryServerKey = stringToArrayBuffer(atob(serverKeyString));
    const serverKey = await window.crypto.subtle.importKey(
        'spki',
        binaryServerKey,
        { name: 'RSA-OAEP', hash: 'SHA-256' },
        false,
        ['encrypt']);
    const encrypted = await window.crypto.subtle.encrypt({ name: 'RSA-OAEP' }, serverKey, secretBytes);
    return {
        encryptedSharedSecret: btoa(String.fromCharCode(...new Uint8Array(encrypted))),
        encodedSharedSecret: btoa(String.fromCharCode(...secretBytes))
    };
}

function stringToArrayBuffer(value) {
    const buffer = new ArrayBuffer(value.length);
    const view = new Uint8Array(buffer);
    for (let index = 0; index < value.length; index++) {
        view[index] = value.charCodeAt(index);
    }

    return buffer;
}

function getWebSocket(url, sharedSecret) {
    return new Promise((resolve, reject) => {
        const protocol = sharedSecret ? encodeURIComponent(sharedSecret.encryptedSharedSecret) : [];
        const webSocket = new WebSocket(url, protocol);

        function onOpen() {
            clearEventListeners();
            resolve(webSocket);
        }

        function onClose(event) {
            clearEventListeners();
            reject(event instanceof ErrorEvent ? event.error : 'WebSocket failed to connect.');
        }

        function clearEventListeners() {
            webSocket.removeEventListener('open', onOpen);
            webSocket.removeEventListener('close', onClose);
        }

        webSocket.addEventListener('open', onOpen);
        webSocket.addEventListener('close', onClose);
    });
}
