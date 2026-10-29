# Containers Agent Instructions

Guidance for changes under `src/Containers` — the `dotnet publish` container-image
feature (`Microsoft.NET.Build.Containers`).

## Where things live

| Path | Role |
|------|------|
| `Microsoft.NET.Build.Containers/` | The library + MSBuild tasks: `Tasks/` (task entry points), `Registry/` (OCI registry client), `LocalDaemons/` (Docker/Podman/tarball outputs). |
| `packaging/` | Wire the tasks into publish. |
| `containerize/` | Standalone CLI wrapper around the same library. |

## Conventions & gotchas

- **User-facing config is `Container*` MSBuild properties**
- **Diagnostics use `CONTAINER####` codes** — a self-contained scheme, *not* the
  `NETSDK####` sequence from `src/Tasks`.
- **`Resource.FormatString` takes a resource key**, so use
  `nameof(Strings.ResourceName)`, not a literal key or the localized property value.
  See [Resource.cs](Microsoft.NET.Build.Containers/Resources/Resource.cs).
- **Registry behavior is tuned via `DOTNET_CONTAINER_*` env vars.** Many (older) ones
  also carry a legacy `SDK_CONTAINER_*` alias; newer ones (e.g. the push/pull
  credentials, `DOTNET_CONTAINER_INSECURE_REGISTRIES`) are `DOTNET_CONTAINER_*`-only.
  Check the existing constant before assuming an alias exists.
- **Bearer-token (realm) requests are untrusted and use a separate transport.** The
  realm comes from the registry, so `AuthHandshakeMessageHandler` sends token requests
  through [AuthRealmHandlerFactory.cs](Microsoft.NET.Build.Containers/AuthRealmHandlerFactory.cs)
  (Microsoft.Security.AntiSSRF, validated at connect time) rather than the registry
  handler chain. The policy comes only from `DOTNET_CONTAINER_AUTH_REALM_POLICY`
  (`Public` default, `AllowlistOnly`, `None`) and the semicolon-separated IP/CIDR list
  `DOTNET_CONTAINER_AUTH_REALM_ALLOWED_ADDRESSES`; there is no runtime address detection.
  Realm requests may use HTTP only when the realm itself is HTTP and the registry is configured
  as insecure; an HTTPS realm is never downgraded to HTTP.
  Don't send realm requests via `base.SendAsync`; unit tests that fake a token server pass
  `realmHandlerFactory` instead.

## Tests

- Unit: `test/Microsoft.NET.Build.Containers.UnitTests` (MSTest) — run everywhere.
- Integration: `test/Microsoft.NET.Build.Containers.IntegrationTests` — a mix. Tests
  that need a runtime opt into `DockerUnavailableCondition` and skip when Docker/Podman
  is absent (don't rely on those in every CI leg); others run in-process without a
  container runtime dependency.
