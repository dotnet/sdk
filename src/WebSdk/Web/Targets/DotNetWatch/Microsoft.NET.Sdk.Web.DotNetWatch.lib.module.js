// Activates the dotnet-watch browser tools client in Blazor apps that render on the server
// (static SSR or Interactive Server), which the browser refresh TagHelper does not reach.
//
// The initializer is part of every development build, so it always asks the application for the
// browser tools settings first and only starts the tools when a dotnet-watch session enabled them.
// The settings document and the configuration module are app hosted and sit next to this
// initializer, so resolving them relative to import.meta.url keeps them correct under any static
// web asset base path or fingerprinting scheme.

// Substituted by Microsoft.NET.Sdk.StaticWebAssets.DotNetWatch.targets.
const settingsPath = '__SETTINGS_PATH__';
const configModulePath = '__CONFIG_MODULE__';

export async function afterWebStarted() {
    if (!await isHotReloadEnabled()) {
        return;
    }

    // The browser tools client de-duplicates activation, so importing the configuration module is safe
    // even when another initializer or the browser refresh TagHelper already imported it.
    await import(new URL(configModulePath, import.meta.url).href);
}

async function isHotReloadEnabled() {
    try {
        // The document is rewritten in place by dotnet-watch, so it must never be read from a cache.
        const response = await fetch(new URL(settingsPath, import.meta.url).href, { cache: 'no-store' });
        if (!response.ok) {
            return false;
        }

        const settings = await response.json();
        return settings?.hotReload === true;
    } catch (error) {
        console.debug('Unable to read the dotnet-watch browser tools settings.', error);
        return false;
    }
}
