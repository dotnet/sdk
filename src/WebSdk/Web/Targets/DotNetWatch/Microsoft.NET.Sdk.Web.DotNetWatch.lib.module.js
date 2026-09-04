// Activates the dotnet-watch browser tools client in Blazor apps that render on the server
// (static SSR or Interactive Server), which the browser refresh TagHelper does not reach.

export async function afterWebStarted() {
    // The browser tools client de-duplicates activation, so importing the configuration module is safe
    // even when another initializer or the browser refresh TagHelper already imported it. The module is
    // app hosted and sits next to this initializer, so resolving it relative to import.meta.url keeps it
    // correct under any static web asset base path or fingerprinting scheme.
    await import(new URL('./Microsoft.NET.Sdk.Web.DotNetWatch.BrowserTools.Config.js', import.meta.url).href);
}
