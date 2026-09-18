// Activates the browser tools client in Blazor apps that render on the server (static SSR or
// Interactive Server), which the browser refresh TagHelper does not reach.
//
// Provider availability signaling is handled separately. The initializer and configuration
// module are app hosted, so resolving the configuration relative to import.meta.url keeps it
// correct under any static web asset base path or fingerprinting scheme.

// Substituted by Microsoft.NET.Sdk.StaticWebAssets.DotNetWatch.targets.
const configModulePath = '__CONFIG_MODULE__';

export async function afterWebStarted() {
    // The browser tools client de-duplicates activation, so importing the configuration module is safe
    // even when another initializer or the browser refresh TagHelper already imported it.
    await import(new URL(configModulePath, import.meta.url).href);
}
