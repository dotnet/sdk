# [Breaking change]: dotnetup default hive moves under its data directory

## Description

The default dotnetup-managed .NET install root moves under the dotnetup data directory. This daily-build change does not automatically migrate existing installations. See the [default hive location decision](../../general/dotnetup/designs/default-hive-location.md).

## Version

Daily dotnetup `0.2.0` builds.

## Previous behavior

The default root was the `dotnet` subdirectory of the platform-local data directory, such as `%LocalAppData%\dotnet` on Windows or `~/.local/share/dotnet` on Linux. This generic location could contain installations not owned by dotnetup.

## New behavior

The default root is the `dotnet` subdirectory of the dotnetup data directory, such as `%LocalAppData%\dotnetup\dotnet` on Windows or `~/.local/share/dotnetup/dotnet` on Linux. Explicit `--install-path` and `global.json` SDK paths are unchanged.

Existing roots in `dotnetup_manifest.json` are not moved or removed. They remain visible to `dotnetup list` and are still processed by `dotnetup update` without `--install-path`, but new default installs, `dotnetup dotnet`, and `dotnetup env` use the new root.

## Type of breaking change

Behavioral change.

## Reason for change

The product-owned directory avoids collisions with existing user or installer content and keeps dotnetup metadata and managed installations under one root.

## Recommended action

Install the required SDKs and runtimes again with the new dotnetup build. Run `dotnetup env set` to reapply the stored environment configuration with the new default. Existing shell-profile configuration also resolves the new default automatically when a new shell starts.

The `everywhere` mode is Windows-only. Windows users of that mode must reapply `dotnetup env set everywhere` because it also persists the absolute managed root in the user-level `PATH` and `DOTNET_ROOT` used by command prompts and GUI applications.

After reinstalling anything needed from the old root in the new path, delete the old folder.

## Feature area

dotnetup.

## Affected APIs

None.