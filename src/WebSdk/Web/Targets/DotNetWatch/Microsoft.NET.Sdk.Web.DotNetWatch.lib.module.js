// Activates the dotnet-watch browser tools client in Blazor apps that render on the server
// (static SSR or Interactive Server), which the browser refresh TagHelper does not reach.

export async function afterWebStarted() {
    // The browser tools client de-duplicates activation, so importing the bootstrap is safe
    // even when another initializer already imported it.
    await import('/_framework/dotnet-browser-tools/browser-tools-bootstrap.js');
}
