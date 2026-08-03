# [Breaking change]: dotnetup default hive moves under its data directory

## Description

The default dotnetup-managed .NET install root moves under the dotnetup data directory. This daily-build change does not automatically migrate existing installations. See the [default hive location decision](../../general/dotnetup/designs/default-hive-location.md).

## Version

Daily dotnetup `0.2.0` builds.

## Previous behavior

The default root was the `dotnet` child of the platform-local data directory, such as `%LocalAppData%\dotnet` on Windows or `~/.local/share/dotnet` on Linux. This generic location could contain installations not owned by dotnetup.

## New behavior

The default root is the `dotnet` child of the dotnetup data directory, such as `%LocalAppData%\dotnetup\dotnet` on Windows or `~/.local/share/dotnetup/dotnet` on Linux. Explicit `--install-path` and `global.json` SDK paths are unchanged.

Existing roots in `dotnetup_manifest.json` are not moved or removed. They remain visible to `dotnetup list` and are still processed by `dotnetup update` without `--install-path`, but new default installs, `dotnetup dotnet`, and `dotnetup env` use the new root.

## Type of breaking change

Behavioral change.

## Reason for change

The product-owned directory avoids collisions with existing user or installer content and keeps dotnetup metadata and managed installations under one root.

## Recommended action

Install the required SDKs and runtimes again with the new dotnetup build. Existing shell-profile configuration runs `dotnetup env script` at shell launch, so it automatically resolves the new default when a new shell starts; no profile rewrite is required. Already-open shells retain their previous environment and should be replaced with a new shell.

The `everywhere` mode is Windows-only. Windows users of that mode must reapply `dotnetup env set everywhere` because it also persists the absolute managed root in the user-level `PATH` and `DOTNET_ROOT` used by command prompts and GUI applications.

Use an explicit `--install-path` when operating on a retained old root. Do not delete it until `dotnetup list` and the filesystem confirm that its remaining content is disposable.

## Feature area

SDK.

## Affected APIs

None.