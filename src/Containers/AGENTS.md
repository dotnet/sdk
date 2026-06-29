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
- **Registry behavior is tuned via `DOTNET_CONTAINER_*` env vars**, each with a legacy
  `SDK_CONTAINER_*` alias — keep both when adding one.

## Tests

- Unit: `test/Microsoft.NET.Build.Containers.UnitTests` (MSTest) — run everywhere.
- Integration: `test/Microsoft.NET.Build.Containers.IntegrationTests` **require a
  container daemon**; they skip when Docker/Podman is absent, so don't
  rely on them running in every CI leg.
