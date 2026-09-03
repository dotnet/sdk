// Activates the dotnet-watch browser tools client. Added to the app by the SDK during dotnet-watch builds.

// Signals to Microsoft.DotNet.HotReload.WebAssembly.Browser.lib.module.js that the app is being watched.
globalThis.__dotnetWatchBrowserTools = true;

import('/_framework/dotnet-browser-tools/browser-tools-bootstrap.js')
    .catch(error => console.debug('Unable to load the dotnet-watch browser tools.', error));
