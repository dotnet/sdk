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

### Terminology

We propose using **"daily"** as the primary term, matching the existing `dotnet-install` scripts'
`--quality daily` parameter. The GitHub issue uses "nightly" which is more familiar to users of
other ecosystems (Rust, Python, etc.), but aligning with the existing .NET infrastructure reduces
confusion and implementation complexity.

Alternative names considered:
- **"nightly"**: More familiar to the broader developer community; could be used as a user-facing
  alias even if the underlying quality is "daily"
- **"prerelease"**: Too broad — preview releases are also pre-release but come from the release manifest
- **"ci"**: Accurate but less user-friendly

**Recommendation**: Use "daily" in the implementation and documentation to match `dotnet-install`.
If user research indicates "nightly" resonates better, we can add it as an alias later.

### CLI syntax

#### Installing the latest daily build for a channel

```bash
# Install the latest daily SDK for .NET 10.0
dnup sdk install 10.0 --quality daily

# Install the latest daily SDK for the 10.0.1xx feature band
dnup sdk install 10.0.1xx --quality daily

# Short form (if we want to support it)
dnup sdk install daily/10.0
```

The `--quality` flag mirrors `dotnet-install`'s terminology and is the recommended approach.
It composes naturally with the existing channel argument.

The `daily/{channel}` short form is convenient but introduces a new syntax pattern. It could
be supported as sugar that expands to `--quality daily`.

#### Installing a specific daily build version

```bash
# Install a specific daily build by its full version string
dnup sdk install 10.0.100-preview.7.25351.1
```

When the user provides a fully-specified version with a prerelease tag, dotnetup should:
1. First, check the release manifest (it may be an officially published preview)
2. If not found in the release manifest, attempt to download from the daily build feed

This fallback behavior means users don't need to know whether a version is "released" or "daily" —
they just provide the version string and dotnetup figures out where to get it.

**Important constraint**: blob-feed fallback should only be attempted for **prerelease** version
strings (versions containing a `-` prerelease tag). Stable version strings like `10.0.100` that
aren't in the release manifest should produce a clear "version not found" error rather than probing
blob storage — this avoids confusing behavior for typos like `10.0.999`.

If `--quality daily` is explicitly provided alongside an exact version, that is an error
(quality is only meaningful for "latest" resolution, matching the `dotnet-install` script behavior).

#### Runtime installs

```bash
# Daily runtime builds follow the same pattern
dnup runtime install 10.0 --quality daily
```

### Version resolution

A new resolution path is needed alongside the existing `ChannelVersionResolver`:

```
User provides: channel + quality
                    │
                    ▼
        ┌───────────────────────┐
        │  Is quality specified? │
        └───────┬───────┬───────┘
                │       │
            No  │       │  Yes (daily/preview)
                ▼       ▼
    ┌──────────────┐  ┌──────────────────────┐
    │ Release       │  │ Daily build feed     │
    │ manifest      │  │ (aka.ms / blob)      │
    │ (existing)    │  │                      │
    └──────────────┘  └──────────────────────┘
```

For daily builds, version resolution queries the build feed:
1. Construct the aka.ms URL: `https://aka.ms/dotnet/{channel}/{quality}/sdk-productVersion.txt`
2. Follow the redirect to get the `latest.version` file content
3. Parse the version string
4. Construct the download URL: `{feed}/Sdk/{version}/dotnet-sdk-{version}-{os}-{arch}.{ext}`

For a specific prerelease version that isn't in the release manifest:
1. Construct the download URL directly: `{feed}/Sdk/{version}/dotnet-sdk-{version}-{os}-{arch}.{ext}`
2. Verify the archive exists (HEAD request or attempt download)
3. Fetch the hash from `{url}.sha512` for verification

### Manifest tracking

Daily builds need to be tracked in the dotnetup manifest so that:
- `dnup list` shows them
- `dnup remove` can remove them
- The garbage collector knows about them

A daily install should be recorded with enough information to identify its source:

```json
{
  "channel": "10.0",
  "quality": "daily",
  "version": "10.0.100-preview.7.25351.1",
  "component": "sdk",
  "feedKind": "daily-build"
}
```

The `quality` and `feedKind` fields distinguish daily installs from release-manifest installs.
(Note: the existing `InstallSource` field means something different — it tracks whether the
install was triggered explicitly by the user vs. by `global.json`. We use `feedKind` to avoid
naming confusion.)

This distinction is important because:
- The same version string might not exist in the release manifest
- Update/GC logic must distinguish `{channel: "10.0", quality: "daily"}` from `{channel: "10.0"}`
  (stable) — otherwise daily installs would be garbage-collected by stable channel rules
