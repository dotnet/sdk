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
- **Registry behavior is tuned via `DOTNET_CONTAINER_*` env vars.** Many (older) ones
  also carry a legacy `SDK_CONTAINER_*` alias; newer ones (e.g. the push/pull
  credentials, `DOTNET_CONTAINER_INSECURE_REGISTRIES`) are `DOTNET_CONTAINER_*`-only.
  Check the existing constant before assuming an alias exists.

## Tests

- Unit: `test/Microsoft.NET.Build.Containers.UnitTests` (MSTest) — run everywhere.
- Integration: `test/Microsoft.NET.Build.Containers.IntegrationTests` — a mix. Tests
  that need a runtime opt into `DockerUnavailableCondition` and skip when Docker/Podman
  is absent (don't rely on those in every CI leg); others run in-process without a
  container runtime dependency.
