// Activates the browser tools client in Blazor apps that render on the server (static SSR or
// Interactive Server), which the browser refresh TagHelper does not reach.
//
// The provider reports its availability over a stable application route. The configuration module
// is app hosted, so resolving it relative to import.meta.url keeps it correct under any static web
// asset base path or fingerprinting scheme.

// Substituted by Microsoft.NET.Sdk.StaticWebAssets.DotNetWatch.targets.
const settingsPath = '__SETTINGS_PATH__';
const configModulePath = '__CONFIG_MODULE__';
const settingsRequestTimeoutMilliseconds = 5000;

export async function afterWebStarted() {
    if (!await isHotReloadEnabled()) {
        return;
    }

    // The browser tools client de-duplicates activation, so importing the configuration module is safe
    // even when another initializer or the browser refresh TagHelper already imported it.
    await import(new URL(configModulePath, import.meta.url).href);
}

async function isHotReloadEnabled() {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), settingsRequestTimeoutMilliseconds);

    try {
        const response = await fetch(settingsPath, { cache: 'no-store', signal: controller.signal });
        if (!response.ok) {
            return false;
        }

        const settings = await response.json();
        return settings?.hotReload === true;
    } catch (error) {
        console.debug('Unable to read the dotnet-watch browser tools settings.', error);
        return false;
    } finally {
        clearTimeout(timeout);
    }
}
