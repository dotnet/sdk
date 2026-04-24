# Installing Pre-Release (Daily/Nightly) Builds with dotnetup

## Motivation

dotnetup currently installs .NET SDKs and runtimes from the official release manifest (`releases-index.json`).
This covers GA, LTS, and preview releases that have been officially published. However, many internal and
external users need access to **daily builds** — CI-produced builds that have not gone through the full
release process. These are commonly called "nightly builds" in the community.

Use cases for daily builds include:
- Testing upcoming features or bug fixes before they ship in a preview or GA release
- Reproducing issues against the latest source to check if they're already fixed
- Internal .NET team development workflows
- CI pipelines that need to validate against the latest .NET bits

Today, users install daily builds via the `dotnet-install` scripts or by manually downloading archives
from `builds.dotnet.microsoft.com`. dotnetup should provide a first-class experience for this.

See: https://github.com/dotnet/sdk/issues/51097

## Background: How daily builds work today

### dotnet-install script model

The `dotnet-install.sh` and `dotnet-install.ps1` scripts use a **quality + channel** model:

- **Channel**: A version band like `10.0`, `10.0.1xx`, `STS`, `LTS`, or a specific version
- **Quality**: One of `daily`, `preview`, or `ga` (default)

Quality combined with channel constructs a discovery URL:
```
https://aka.ms/dotnet/{channel}/{quality}/sdk-productVersion.txt
```

This aka.ms redirect resolves to a blob on the primary build feed. The resolved version is then used
to construct the download URL:
```
{feed}/Sdk/{version}/dotnet-sdk-{version}-{os}-{arch}.tar.gz
```

### Build feeds

Two feeds serve daily builds:
1. **Primary**: `https://builds.dotnet.microsoft.com/dotnet`
2. **Fallback**: `https://ci.dot.net/public`

### Version discovery

For a given channel and quality, the scripts fetch a `latest.version` file:
```
{feed}/Sdk/{channel}/latest.version
```

This file contains a single line with the latest version string for that channel
(e.g., `10.0.100-preview.7.25351.1`).

### Key constraints in the current model

- Quality cannot be combined with a fully-specified version (quality is only meaningful for "latest")
- Quality is not supported for `LTS` or `STS` meta-channels
- Daily builds are **not** listed in the release manifest (`releases-index.json`)
- Hash verification uses `{feed}/Sdk/{version}/dotnet-sdk-{version}-{os}-{arch}.tar.gz.sha512`

## Proposed design

### Core principle: daily builds are just channels

Rather than introducing a separate `--quality` flag, daily builds are expressed as **channel names**
in dotnetup. This keeps the mental model simple: every install is `dnup sdk install <channel>`,
and daily channels are just another kind of channel alongside `latest`, `preview`, `lts`, etc.

### Terminology

We use **"daily"** as the channel keyword, matching the existing `dotnet-install` scripts'
quality level. The GitHub issue uses "nightly" which is more familiar in other ecosystems
(Rust, Python), but "daily" aligns with the existing .NET build infrastructure.

Alternative names considered:
- **"nightly"**: More community-friendly; could be added as an alias
- **"ci"**: Accurate but less user-friendly
- **"preview"**: Already used for official preview releases from the release manifest

**Recommendation**: Use "daily" as the primary name. Consider "nightly" as an alias if user
research shows it resonates better.

### Channel syntax

Daily channels use a suffix pattern `{version-scope}/daily` or `{version-scope}-daily`:

| Channel | Meaning |
|---------|---------|
| `daily` | Latest daily build (latest major version) |
| `10.0/daily` | Latest daily build for .NET 10.0 |
| `10.0.1xx/daily` | Latest daily build for the 10.0.1xx feature band |

This reads naturally as "10.0, daily" — the version scope comes first (what you want),
then the qualifier (what kind of build). It mirrors how existing channels work: you start
with the version scope and optionally narrow it.

