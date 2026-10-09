---
title: How dotnetup verifies downloads
description: Learn how dotnetup verifies .NET release manifests and archives, how daily builds differ, and how administrators can block unsigned downloads.
ms.topic: conceptual
ms.date: 10/09/2026
---

# How dotnetup verifies downloads

`dotnetup` verifies the .NET SDKs and runtimes that it downloads before it
extracts them. The type of verification depends on where the build comes from.

## Released builds

For released builds, `dotnetup` uses a chain of trust that starts with a signed
release manifest:

1. `dotnetup` downloads the .NET releases index and the `releases.json` file
   for the selected channel.
1. `dotnetup` verifies the detached code signature (`.p7s` file) of each
   manifest. If the signature is missing or invalid, the command fails.
1. The verified manifest contains the SHA-512 hash of each archive.
1. `dotnetup` downloads the archive and compares its SHA-512 hash to the hash
   in the manifest. If the hashes don't match, the command fails without
   extracting the archive.

Because the signed manifest pins each archive's hash, `dotnetup` doesn't verify
a separate signature for each archive.

Signature verification also checks certificate revocation status online.
Allow access to the required certificate revocation endpoints. If the network
blocks these checks, verification fails and `dotnetup` stops the installation,
even when the signature is otherwise valid.

### Manifest expiration

Release manifests contain an expiration time. `dotnetup` checks that the
manifest hasn't expired, even if its signature and archive hashes are valid.
It also checks that the manifest was signed before its expiration time.

An incorrect system clock or an expired manifest can cause verification to
fail. Don't edit the manifest or change the clock to bypass this check.

### Trusted certificates

For manifest signatures and timestamps, `dotnetup` uses trusted root
certificates bundled with the tool. It doesn't add certificates from the
operating system trust store to those trust roots.

Adding a certificate to the operating system trust store doesn't make
`dotnetup` trust a release-manifest signature from that certificate's chain.
The signature must satisfy the dotnetup verification policy and chain to a
bundled trusted root.

## Daily and unlisted prerelease builds

Daily builds, and prerelease versions that aren't in the signed release
manifest, come from a different feed. These builds have a SHA-512 hash file but
no code signature. For these builds, `dotnetup` verifies only the SHA-512 hash.

`dotnetup` uses this path when you install:

- A daily channel, such as `daily` or `10.0-daily`.
- A fully specified prerelease version that isn't in the release manifest.

Before installation, `dotnetup` warns that the build isn't code-signed and
only its SHA-512 hash is verified.

For more information, see
[Try .NET daily builds with dotnetup](../usecases/try-daily-builds.md).

## Block unsigned downloads

Administrators can block installation of builds that aren't code-signed. When
this policy is in effect, `dotnetup` still installs released builds, but
installations of daily and unlisted prerelease builds fail.

### Windows

Create the `BlockUnsignedDownloads` registry value as a `REG_DWORD` with a
nonzero value under `HKLM\SOFTWARE\Policies\Microsoft\dotnet\Dotnetup`. For
example, run these commands in an elevated PowerShell session:

```powershell
$key = 'HKLM:\SOFTWARE\Policies\Microsoft\dotnet\Dotnetup'
New-Item -Path $key -Force | Out-Null
New-ItemProperty -Path $key -Name 'BlockUnsignedDownloads' -PropertyType DWord -Value 1 -Force | Out-Null
```

To clear the policy, delete the value or set it to `0`.

### Linux and macOS

Create the `/etc/dotnet/dnup-block-unsigned-downloads` file. The file's content
doesn't matter. For example:

```bash
sudo mkdir -p /etc/dotnet
sudo touch /etc/dotnet/dnup-block-unsigned-downloads
```

To clear the policy, delete the file.

When a policy blocks installation, choose a released version or ask an
administrator to clear the policy.

## The dotnetup executable

The `get-dotnetup` download scripts verify the downloaded `dotnetup`
executable with its SHA-512 checksum file. For more information, see
[Get started with dotnetup](../README.md).

## See also

- [How dotnetup works](how-dotnetup-works.md)
- [Channels and versions](channels.md)
- [Daily channels](../channels/daily.md)
- [Signature verification design](../signature-verification.md)
