# Trying Daily Builds Safely

> **Note:** Daily channel support (`daily`, `10.0-daily`, etc.) is being added in [PR #54312](https://github.com/dotnet/sdk/pull/54312). Installing specific pre-release versions by their full version string is already supported. The examples below reflect the intended experience once daily channels are fully available.

Daily builds are the latest CI-produced .NET builds — they haven't gone through the full release process but let you test upcoming features, verify bug fixes, or validate your code against the bleeding edge. `dotnetup` makes it easy to install daily builds alongside your stable installations without affecting your day-to-day workflow.

## Quick Reference

```bash
# Install the latest daily SDK build
dotnetup sdk install daily

# Install a daily build for a specific .NET version
dotnetup sdk install 10.0-daily

# Install a daily build for a specific feature band
dotnetup sdk install 10.0.1xx-daily

# Install a specific pre-release version from the daily feed
dotnetup sdk install 10.0.100-preview.7.25351.1

# Run a command using the dotnetup-managed SDK
dotnetup dotnet build
dotnetup dotnet test

# Shorthand for the above
dotnetup do build
dotnetup do test
```

## Daily Channels

Daily builds are expressed as channels — the same mental model you already use with `latest`, `lts`, and `preview`. Just add a `-daily` suffix to any version scope:

| Channel | What It Installs |
|---------|-----------------|
| `daily` | Latest daily build for the newest major version |
| `10.0-daily` | Latest daily build for .NET 10.0 |
| `10.0.1xx-daily` | Latest daily build for the 10.0.1xx feature band |

### Installing a daily SDK

```
$ dotnetup sdk install daily
⚠ Daily builds are not code-signed. Only the SHA-512 hash is verified.
Downloading .NET SDK 11.0.100-preview.1.25310.1...  ████████████████████████ 100%
Installed .NET SDK 11.0.100-preview.1.25310.1 to C:\Users\you\.dotnet
```

### Installing a daily runtime

```
$ dotnetup runtime install 10.0-daily
⚠ Daily builds are not code-signed. Only the SHA-512 hash is verified.
Downloading .NET Runtime 10.0.0-preview.7.25351.1...  ████████████████████████ 100%
Installed .NET Runtime 10.0.0-preview.7.25351.1 to C:\Users\you\.dotnet
```

### Installing a specific pre-release version

If you know the exact version (e.g. from a GitHub issue or a colleague), you can install it directly:

```
$ dotnetup sdk install 10.0.100-preview.7.25351.1
Downloading .NET SDK 10.0.100-preview.7.25351.1...  ████████████████████████ 100%
Installed .NET SDK 10.0.100-preview.7.25351.1 to C:\Users\you\.dotnet
```

When you provide a fully specified pre-release version that isn't in the official release manifest, dotnetup automatically falls back to the daily build feed.

## Running Commands with `dotnetup dotnet`

The `dotnetup dotnet` command (shorthand: `dotnetup do`) forwards any arguments to the `dotnet` CLI from your dotnetup-managed install. This is the easiest way to use daily builds without modifying your system PATH:

```
$ dotnetup dotnet --version
11.0.100-preview.1.25310.1

$ dotnetup dotnet build
  myproject → bin\Debug\net11.0\myproject.dll

Build succeeded.

$ dotnetup dotnet test
  Starting test execution...
  Passed! - 42 tests passed.

$ dotnetup do run -- --my-app-arg
  Hello from .NET 11!
```

### How `dotnetup dotnet` works

When you run `dotnetup dotnet <args>`:

1. dotnetup resolves the managed install directory (e.g. `~/.dotnet`)
2. It sets `DOTNET_ROOT` to that directory
3. It prepends the install directory to `PATH`
4. It launches `dotnet <args>` as a child process
5. The child process exit code becomes the `dotnetup` exit code

This means your system `dotnet` remains untouched — IDE integrations, other terminal sessions, and system services continue to use the system .NET.

## Side-by-Side with Stable Installs

Daily builds install into the same dotnetup-managed directory as your stable SDKs. The .NET SDK host naturally handles multiple installed versions via `global.json` and roll-forward policies:

```
$ dotnetup list

Installations (managed by dotnetup):

  C:\Users\you\.dotnet

    Tracked channels:
      SDK latest                          (source: explicit)
      SDK daily                           (source: explicit)

    Installed versions:
      SDK 10.0.100                        (x64)
      SDK 11.0.100-preview.1.25310.1      (x64)

Total: 2
```

Your stable projects continue to use 10.0.100 (via their `global.json`), while you can test against the daily build when needed.

## Updating Daily Builds

Daily channel installs support updates, just like any other tracked channel:

```
$ dotnetup update
Downloading .NET SDK 11.0.100-preview.1.25315.1... ████████████████████████ 100%
Installed .NET SDK 11.0.100-preview.1.25315.1 to C:\Users\you\.dotnet
SDK 10.0.100 is already up to date.
```

Running `dotnetup sdk update` re-resolves the `daily` channel and installs a newer version if one is available.

> **Note:** Specific-version installs (e.g. `dotnetup sdk install 10.0.100-preview.7.25351.1`) are pinned — they record the exact version as the channel and won't be updated.

## Using Daily Builds with global.json

If your project's `global.json` specifies a pre-release version that isn't in the official release manifest, dotnetup automatically resolves it from the daily build feed:

```json
{
  "sdk": {
    "version": "11.0.100-preview.1.25310.1",
    "allowPrerelease": true
  }
}
```

```
$ dotnetup sdk install
SDK 11.0.100-preview.1.25310.1 will be installed since ~/src/myproject/global.json
specifies that version.
Downloading .NET SDK 11.0.100-preview.1.25310.1...  ████████████████████████ 100%
Installed .NET SDK 11.0.100-preview.1.25310.1 to /home/you/.dotnet
```

## Trust and Security

Daily builds have a different trust model compared to official releases:

| | Official Releases | Daily Builds |
|--|-------------------|-------------|
| **Hash source** | Release manifest (independent metadata) | `.sha512` companion file (same feed as archive) |
| **Code signing** | Authenticode-signed binaries | Not code-signed |
| **Release process** | Full validation and signing pipeline | CI-produced on every commit or daily schedule |

dotnetup verifies the SHA-512 hash of every download, but daily builds are not code-signed. dotnetup clearly indicates this during installation:

```
⚠ Daily builds are not code-signed. Only the SHA-512 hash is verified.
```

## Common Workflows

### Testing a bug fix before it ships

```bash
# Install the latest daily build
dotnetup sdk install daily

# Test your project against it
dotnetup dotnet build
dotnetup dotnet test
```

### Validating your library against a future .NET version

```bash
# Install the next major version's daily
dotnetup sdk install 11.0-daily

# Build your library targeting the new TFM
dotnetup dotnet build -f net11.0
```

### CI pipeline testing against daily builds

In a CI environment (non-interactive), daily builds install without prompts:

```bash
dotnetup sdk install daily --no-progress
dotnetup dotnet test --logger "trx"
```

### Cleaning up

To stop tracking a daily channel and clean up:

```bash
dotnetup sdk uninstall daily
```

## Next Steps

- [Getting Started](./getting-started.md) — First-time setup and configuration
- [Install SDKs with global.json](./install-with-global-json.md) — Project-specific SDK management
- [Update Installations](./update-installations.md) — Keep all your .NET installations current