| Existing channels | Daily channels |
|-------------------|---------------|
| `latest` | `daily` |
| `preview` | (already in release manifest) |
| `lts` | (not applicable — daily builds aren't LTS) |
| `10.0` | `10.0/daily` |
| `10.0.1xx` | `10.0.1xx/daily` |

### CLI examples

```bash
# Install the latest daily SDK build
dnup sdk install daily

# Install the latest daily SDK for .NET 10.0
dnup sdk install 10.0/daily

# Install the latest daily SDK for the 10.0.1xx feature band
dnup sdk install 10.0.1xx/daily

# Install a specific daily build by its full version string
dnup sdk install 10.0.100-preview.7.25351.1

# Daily runtime builds follow the same pattern
dnup runtime install 10.0/daily
```

#### Specific version fallback

When the user provides a fully-specified version with a prerelease tag (e.g.,
`10.0.100-preview.7.25351.1`), dotnetup should:
1. First, check the release manifest (it may be an officially published preview)
2. If not found in the release manifest, attempt to download from the daily build feed

**Important constraint**: blob-feed fallback should only be attempted for **prerelease** version
strings (versions containing a `-` prerelease tag). Stable version strings like `10.0.100` that
aren't in the release manifest should produce a clear "version not found" error rather than probing
blob storage — this avoids confusing behavior for typos like `10.0.999`.

### Version resolution

```
User provides: channel
        │
        ▼
  ┌─────────────────────┐
  │ Is it a .../daily    │
  │ channel?            │
  └───────┬───────┬─────┘
          │       │
      No  │       │  Yes
          ▼       ▼
  ┌──────────────┐  ┌──────────────────────┐
  │ Release       │  │ Daily build feed     │
  │ manifest      │  │ (aka.ms / blob)      │
  │ (existing)    │  │                      │
  └──────────────┘  └──────────────────────┘
```

For daily channels, version resolution queries the build feed:
1. Parse the channel: `10.0/daily` → base channel `10.0`, quality `daily`
2. Construct the aka.ms URL: `https://aka.ms/dotnet/10.0/daily/sdk-productVersion.txt`
3. Follow the redirect to get the latest version string
4. Construct the download URL: `{feed}/Sdk/{version}/dotnet-sdk-{version}-{os}-{arch}.{ext}`

For a specific prerelease version that isn't in the release manifest:
1. Construct the download URL directly from the version string
2. Verify the archive exists (HEAD request or attempt download)
3. Fetch the hash from `{url}.sha512` for verification

### Channel parsing in `UpdateChannel`

The `UpdateChannel` class gains awareness of the `/daily` suffix:

```csharp
// New properties
public bool IsDaily => Name.Equals("daily", StringComparison.OrdinalIgnoreCase)
    || Name.EndsWith("/daily", StringComparison.OrdinalIgnoreCase);
public string BaseChannel => IsDaily
    ? Name.Contains('/') ? Name.Substring(0, Name.LastIndexOf('/')) : "latest"
    : Name;
```

The `Matches()` method for daily channels matches any version that would match the base channel.
For example, `10.0/daily` matches any `10.0.x` version, just like `10.0` does today. The
difference is only in how versions are **resolved** (blob feed vs release manifest) and
**tracked** (daily install specs vs release install specs).

### Manifest tracking

Daily builds are tracked in the dotnetup manifest just like any other channel:

```json
{
  "channel": "10.0/daily",
  "version": "10.0.100-preview.7.25351.1",
  "component": "sdk"
}
```

Because the channel name itself encodes the daily nature, no additional fields are needed.
The GC, update, and list logic can use the existing `channel` field to determine resolution
behavior:
- Channel ends with `/daily` (or is exactly `daily`)? → resolve from blob feed
- Otherwise → resolve from release manifest

This keeps the manifest schema unchanged and avoids needing to update every piece of code that
reads install specs.

### Update behavior

**Daily channel installs support updates**, just like any other tracked channel. Running
`dnup sdk update` will re-resolve the `10.0/daily` channel against the blob feed and install
a newer version if available.

**Specific-version daily installs** (e.g., `dnup sdk install 10.0.100-preview.7.25351.1`)
are point-in-time snapshots. They record the exact version as the channel, so there's nothing
to "update to" — same as installing a specific released version today.

### Security and trust model

Daily builds have a **weaker trust model** than release-manifest installs. For released versions,
the hash is obtained from the release manifest (an independent metadata source hosted separately
from the archive). For daily builds, both the archive and the `.sha512` hash file are served from
the same blob storage feed. This protects against download corruption but not against a
compromised feed.

Mitigations:
- **Host allowlist**: aka.ms redirects should only be followed to known, expected blob hosts
  (`builds.dotnet.microsoft.com`, `ci.dot.net`). Redirects to unexpected hosts should be rejected.
- **TLS-only**: All feed communication must be over HTTPS.
- **Transparency**: dotnetup should clearly indicate when an install came from a daily build feed
  rather than the release manifest (e.g., in `dnup list` output and installation messages).
- **Future**: If independent signature verification is added (e.g., verifying the Authenticode
  signature on the extracted binaries), this would strengthen the trust model for daily builds.

If a `--feed` override is added later, it should be treated as an advanced/untrusted mode with
appropriate warnings.

### Hash verification

Daily build archives have SHA-512 hash files available at `{download-url}.sha512`. The existing
hash verification in `DotnetArchiveDownloader` can be extended to fetch the hash from this
companion file instead of from the release manifest.

### Archive signatures

Daily build archives are signed with the same Authenticode/codesign signatures as release builds
(the signing happens in the build pipeline). The archive-level verification (SHA-512) is the same.
No additional signature verification infrastructure is needed for daily builds.

## Implementation phases

### Phase 1: Daily channel parsing and blob-feed download

**Goal**: `dnup sdk install 10.0/daily` resolves and installs the latest daily build.

Changes needed:
- Extend `UpdateChannel` with `IsDaily` / `BaseChannel` properties for `.../daily` suffix parsing
- Add `IsValidChannelFormat()` support for `.../daily` channels
- Create a `DailyBuildVersionResolver` that queries `aka.ms` / `latest.version` files
- Extend `DotnetArchiveDownloader` with a blob-storage download path that constructs URLs from
  version strings rather than release manifest entries
- Add hash fetching from `{url}.sha512` companion files
- Add host allowlist for blob feed redirect validation
- Wire daily channel detection through `InstallWorkflow` to use the daily resolver and downloader

### Phase 2: Specific prerelease version fallback

**Goal**: `dnup sdk install 10.0.100-preview.7.25351.1` works for versions not in the release manifest.

Changes needed:
- Modify `InstallWorkflow.ResolveSpec()` to fall back to blob storage when the release manifest
  doesn't contain the requested prerelease version (non-daily channel, prerelease tag present)
- Reuse the blob-feed download path from Phase 1

### Phase 3: List and browse daily builds

**Goal**: Users can discover what daily builds are available.

This phase is more exploratory. Options include:
- Querying the blob storage for available versions (may require an index endpoint)
- Listing recent builds from the aka.ms redirects
- An interactive picker in the TUI showing recent daily versions for a channel

The feasibility depends on what listing/enumeration APIs are available from the build feeds.

## Open questions

1. **How should `global.json` interact with daily builds?** If a `global.json` specifies a daily
   build version, should `dnup` automatically try the daily feed? Or should it require explicit
   configuration?

2. **Should there be a `--feed` override?** For internal scenarios, users might want to point at
   a different feed URL. The `dotnet-install` scripts support `--azure-feed` for this. If added,
   this should be treated as an advanced/untrusted mode.

3. **What about listing daily builds (Phase 3)?** The blob storage doesn't have a natural listing
   API. We may need to rely on version ranges or a separate index. This needs investigation.

4. **Naming**: Should we use "daily" throughout, or offer "nightly" as a user-facing alias?
   User research / PM input would be valuable here.

5. **Runtime daily builds**: Do daily builds of the runtime follow the same feed structure?
   Need to verify the URL patterns for runtime vs SDK daily downloads.

6. **Host allowlist / redirect policy**: What is the exact set of allowed hosts for aka.ms
   redirects? Need to enumerate all legitimate blob storage hosts used by the .NET build
   infrastructure.

7. **Feed retry order**: What is the exact primary/fallback retry order across
   `builds.dotnet.microsoft.com` and `ci.dot.net/public`? Should we always try primary first,
   or use both in parallel?

8. **Channel separator**: Is `10.0/daily` the best syntax? Alternatives include `10.0:daily`,
   `10.0-daily`. The `/` syntax reads naturally ("10.0 slash daily") and mirrors URL path
   structure, but may conflict with file path parsing on some platforms. The `-` syntax is
   simpler but could be confused with prerelease version tags.
