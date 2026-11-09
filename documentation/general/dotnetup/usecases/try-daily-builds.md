---
title: Try .NET daily builds with dotnetup
description: Install, run, update, and remove daily .NET builds.
ms.topic: how-to
ms.date: 08/07/2026
---

# Try .NET daily builds with dotnetup

Daily channels give you bleeding-edge builds from the .NET build feeds. `dotnetup`
makes it easy to try these daily builds in your workspaces.

## Install a daily SDK

Install the latest daily SDK:

```dotnetcli
dotnetup sdk install daily
```

Limit the request to a release, feature band, or development phase:

```dotnetcli
dotnetup sdk install 11-daily
dotnetup sdk install 11.0-daily
dotnetup sdk install 11.0.1xx-daily
dotnetup sdk install 11.0.1xx-preview.5-daily
```

RTM and servicing daily builds can report a release version such as `11.0.100`
from `dotnet --version`, even though dotnetup tracks a build identity such as
`11.0.100-rtm.26552.115`. The build identity lets daily-channel updates distinguish
successive builds with the same release-version filename.

Successive RTM or servicing daily builds, and the matching stable release, install
into the same folder. When the stable release was built from the same commit as the
installed daily build, dotnetup installs it alongside the daily build, since the files
are identical. Otherwise dotnetup doesn't install one build over another; uninstall
the existing one first, for example `dotnetup sdk uninstall 11.0.1xx-daily`,
and then install the newer build.

With `--update-global-json`, dotnetup writes the version the folder uses, such as
`11.0.100`, because that's what the host resolves `global.json` against. Installing
from that `global.json` later uses the daily build already in the folder.

## Isolate a daily SDK

Use a separate installation root when you do not want daily and stable
installations in the same root:

```dotnetcli
dotnetup sdk install daily --install-path .dotnet-daily
```

Run the installation's executable directly:

```powershell
.\.dotnet-daily\dotnet.exe --version
```

On Linux or macOS, run:

```bash
./.dotnet-daily/dotnet --version
```

`dotnetup dotnet` selects the default dotnetup-managed .NET installation root.
It does not automatically select an arbitrary `--install-path`.

## Install daily runtimes

Runtime daily channels use the same suffix:

```dotnetcli
dotnetup runtime install 11.0-daily
dotnetup runtime install aspnetcore@11.0-daily
```

## Update or remove the daily requirement

A daily channel is a rolling requirement:

```dotnetcli
dotnetup sdk update
dotnetup sdk uninstall daily --install-path .dotnet-daily
```

An exact prerelease version is pinned and is not changed by update commands.

## See also

- [Daily channels](../channels/daily.md)
- [Preview channels](../channels/preview.md)
- [Manage custom installation roots](manage-custom-installation-roots.md)
- [How dotnetup verifies downloads](../concepts/download-verification.md)
