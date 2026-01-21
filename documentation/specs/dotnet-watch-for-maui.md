# `dotnet watch` for .NET MAUI Scenarios

## Overview

This spec describes how `dotnet watch` provides Hot Reload for mobile platforms (Android, iOS), which cannot use the standard named pipe transport. Similar to how web applications already use websockets for reloading CSS and JavaScript, we will use the same model for mobile applications.

## Transport Selection

| Platform        | Transport  | Reason                                                                        |
|-----------------|------------|-------------------------------------------------------------------------------|
| Desktop/Console | Named Pipe | Existing implementation, Fast, local IPC                                      |
| Android/iOS     | WebSocket  | Named pipes don't work over the network; `adb reverse` tunnels the connection |

`dotnet-watch` detects WebSocket transport via the `HotReloadWebSockets` capability:

```xml
<ProjectCapability Include="HotReloadWebSockets" />
```

Mobile workloads (Android, iOS) add this capability to their SDK targets. This allows any workload to opt into WebSocket-based hot reload.

## SDK Changes ([dotnet/sdk#52581](https://github.com/dotnet/sdk/pull/52581))

### WebSocket Details

`dotnet-watch` already has a WebSocket server for web apps: `BrowserRefreshServer`. This server:

- Hosts via Kestrel on `https://localhost:<port>`
- Communicates with JavaScript (`aspnetcore-browser-refresh.js`) injected into web pages
- Sends commands like "refresh CSS", "reload page", "apply Blazor delta"

For mobile, we reuse the Kestrel infrastructure but with a different protocol:

| Server                     | Client                 | Protocol                                   |
|----------------------------|------------------------|--------------------------------------------|
| `BrowserRefreshServer`     | JavaScript in browser  | JSON messages for CSS/page refresh         |
| `HotReloadWebSocketServer` | Startup hook on device | Binary delta payloads (same as named pipe) |

The mobile server (`HotReloadWebSocketServer`) extends `KestrelWebSocketServer` and speaks the same binary protocol as the named pipe transport, just over WebSocket instead.

### 1. WebSocket Capability Detection

[ProjectGraphUtilities.cs](../../src/BuiltInTools/Watch/Build/ProjectGraphUtilities.cs) checks for the `HotReloadWebSockets` capability (case-insensitive).

### 2. MobileAppModel

Creates a `MobileHotReloadClient` with a WebSocket server instead of named pipes.

### 3. Environment Variables

`dotnet-watch` launches the app via:

```dotnetcli
dotnet run --no-build \
  -e DOTNET_WATCH=1 \
  -e DOTNET_MODIFIABLE_ASSEMBLIES=debug \
  -e DOTNET_WATCH_HOTRELOAD_WEBSOCKET_ENDPOINT=ws://127.0.0.1:<port> \
  -e DOTNET_STARTUP_HOOKS=<path to DeltaApplier.dll>
```

The port is dynamically assigned (defaults to 0, meaning the OS picks an available port) to avoid conflicts in CI and parallel test scenarios. The `DOTNET_WATCH_HOTRELOAD_HTTP_PORT` environment variable can override this if a specific port is needed.

These environment variables are passed as `@(RuntimeEnvironmentVariable)` MSBuild items to the workload. See [dotnet-run-for-maui.md](dotnet-run-for-maui.md) for details on `dotnet run` and environment variables.

## Android Workload Changes (Example Integration)

### [dotnet/android#10770](https://github.com/dotnet/android/pull/10770) — RuntimeEnvironmentVariable Support

Enables the Android workload to receive env vars from `dotnet run -e`:

- Adds `<ProjectCapability Include="RuntimeEnvironmentVariableSupport" />`
- Adds `<ProjectCapability Include="HotReloadWebSockets" />` to opt into WebSocket-based hot reload
- Configures `@(RuntimeEnvironmentVariable)` items, so they will apply to Android.

### [dotnet/android#10778](https://github.com/dotnet/android/pull/10778) — dotnet-watch Integration

1. **Startup Hook:** Parses `DOTNET_STARTUP_HOOKS`, includes the assembly in the app package, rewrites the path to just the assembly name (since the full path doesn't exist on device)
2. **Port Forwarding:** Runs `adb reverse tcp:<port> tcp:<port>` so the device can reach the host's WebSocket server via `127.0.0.1:<port>` (port is parsed from the endpoint URL)
3. **Prevents Double Connection:** Disables startup hooks in `Microsoft.Android.Run` (the desktop launcher) so only the mobile app connects

## Data Flow

1. **Build:** `dotnet-watch` builds the project, detects `HotReloadWebSockets` capability
2. **Launch:** `dotnet run -e DOTNET_WATCH_HOTRELOAD_WEBSOCKET_ENDPOINT=ws://127.0.0.1:<port> -e DOTNET_STARTUP_HOOKS=...`
3. **Workload:** Android build tasks:
   - Include the startup hook DLL in the APK
   - Set up ADB port forwarding for the dynamically assigned port
   - Rewrite env vars for on-device paths
4. **Device:** App starts → StartupHook loads → `Transport.TryCreate()` reads env vars → `WebSocketTransport` connects to `ws://127.0.0.1:<port>`
5. **Hot Reload:** File change → delta compiled → sent over WebSocket → applied on device

## iOS

Similar changes will be made in the iOS workload to opt into WebSocket-based hot reload:

- Add `<ProjectCapability Include="HotReloadWebSockets" />`
- Handle startup hooks and port forwarding similar to Android

## Dependencies

- **[runtime#123964](https://github.com/dotnet/runtime/pull/123964):** [mono] read `$DOTNET_STARTUP_HOOKS` — needed for Mono runtime to honor startup hooks (temporary workaround via `RuntimeHostConfigurationOption`)