- The download source is different

### Update behavior

**Daily builds should not auto-update by default.** The issue author confirms this assumption:
> "I assume we don't update specific nightlies."

However, we should support explicit updates:
```bash
# Explicitly update to the latest daily for the tracked channel
dnup sdk update --quality daily
```

When a user has an install spec like `{channel: "10.0", quality: "daily"}`, running `dnup sdk update`
with `--quality daily` would resolve the latest daily for 10.0 and install it if newer.

Install specs for daily builds should be recorded only when the user installs via channel + quality
(not when they install a specific version). A specific-version daily install is a point-in-time
snapshot and shouldn't imply ongoing tracking.

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

### Phase 1: Data model and source identity

**Goal**: Introduce the concept of build quality and feed kind into the request/manifest/spec model.

Changes needed:
- Add `Quality` property to `InstallRequestOptions` (enum: `Ga`, `Preview`, `Daily`)
- Add `FeedKind` property to install spec recording (enum: `ReleaseManifest`, `DailyBuild`)
- Update `UpdateChannel` identity: a channel + quality pair must be distinct from the same channel
  without quality, so update/remove/GC logic doesn't confuse daily and stable installs
- Update manifest serialization to persist `quality` and `feedKind`
- Update `dnup list` output to surface daily-build provenance

### Phase 2: Install a specific daily build version

**Goal**: `dnup sdk install 10.0.100-preview.7.25351.1` works for versions not in the release manifest.

Changes needed:
- Extend `DotnetArchiveDownloader` with a blob-storage download path that constructs URLs from
  version strings rather than release manifest entries
- Add hash fetching from `{url}.sha512` companion files
- Add host allowlist for blob feed redirect validation
- Modify `InstallWorkflow.ResolveSpec()` to fall back to blob storage when the release manifest
  doesn't contain the requested prerelease version
- Track the install in the manifest with `feedKind: "daily-build"`

### Phase 3: Install latest daily for a channel

**Goal**: `dnup sdk install 10.0 --quality daily` resolves and installs the latest daily build.

Changes needed:
- Add `--quality` option to install commands (validate: incompatible with exact versions, `lts`, `sts`)
- Create a `DailyBuildVersionResolver` (or extend `ChannelVersionResolver`) that queries
  `aka.ms` / `latest.version` files
- Wire quality through the install workflow
- Record install spec with quality so updates can re-resolve from the daily feed

### Phase 4: List and browse daily builds

**Goal**: Users can discover what daily builds are available.

This phase is more exploratory. Options include:
- Querying the blob storage for available versions (may require an index endpoint)
- Listing recent builds from the aka.ms redirects
- An interactive picker in the TUI showing recent daily versions for a channel

The feasibility depends on what listing/enumeration APIs are available from the build feeds.

## Open questions

1. **Should `dnup sdk install 10.0 --quality preview` also be supported?** The `dotnet-install`
   scripts support `--quality preview` to get the latest preview build. This would be a natural
   extension.

2. **How should `global.json` interact with daily builds?** If a `global.json` specifies a daily
   build version, should `dnup` automatically try the daily feed? Or should it require explicit
   configuration?

3. **Should there be a `--feed` override?** For internal scenarios, users might want to point at
   a different feed URL. The `dotnet-install` scripts support `--azure-feed` for this. If added,
   this should be treated as an advanced/untrusted mode.

4. **What about listing daily builds (Phase 4)?** The blob storage doesn't have a natural listing
   API. We may need to rely on version ranges or a separate index. This needs investigation.

5. **Naming**: Should we use "daily" throughout, or offer "nightly" as a user-facing alias?
   User research / PM input would be valuable here.

6. **Runtime daily builds**: Do daily builds of the runtime follow the same feed structure?
   Need to verify the URL patterns for runtime vs SDK daily downloads.

7. **Host allowlist / redirect policy**: What is the exact set of allowed hosts for aka.ms
   redirects? Need to enumerate all legitimate blob storage hosts used by the .NET build
   infrastructure.

8. **Feed retry order**: What is the exact primary/fallback retry order across
   `builds.dotnet.microsoft.com` and `ci.dot.net/public`? Should we always try primary first,
   or use both in parallel?

9. **Invalid combinations**: What validation and error messages do we want for invalid combos
   like `--quality daily` with `lts`, `sts`, or a fully specified version?

10. **Distinguishing tracked channels**: How do we surface the difference between tracked `10.0`
    (stable) vs tracked `10.0 --quality daily` in `dnup list`, `dnup remove`, and other commands?
