# Project-local SDK setup with dotnetup

## Recommendation

Add one explicit SDK setup path built from existing concepts:

```bash
dotnetup sdk install [<version-or-channel>] --install-path .dotnet --update-global-json
```

The proposal is to extend `--update-global-json` so it can do the repository
configuration work directly:

1. Find the repository SDK configuration root.
2. If no `global.json` exists, create one at the repository root when the user
   supplied an SDK version or channel.
3. Install the requested SDK into the explicit `--install-path`.
4. Create or merge `global.json` so a .NET 10+ host resolves SDKs from the
   install path before falling back to the normal host roots.
5. Track the install in dotnetup's shared manifest as a repository root so
   updates, list, uninstall, and garbage collection can reason about
   dotnetup-owned files.

For an exact SDK pin, the resulting `global.json` shape is:

```jsonc
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "disable",
    "paths": [ ".dotnet", "$host$" ]
  }
}
```

`$host$` is the `sdk.paths` token for the normal host SDK search roots. Listing
`.dotnet` first and `$host$` second means the .NET host searches the repository
SDK root first, then falls back to the SDKs it would normally consider.
`allowPrerelease` is omitted for stable SDK requests and appears only for
prerelease or preview requests.

This is the proposed v1. Earlier design directions (`--local` and
`--install-path here`) were rejected in favor of this approach.

## Why this feature is worth adding

Many .NET repositories already carry repo bootstrap scripts that download an SDK
into a checkout-local `.dotnet/` directory. Those scripts exist because a clean
checkout often needs a toolchain before the developer can build, run tests, or
restore tools. They also exist because teams do not always want to mutate PATH,
shell profiles, system installs, or user-level `DOTNET_ROOT` just to work in one
repository.

dotnetup should not add this feature merely to be another spelling of:

```bash
dotnet-install --install-dir .dotnet
```

The value dotnetup adds is the part ad-hoc scripts commonly get wrong or leave
for every repository to maintain:

- Write a correct .NET 10+ `global.json` `sdk.paths` entry.
- Keep the installed SDK, the `global.json` version, and the roll-forward policy
  consistent.
- Preserve existing `global.json` content instead of overwriting repository
  settings.
- Avoid PATH, shell profile, and `DOTNET_ROOT` mutation.
- Track what dotnetup installed so update, list, uninstall, and garbage
  collection can remain safe.
- Apply security rules for repository-owned install roots, especially symlink
  and reparse-point handling.
- Give one documented, cross-platform workflow for "clone repo, install SDK,
  build".

This is primarily a repository bootstrap feature, not a general local/global
install mode.

## Goals

- Make a clean checkout buildable with a single dotnetup command.
- Use a portable `global.json` representation that can be committed.
- Keep the install scoped to the repository unless `$host$` fallback is needed.
- Preserve dotnetup's update/list/uninstall/GC story for files dotnetup owns.
- Make all repository mutations explicit in help text and command output.
- Keep the first version narrow enough to review, test, and explain.

## Non-goals

- Do not add `--local`.
- Do not add root-level `dotnetup install --local`.
- Do not add a magic `here` value for `--install-path`.
- Do not configure runtimes independently for project-local runtime resolution.
- Do not modify PATH, shell profiles, user-level `DOTNET_ROOT`, or machine-level
  `DOTNET_ROOT`.
- Do not point committed `global.json` files at user-specific dotnetup hive paths.
- Do not use symlinks or junctions for the v1 repository projection model.
- Do not solve cross-repository SDK deduplication in v1.
- Do not support cross-architecture repo-local installs in v1.
- Do not make hermetic SDK resolution the default.
- Do not auto-generate `global.json` for unversioned directories or non-Git
  version control systems. Users in those environments can create `global.json`
  first.

## Vocabulary

| Term | Meaning |
| --- | --- |
| dotnetup shared manifest | dotnetup's user-level state file that records install specs, dotnet roots, installed versions, and dotnetup-owned subcomponents. |
| install spec | The user's requested component and version/channel, plus where the request came from, such as an explicit command or a `global.json` file. |
| dotnetup hive | dotnetup's user-level managed install store. This is the normal place dotnetup installs SDKs and runtimes outside this proposed repo-owned setup mode. |
| repository root | In this document, the directory dotnetup chooses for repository SDK setup. It is normally the directory containing the nearest `global.json`; if no `global.json` exists, it is the Git repository root. |
| `$host$` | A `global.json` `sdk.paths` token for the normal host SDK search roots. |

## Host contract

This proposal depends on .NET 10+ host support for `global.json` `sdk.paths`.
The Microsoft Learn article
[Test prerelease .NET SDKs locally with global.json paths](https://learn.microsoft.com/dotnet/core/tools/test-prerelease-sdk-locally#how-sdkpaths-works)
documents the contract used here:

- `sdk.paths` is an ordered array of SDK search roots.
- Relative paths are resolved relative to the `global.json` location, not the
  current working directory.
- `$host$` means the normal host SDK roots.
- .NET 10 and later hosts recognize `sdk.paths`; older hosts ignore it.

If that host contract changes before .NET 10 ships, this spec should be updated
before implementation proceeds.

Before implementation starts, maintainers should confirm with the .NET host team
that `sdk.paths` is intended to be a supported contract for committed
repository-level `global.json` files, not only an experimental prerelease testing
escape hatch.

## Command surface

| Command | Meaning |
| --- | --- |
| `dotnetup sdk install --install-path .dotnet --update-global-json` | From the repository root, read the nearest `global.json`, install that SDK into `.dotnet/`, and merge `.dotnet` into `sdk.paths`. |
| `dotnetup sdk install <version> --install-path .dotnet --update-global-json` | From the repository root, install an exact SDK version into `.dotnet/` and create/merge `global.json`. |
| `dotnetup sdk install <channel> --install-path .dotnet --update-global-json` | From the repository root, resolve a channel, install the resolved SDK into `.dotnet/`, write the concrete version to `global.json`, and track the original channel in the manifest. |
| `dotnetup sdk install <version-or-channel> --install-path .dotnet --update-global-json --update-gitignore` | Also add `.dotnet/` to `.gitignore` if needed. |

| Option | Description |
| --- | --- |
| `--update-global-json` | Creates or updates `global.json`. When `--install-path` is also provided, it also merges the install path into `sdk.paths`. |
| `--install-path <path>` | Installs the SDK into the specified path. Relative paths are resolved from the current working directory. For repository SDK setup, run from the repository root and use a portable repo-relative path such as `.dotnet`. |
| `--update-gitignore` | Requests a `.gitignore` edit so `.dotnet/` is ignored. Without this option, dotnetup warns if `.dotnet/` is not ignored. |

Root-level `dotnetup install --local` and `dotnetup sdk install --local` are not
part of this proposal.

## User experience

### Existing `global.json`

```bash
git clone https://example/repo
cd repo
dotnetup sdk install --install-path .dotnet --update-global-json
dotnet build
```

If `dotnet build` selects an unexpected SDK, see
[Host prerequisites](#host-prerequisites). The `.dotnet` path is honored by .NET
10 and later hosts.

If `global.json` already contains:

```jsonc
{
  "sdk": {
    "version": "10.0.100"
  }
}
```

dotnetup installs SDK `10.0.100` into `.dotnet/` and updates `global.json` to:

```jsonc
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "disable",
    "paths": [ ".dotnet", "$host$" ]
  }
}
```

### New `global.json`

```bash
dotnetup sdk install 10.0.100 --install-path .dotnet --update-global-json
```

If no `global.json` exists, dotnetup creates one at the repository root using the
requested version or channel. If the user omits both a version/channel and an
existing `global.json`, dotnetup should fail and ask for an explicit SDK version
or channel. This avoids pinning a new repository to whatever SDK happened to be
ambiently resolved on one machine.
Creating `global.json` at the repository root does not change how
`--install-path` is resolved; relative install paths still resolve against the
current working directory.

If no `global.json` exists and the current directory is not inside a repository,
dotnetup should fail with a clear message:

```text
No global.json was found and the current directory is not inside a repository.
Run this command from inside a Git repository, or create a global.json first.
--update-global-json requires either an existing global.json or a repository root
where it can create one.
```

This keeps normal install defaults available for plain installs while avoiding
surprising or non-deterministic `global.json` creation.

### Running from a subdirectory

`--install-path` keeps normal CLI path behavior: relative paths are resolved
against the current working directory. When `--update-global-json` writes
`sdk.paths`, dotnetup computes a portable relative path from the `global.json`
directory to the resolved install path.

For example, if `repo/global.json` exists:

```bash
cd repo/src/App
dotnetup sdk install --install-path .dotnet --update-global-json
```

dotnetup updates `repo/global.json`, installs the SDK into
`repo/src/App/.dotnet`, and writes a relative `sdk.paths` entry that points from
`repo/global.json` to that directory. To get `repo/.dotnet`, run the command
from `repo` or pass an install path that resolves there.

```jsonc
"paths": [ "src/App/.dotnet", "$host$" ]
```

When the SDK version is read from an existing `global.json` and no explicit
version/channel argument is provided, dotnetup should preserve the existing
`rollForward` value. If `rollForward` is absent, dotnetup treats the version as
an exact pin and writes `disable`.

| Existing `rollForward` | Dotnetup manifest source | Write behavior |
| --- | --- | --- |
| absent | exact version from `global.json` | write `disable` |
| `disable` | exact version from `global.json` | preserve |
| `latestPatch` | feature-band channel, such as `10.0.1xx` | preserve |
| `latestFeature` | major/minor channel, such as `10.0` | preserve |
| `latestMinor` | major channel, such as `10` | preserve |
| `latestMajor` | `latest` approximation | preserve |

### Channel setup

```bash
dotnetup sdk install 10.0.1xx --install-path .dotnet --update-global-json
```

dotnetup resolves the channel to a concrete SDK version, installs that version,
writes the concrete version to `global.json`, and records the original channel in
the dotnetup manifest so `dotnetup sdk update` can advance it later.

## Why `--update-global-json` plus `--install-path`

`--update-global-json` is the recommended API because it makes the repository
configuration side effect explicit without adding a new special-purpose option.

Pros:

- Reuses `sdk install`, `--install-path`, and the existing
  `--update-global-json` concept.
- Avoids the overloaded word `local`.
- Avoids a magic install-path sentinel such as `here`.
- Keeps the scenario SDK-specific.
- Lets help text describe the side effects precisely:
  "`--update-global-json` creates or updates `global.json`; when `--install-path`
  is provided, the path is also added to `sdk.paths`."
- Supports the project-local SDK scenario as a composition of existing options:
  install into `.dotnet` and update `global.json` to point there.

This extends existing `dotnetup sdk install --update-global-json` behavior. Today
that flag updates the resolved concrete SDK version in an existing `global.json`
when one is found by normal `global.json` discovery. It does not define a
repository bootstrap story when no `global.json` exists. For this scenario it
should also:

- create `global.json` at the repository root when none exists and the current
  directory is inside a repository;
- fail if no `global.json` exists and the current directory is not inside a
  repository;
- write or update `sdk.version`;
- write the appropriate `sdk.rollForward` and `sdk.allowPrerelease` values;
- when `--install-path` is provided, merge that path into `sdk.paths`; add
  `$host$` only when dotnetup creates a new `sdk.paths` array.

This is intended as a compatible expansion for successful repository setup. The
only newly specified failure is the no-`global.json`, no-repository case: if
today's implementation already fails there, this preserves the failure with a
better diagnostic; if it currently no-ops, the design intentionally changes that
explicitly requested configuration update into an actionable failure rather than
success-shaped behavior.

This keeps the normal "install does not edit `global.json` unless asked"
behavior. Repository SDK setup asks for the edit explicitly with
`--update-global-json`.
Creating or updating `global.json` belongs to `--update-global-json` itself;
adding `sdk.paths` belongs only to the combination of `--update-global-json` and
`--install-path`.

## Why not `--local`

`--local` should not be used for this feature.

dotnetup already has several "local-ish" concepts:

- user-level vs system-level installation;
- archive installs vs system installers;
- PATH-visible vs not-on-PATH installs;
- dotnetup-managed user hives;
- repository-scoped SDK resolution.

`--local` does not tell the user which one is meant. In this feature the most
important side effect is not just where files are copied; it is that dotnetup
updates `global.json` so a repository uses a specific SDK search path. The API
should avoid a term that can be confused with user-local or non-admin installs.

## Install-root decision

Use a repository-owned install root for v1. The recommended path is `.dotnet/`
at the repository root.

```text
repo/
  global.json
  .dotnet/
    dotnet
    sdk/
      10.0.100/
```

The committed `global.json` can then use a portable relative path:

```jsonc
"paths": [ ".dotnet", "$host$" ]
```

This is the key product reason to choose a repo-owned root. A team member, CI
job, or contributor can clone the repository and see the same path value. The
path does not contain a user profile, machine-specific dotnetup location, or
absolute drive root.

When `--update-global-json` is used for repository setup, the resolved
`--install-path` must stay inside the selected repository. Paths outside the
repository should fail unless a future design explicitly allows non-portable or
user-specific paths.

v1 should install the SDK for the native architecture of the running dotnetup
process. Architecture overrides for repository SDK setup are out of scope until
dotnetup's broader cross-architecture story is designed.

### Rejected: dotnetup-managed hive in `global.json`

The dotnetup-managed hive should not be the v1 install root for this scenario.

The problem is portability. Today, `global.json` `sdk.paths` has `$host$`, but it
does not have a `$dotnetup$`, `$home$`, or similar placeholder that expands to a
user-specific dotnetup hive. Writing a path like:

```jsonc
"paths": [ "C:\\Users\\alice\\AppData\\Local\\dotnetup\\dotnet", "$host$" ]
```

or:

```jsonc
"paths": [ "/Users/alice/.dotnetup/dotnet", "$host$" ]
```

is not suitable for a committed repository file. It works only for the user who
created it.

If a future .NET host supports a portable dotnetup-root token, the managed-hive
model should be reconsidered. Until then, it does not meet the clean-checkout and
team-bootstrap goals.

### Rejected for v1: hive payload plus `.dotnet` symlink/junction

A hybrid model could install payloads in the dotnetup hive and create a
repository `.dotnet` symlink or junction that points to the hive. That would
deduplicate payloads while preserving a portable `global.json`.

Do not use that model for v1.

It introduces a second lifecycle and security problem: the repo path is a
reparse point. dotnetup would need to create, validate, repair, and garbage
collect links across Windows, macOS, Linux, developer machines, CI, containers,
and repository cleanup tools. The security rules for "do not follow an existing
`.dotnet` reparse point" also become harder to explain if dotnetup itself is
expected to create one.

Repo-owned `.dotnet/` duplicates SDK payloads across repositories, but it is
simple, portable, inspectable, and matches existing repository bootstrap
patterns. Deduplication can be a later feature if disk usage proves to be a
larger problem than simplicity and portability.

## Host prerequisites

`global.json` `sdk.paths` requires a .NET 10 or later host.
See [Host contract](#host-contract) for the underlying SDK-resolution contract.

dotnetup must make this visible because older hosts ignore `sdk.paths`. If a user
sets up a repository with `.dotnet/` but later runs `dotnet build` through a .NET
8 or .NET 9 host, the host will not honor the path and may select a different
SDK.

Requirements:

- Detect the active host version when possible.
- Warn clearly if the active host is older than .NET 10.
- Explain that the SDK was installed, but normal `dotnet` commands need a .NET
  10+ host to honor `global.json` `sdk.paths`.
- Do not mutate PATH or `DOTNET_ROOT` to hide this requirement.

Example warning:

```text
The SDK was installed to .dotnet and global.json was updated.
Warning: the active dotnet host is 9.0.0. global.json sdk.paths is honored by
.NET 10 and later hosts. Commands run through this host may ignore .dotnet.
```

## Root discovery

For `--update-global-json`, dotnetup should find the SDK configuration root using
this order:

1. If a `global.json` is found by walking upward from the current directory
   without crossing the Git repository boundary, use the directory containing
   that `global.json`. If there is no Git boundary, use a discovered
   `global.json` only when it is in the current directory.
2. If no `global.json` exists and the current directory is inside a Git
   repository, use the repository root and create `global.json` there.
3. If no `global.json` exists and the current directory is not inside a Git
   repository, fail with a message explaining that `--update-global-json` needs
   either an existing `global.json` or a repository root where it can create one.

This intentionally makes repository creation stricter than normal install. A
plain install can work outside a repository; `--update-global-json` mutates
repository configuration and therefore needs either an existing `global.json` or
a repository root.

The upward walk has a ceiling:

- If dotnetup encounters a `.git` directory or file while walking upward, it
  checks that directory for `global.json` and then stops. It should not cross the
  Git repository boundary and mutate an unrelated parent `global.json`.
- If there is no `.git` boundary, dotnetup may walk to the filesystem root only
  to discover whether a `global.json` exists in the current directory. For v1, it
  should fail rather than mutate a parent `global.json` unless the discovered
  `global.json` is in the current directory. A future explicit root option can
  relax this.

Before writing files, dotnetup should print the resolved repository SDK
configuration root and `global.json` path. This is especially important when the
command is run from a subdirectory and a Git boundary scopes which parent
`global.json` can be selected.

Nested `global.json` files are allowed. The nearest `global.json` wins, matching
the host's SDK resolution model. This means a monorepo can intentionally have
more than one repository SDK setup root. If that is not desired, the user should
run the command from the directory associated with the intended `global.json`.

A future `--root <path>` option can be considered for monorepos or unusual
layouts, but it is not required for v1.

## `global.json` write policy

The command must never blindly overwrite `global.json`.

Requirements:

- Preserve unknown top-level sections such as `tools`, `msbuild-sdks`, and any
  future sections.
- Preserve unknown properties inside `sdk` unless the command intentionally
  updates that property.
- When `--install-path` is provided, merge the resolved install path into
  `sdk.paths` instead of hard-coding `.dotnet`.
- Resolve relative `--install-path` values against the current working
  directory, then write a relative path from the `global.json` directory to that
  resolved install path.
- If `sdk.paths` does not exist, write the install path first and `$host$`
  second.
- If `sdk.paths` already exists, prepend the install path unless it is already
  present. Preserve the relative order of existing entries, including existing
  custom paths and `$host$`. This intentionally makes the SDK installed by this
  command the first search location without otherwise reordering team-defined
  fallback paths.
- Write or preserve `sdk.rollForward` according to the version/channel mapping.
- Write `sdk.allowPrerelease` when the requested SDK requires prerelease
  resolution. Record in the manifest when dotnetup writes this property so a
  later stable-channel update can remove it safely. If the user already had
  `allowPrerelease` in `global.json`, preserve it unless the user explicitly
  asks dotnetup to manage that property.
- Preserve `$host$` if present.
- If `sdk.paths` already exists and `$host$` is missing, do not add it silently.
  Warn that the repository has no normal host fallback and preserve the existing
  hermetic behavior. `$host$` is added by default only when dotnetup creates a new
  `sdk.paths` array.
- Avoid duplicate path entries.
- Prefer the portable relative path `".dotnet"` for the repo-owned root when
  the command is run from the repository root.
- Normalize relative paths written to `global.json` to forward slashes.
- For v1 repository setup, only portable paths should be written to
  `global.json`. A resolved install path outside the selected repository should
  fail for `--update-global-json` unless a future explicit non-portable opt-in is
  designed.
- If the user provides an absolute `--install-path` that resolves inside the
  selected repository, dotnetup may use it as the physical install root but should
  still write a portable relative path to `global.json`.
- Accept comments and trailing commas if the parser supports them.
- If the implementation cannot preserve comments/formatting on write, the
  command should say that the file will be normalized before it writes.
- If `global.json` is malformed, fail and leave it unchanged.
- Write updates atomically where practical.

The implementation should not deserialize only the known `sdk` shape and
serialize a new file from that typed model; that would risk deleting unknown
repository settings.

The preferred implementation is a minimal, format-preserving edit that changes
only the affected `sdk` properties and paths array. If the available JSON tooling
cannot preserve comments and whitespace, the command should still preserve
unknown JSON properties but must warn before normalizing file formatting.

## Version and roll-forward behavior

`global.json` always contains a concrete SDK version. dotnetup's manifest records
the original channel when the user requested a channel.

| User input | Installed SDK | `global.json` version | `rollForward` | `allowPrerelease` | Manifest remembers |
| --- | --- | --- | --- | --- | --- |
| `10.0.100` | `10.0.100` | `10.0.100` | `disable` | omitted | `10.0.100` |
| `11.0.100-preview.4.1` | exact prerelease | exact prerelease | `disable` | `true` | exact prerelease |
| `10.0.1xx` | latest in feature band | resolved concrete version | `latestPatch` | omitted | `10.0.1xx` |
| `10.0` | latest in major/minor | resolved concrete version | `latestFeature` | omitted | `10.0` |
| `10` | latest in major | resolved concrete version | `latestMinor` | omitted | `10` |
| `latest` | latest stable SDK | resolved concrete version | `latestMajor` | omitted | `latest` |
| `lts`* | latest LTS SDK | resolved concrete version | `latestFeature` | omitted | `lts` |
| `preview`* | latest preview SDK | resolved concrete version | `latestMajor` | `true` | `preview` |

The `lts` and `preview` rows are approximations for host fallback behavior:
`global.json` cannot directly express "LTS channel" or "preview channel".
dotnetup preserves the exact requested channel in its manifest and uses that
manifest entry during `dotnetup sdk update`.
For LTS, `latestFeature` is a closer approximation than `latestPatch` because
`latestPatch` would not advance across feature bands. The real LTS channel still
lives in the dotnetup manifest; `rollForward` only describes host fallback for
the concrete version written to `global.json`.

For all channel rows, `rollForward` describes host fallback behavior. dotnetup
uses the stored manifest channel, not the `global.json` roll-forward value, to
decide how far `dotnetup sdk update` may advance the installed SDK.
Tools that read only the committed `global.json` will not see the original
dotnetup channel for `lts` or `preview`; they only see the host fallback
approximation written there.

For exact version pins, update should not advance the repository SDK. For channel
installs, update may install a newer matching SDK and update `global.json`
`sdk.version` to the new concrete version.

When the user supplies an explicit version or channel, the mapping table above
applies and dotnetup updates `sdk.rollForward` accordingly, even if the existing
`global.json` had a different value. Preservation of existing `rollForward`
applies only when dotnetup is deriving the install request from an existing
`global.json` and the user did not pass a version or channel.

The setup command should be idempotent. Re-running
`dotnetup sdk install --install-path .dotnet --update-global-json` when the
requested SDK is already installed, the manifest entry is current, and
`global.json` already contains the expected `sdk.paths` entry should be a fast
verify-and-skip operation. It should report that the repository SDK setup is
already up to date rather than redownloading or silently doing nothing.

## Manifest shape

Repository roots extend the existing `dotnetRoots` model with an explicit root
kind and repository metadata. The `path` remains the absolute path to the
physical dotnet root on this machine. The repository metadata explains why that
root exists and how to reconcile it with `global.json`.

Example:

```json
{
  "schemaVersion": "1",
  "dotnetRoots": [
    {
      "path": "C:\\src\\my-repo\\.dotnet",
      "rootKind": "repository",
      "architecture": "x64",
      "repositoryRoot": "C:\\src\\my-repo",
      "globalJsonPath": "C:\\src\\my-repo\\global.json",
      "installSpecs": [
        {
          "component": "sdk",
          "versionOrChannel": "10.0.1xx",
          "installSource": "explicit",
          "globalJsonPath": "C:\\src\\my-repo\\global.json",
          "managedSdkPath": ".dotnet",
          "managedAllowPrerelease": false
        }
      ],
      "installations": [
        {
          "component": "sdk",
          "version": "10.0.102",
          "state": "active",
          "subcomponents": [
            "sdk/10.0.102",
            "shared/Microsoft.AspNetCore.App/10.0.2",
            "shared/Microsoft.NETCore.App/10.0.2",
            "shared/Microsoft.WindowsDesktop.App/10.0.2"
          ]
        }
      ]
    }
  ]
}
```

`installSource` should use the existing manifest meanings:

| Value | Meaning for repository setup |
| --- | --- |
| `explicit` | The version or channel came from the `dotnetup sdk install ... --update-global-json` command line. |
| `global.json` | The version or channel was derived from an existing `global.json` because the user did not pass one on the command line. |

The manifest also needs enough ownership metadata to edit `global.json` safely:

- the `sdk.paths` entry dotnetup manages, using the same portable spelling that
  appears in `global.json`;
- whether dotnetup wrote `sdk.allowPrerelease`;
- pending vs active installation state so crash recovery can distinguish
  dotnetup-owned files from unknown conflicts.

If an existing `global.json` cannot be mapped unambiguously to a supported
dotnetup channel, the manifest should record the concrete `sdk.version` as an
exact pin and `dotnetup sdk update` should not auto-advance it.

If the repository is moved, the old absolute paths may no longer exist. Commands
that read the manifest should handle that gracefully:

- `list` should report missing repository roots as stale rather than failing or
  silently omitting them.
- `update` should skip missing repository roots by default and prune stale
  entries during manifest cleanup.
- `gc` should remove stale manifest entries for missing repositories, but it
  cannot delete files that are already gone.
- Running `dotnetup sdk install --install-path .dotnet --update-global-json` from the new location
  creates or refreshes the manifest entry for the new absolute paths.

## Update behavior

For repo-owned roots, `dotnetup sdk update` should:

1. Use the same discovery walk as `--update-global-json` to find the repository
   SDK configuration root for the current working directory.
2. Re-read the associated `global.json`.
3. Match that `global.json` path and configured install root to a repository
   install spec in the dotnetup manifest.
4. If no repository install spec exists for the current directory, do not update
   any repository roots. Continue normal dotnetup-managed root update behavior,
   if applicable, and print an informational message that repository SDK setup
   can be enabled with
   `dotnetup sdk install --install-path .dotnet --update-global-json`.
5. If the user removed the configured install root from `sdk.paths`, do not add
   it back silently. Report that the repo setup is no longer active and skip that
   spec unless the user runs setup again.
6. For exact pins, reinstall the exact version only if repair is needed.
7. For channel specs, re-resolve the channel, install the newer SDK into the
   configured install root, and update `global.json` `sdk.version`.
8. Update `sdk.allowPrerelease` to match the stored manifest channel. If a
   channel no longer requires prerelease SDKs, remove `allowPrerelease` only when
   dotnetup previously wrote it or the user confirms the edit.
9. Garbage collect only dotnetup-owned files that are no longer referenced.

For channel specs, `dotnetup sdk update` changes a repository file when it writes
the newer concrete `sdk.version` to `global.json`. Developers should commit that
change if the repository is meant to advance. CI jobs that run update should
either avoid dirty-worktree checks for that step or explicitly handle the
resulting `global.json` diff.

When run without an explicit repository option, `dotnetup sdk update` should not
walk every repository ever recorded in the manifest. Updating old checkouts would
cause surprising downloads and source-control modifications. A future explicit
option such as `--all-repositories` can update every known repository root.

List and info output should show repository roots separately from normal
dotnetup-managed roots so users understand why an SDK exists under a checkout.

Example:

```text
$ dotnetup list

Installations (managed by dotnetup):

  C:\Users\you\.dotnet

    Tracked channels:
      SDK latest                          (source: explicit)

    Installed versions:
      SDK 10.0.300                        (x64)

Repository SDK roots:

  C:\src\my-repo\.dotnet
    global.json: C:\src\my-repo\global.json

    Tracked channels:
      SDK 10.0.1xx                        (source: global.json)

    Installed versions:
      SDK 10.0.102                        (x64)
```

If `dotnetup list --json` or `dotnetup --info --json` includes repository roots,
the JSON shape should expose the same distinction as the human-readable output.
At minimum, repository entries should include:

```json
{
  "component": "sdk",
  "version": "10.0.102",
  "installRoot": "C:\\src\\my-repo\\.dotnet",
  "architecture": "x64",
  "rootKind": "repository",
  "repositoryRoot": "C:\\src\\my-repo",
  "globalJsonPath": "C:\\src\\my-repo\\global.json",
  "versionOrChannel": "10.0.1xx"
}
```

Normal dotnetup-managed roots should either omit these repository-only fields or
set `rootKind` to `dotnetupManaged`. That compatibility decision should be made
when the list/info JSON schema is updated.

Phase 3 must update the `dotnetup list` and `dotnetup --info` specs before this
feature is considered complete, because repository-root JSON fields are part of
the management surface proposed here.

For `dotnetup list --no-verify`, repository roots should behave like normal
install roots: read manifest state without checking whether the configured
install root or the associated `global.json` still exists on disk.
Verification-enabled list/info should detect missing repository roots and report
them as stale.

## Uninstall and garbage collection

Uninstall should remove dotnetup's install spec and dotnetup-owned SDK files when
they are no longer referenced.

Uninstall should not edit `global.json` by default.

If uninstall targets the repository install root currently referenced by
`global.json`, dotnetup should warn that the repository configuration remains in
place and may fall back to `$host$` or fail until setup is run again. In
non-interactive mode, removing an active repository SDK should require an
explicit force/yes option if dotnetup has such a convention; otherwise it should
fail with the warning rather than silently leaving the repository unpopulated.

Reasoning:

- `global.json` is repository configuration.
- Removing a path entry can break a repository that expects the configured
  install root to be populated by CI, scripts, or a future dotnetup setup run.
- A user who wants to undo the repository configuration can edit `global.json`
  explicitly.

Garbage collection for repository roots must be conservative:

- Do not sweep unknown files in the install root.
- Do not delete files that are not tracked as dotnetup-owned.
- Do not delete the install root unless it is empty after removing
  dotnetup-owned files.
- If manifest state is missing or corrupt, fail safe and leave repository files
  in place.
- If a repository root recorded in the manifest no longer exists, prune the
  stale manifest entry without treating it as an error.

The manifest should record enough information to support this:

- root kind: dotnetup-managed or repository;
- repository root path;
- install root path;
- associated `global.json` path;
- requested version or channel;
- resolved installed version;
- dotnetup-owned subcomponents.

## `.gitignore` behavior

If the repository install root is not ignored, dotnetup should warn by default:

```text
Warning: .dotnet/ is not ignored by this repository. Add .dotnet/ to .gitignore
or run again with --update-gitignore.
```

Add an explicit `--update-gitignore` option to let users request the edit.

Do not silently edit `.gitignore` by default. `global.json` is the primary
configuration file for the feature, but `.gitignore` is a broader repository
policy file. Editing it automatically in CI or scripted flows would be
surprising.

If `--update-gitignore` is used, dotnetup should:

- add the repository-relative install root, such as `.dotnet/`, to the nearest
  repository `.gitignore` if one exists;
- create `.gitignore` in the configuration root if none exists;
- avoid duplicate entries;
- preserve existing content.

The entry should be anchored relative to the `.gitignore` file being edited when
possible, for example `/.dotnet/` for a repository-root install root or
`/src/App/.dotnet/` for a subdirectory install root recorded in the root
`.gitignore`. This avoids accidentally ignoring unrelated `.dotnet` directories
elsewhere in the tree.

When checking whether the repository install root is ignored, dotnetup should
prefer the same semantics as Git, including parent `.gitignore` files,
`.git/info/exclude`, and global ignore configuration when available. Shelling out
to `git check-ignore` or using an equivalent ignore matcher is preferable to
naive string matching.

If Git ignore evaluation is unavailable or returns an error because the source
tree is not a Git repository, dotnetup should fall back to checking `.gitignore`
files in the configuration root. If no `.gitignore` is found, warn
unconditionally.

## Concurrency

The full repository setup operation should run under the same process-wide
installation-state lock used for normal install/update/uninstall operations
(`ScopedMutex` over `MutexNames.ModifyInstallationStates`).

The lock should cover:

- resolving the requested version/channel;
- downloading and extracting SDK payloads into the configured install root;
- editing `global.json`;
- editing `.gitignore` when `--update-gitignore` is requested;
- writing the dotnetup shared manifest.

This prevents two dotnetup processes from racing on the same repository root or
writing manifest state that does not match the final `global.json`. The
implementation should still use atomic file replacement for individual file
writes, but atomic writes alone are not enough because this feature updates
multiple pieces of state.

The setup operation should use a transaction-like order:

1. Resolve and validate the selected repository root, `global.json`, and install
   path.
2. Download and extract into a temporary staging directory under the install
   root.
3. Validate the staged SDK payload.
4. Persist a pending manifest entry that records the install root, target SDK
   version, staging location, and files or subcomponent paths that are about to be
   promoted.
5. Atomically promote the staged SDK payload into its final subcomponent paths.
6. Mark the pending manifest entry active so the promoted files are
   dotnetup-owned.
7. Update `global.json` last so the host never points at an incomplete install.
8. Edit `.gitignore` last or near-last; it is helpful repository hygiene, not a
   prerequisite for SDK resolution.

If dotnetup crashes before `global.json` is updated, a later setup run should use
the pending or active manifest entry to complete, repair, or roll back the
install rather than treating promoted files as unknown conflicts. If it crashes
after `global.json` is updated, the promoted payload and active manifest
ownership should already exist. `global.json` must not be updated before either a
valid active manifest entry exists or the implementation has equally explicit
recovery rules.

If a future implementation needs more concurrency, it can add a per-repository
lock layered under the global installation-state lock. The v1 design should
prefer correctness and predictable repository state over parallel install
throughput.

## Non-interactive behavior

This command must not prompt in non-interactive or CI mode.

| Situation | Interactive behavior | Non-interactive behavior |
| --- | --- | --- |
| No `global.json`, no version/channel, and cwd is inside a repository | Fail and ask for an explicit SDK version or channel | Same failure |
| No `global.json`, explicit version/channel, and cwd is inside a repository | Create `global.json` at the repository root | Same behavior |
| No `global.json` and cwd is not inside a repository | Fail with an actionable error | Same failure |
| `global.json` found above the current directory with no `.git` boundary | Fail unless the current directory contains the selected `global.json` | Same failure |
| Active host is older than .NET 10 | Warn after setup succeeds | Warn after setup succeeds; do not fail because the SDK was installed successfully |
| Resolved repository install root is not ignored | Warn and suggest `--update-gitignore` | Warn and suggest `--update-gitignore`; do not auto-edit |
| Existing `sdk.paths` lacks `$host$` | Warn and preserve the existing no-host fallback behavior | Same warning |
| Valid `global.json` can be read but formatting/comments cannot be preserved | Warn before writing normalized JSON | Warn to the diagnostic stream and proceed, preserving JSON properties but not formatting |
| Resolved install path is a symlink, junction, mount point, or other reparse point | Fail | Same failure |
| `global.json` is malformed | Fail without writing | Same failure |

Warnings should be written to the standard diagnostic/error stream used by
dotnetup so they are visible when command output is redirected. `--quiet`, if it
exists or is added later, should not suppress warnings that indicate the setup may
not be honored by later `dotnet` commands.

## CI usage

The CI story is the same as the clean-checkout story: CI should run dotnetup
before running SDK commands.

Example:

```bash
dotnetup sdk install --install-path .dotnet --update-global-json
dotnet build
dotnet test
```

Requirements:

- The CI image must provide dotnetup.
- The `dotnet` host used for `dotnet build` and `dotnet test` must be .NET 10 or
  later for `sdk.paths` to be honored.
- If the host is older than .NET 10, the setup command can still install the SDK
  and update `global.json`, but subsequent `dotnet` commands may ignore
  `.dotnet/`.

New setup does not make CI hermetic by default because dotnetup creates
`sdk.paths` with `"$host$"` fallback. Existing `sdk.paths` arrays that omit
`"$host$"` are preserved with a warning.

CI should run repository setup from the repository root, or pass an install path
that resolves to the intended root, so the relative `sdk.paths` entry matches the
committed `global.json`. CI should generally use setup/install, not update,
unless the workflow is prepared to handle a modified `global.json`.

## Exit codes

Until dotnetup has a broader exit-code convention, this feature should use the
same simple convention as other dotnetup commands: `0` for success and `1` for
expected command failures or unexpected errors. The important contract is which
situations are success, warnings, and failures:

| Result | Exit code |
| --- | --- |
| SDK installed or setup already up to date | `0` |
| SDK installed, but host is older than .NET 10 | `0` with warning |
| SDK installed, but the repository install root is not ignored | `0` with warning |
| No `global.json` and cwd is not inside a repository | `1` |
| Non-interactive root discovery would mutate a parent `global.json` without a `.git` boundary | `1` |
| Malformed `global.json` | `1` |
| Existing install path is a symlink/reparse point | `1` |
| Conflicting unknown files in the install root | `1` |
| Download, extraction, or hash verification failure | `1` |

## Testing

Implementation should include tests for the behavior that makes this feature
different from normal installs:

- root discovery, including nearest `global.json`, `.git` ceiling, nested
  `global.json`, no-`global.json`, and non-interactive parent-root failure;
- `global.json` merging, including preserving unknown top-level sections,
  preserving unrelated `sdk` properties, adding `paths`, avoiding duplicate
  entries, preserving `$host$`, and handling malformed JSON without writing;
- version/channel mapping to `sdk.version`, `rollForward`, `allowPrerelease`, and
  manifest `versionOrChannel`;
- repository-root manifest entries, including moved/deleted repositories and
  stale-entry list/update/gc behavior;
- existing install-root content, including unknown non-conflicting files and
  unknown files that conflict with target SDK subcomponent paths;
- idempotency when setup is run multiple times;
- `.gitignore` detection and `--update-gitignore`;
- symlink, junction, mount point, and reparse-point rejection for the resolved
  install path;
- canonical containment checks for `..` segments, case-insensitive paths on
  Windows, and non-existent install leaves under symlinked or junctioned
  ancestors;
- no-follow validation for `global.json` and `.gitignore` write targets,
  including symlinked files and ancestors that escape the repository root;
- injected failures between staging, promotion, manifest write, and
  `global.json` write to validate recovery behavior;
- non-interactive/CI warning and failure behavior;
- host-version warning behavior for .NET hosts older than 10;
- list/info output for repository roots, including JSON output if list/info JSON
  is extended to include root kind and `globalJsonPath`.

## Implementation phases

### Phase 1: Repository setup for exact SDK versions

Goal:
`dotnetup sdk install 10.0.100 --install-path .dotnet --update-global-json`
installs an exact SDK into `.dotnet/` and creates or merges `global.json`.

This phase should include root discovery, install-root safety checks,
`global.json` merge/write behavior, manifest root-kind tracking, idempotency,
`.gitignore` warning behavior, and host-version warnings.

### Phase 2: Existing `global.json` and channel support

Goal: `dotnetup sdk install --install-path .dotnet --update-global-json` and
`dotnetup sdk install 10.0.1xx --install-path .dotnet --update-global-json` work
with existing `global.json` files and channel inputs.

This phase adds channel-to-roll-forward mapping, manifest preservation of the
original channel, update behavior for channel specs, and prerelease
`allowPrerelease` handling.

### Phase 3: Repository-aware list, info, update, uninstall, and GC

Goal: repository roots participate coherently in dotnetup management commands.

This phase adds human-readable and JSON list/info output, current-directory
scoped repository update behavior, stale repository detection, conservative
uninstall/GC for dotnetup-owned files, and moved/deleted repository handling.
If JSON output changes, this phase should also update the `dotnetup-list.md` and
`dotnetup-info.md` schemas so the docs stay in sync.

### Phase 4: Optional `.gitignore` editing

Goal: `--update-gitignore` edits `.gitignore` safely.

This can ship with Phase 1 if it is small, but it is separable. The core feature
can warn about `.dotnet/` not being ignored before it supports editing
`.gitignore` automatically.

## Security and portability requirements

### Symlinks and reparse points

For v1, the resolved install path must be a normal directory. The recommended
path is `.dotnet`, but the rule applies to whatever path `--install-path`
selects.

If the resolved install path already exists and is a symlink, junction, mount
point, or other reparse point, dotnetup should fail unless a future explicit
opt-in is designed.

The validation must not be a lexical string check only. Before installing,
dotnetup should canonicalize the selected repository root, the `global.json`
directory, and every existing ancestor of the resolved install path. The install
path is valid only if the canonical install location stays inside the canonical
repository root using platform-appropriate comparison rules. If any traversed
ancestor is a symlink, junction, mount point, or other reparse point, fail rather
than following it.

The same no-follow rule applies to every file dotnetup mutates. `global.json` and
`.gitignore` must be normal files inside the canonical repository root; if either
write target is a symlink, junction, reparse point, mount-point escape, or has an
ancestor that escapes the canonical repository root, dotnetup should fail rather
than writing through it.

Reasoning:

- A repository can contain or create a path that points outside the checkout.
- Installing through that path could overwrite files outside the intended root.
- Garbage collection through that path could delete files dotnetup does not own.
- The v1 proposal intentionally rejects symlink/junction projection, so following
  existing reparse points would contradict the safety model.
- A non-existent leaf can still escape the repository if one of its existing
  parents is a link or mount point, so ancestor validation is required.

### Existing install root

If the install root exists as a normal directory:

- dotnetup may install into it;
- dotnetup must not delete unknown files;
- if files conflict with the SDK being installed, dotnetup should fail with a
  clear diagnostic rather than overwrite unrelated content.

A conflict means a file or directory already exists at a path dotnetup needs to
write for the target SDK subcomponent, and that existing path is not recorded in
the manifest as dotnetup-owned for this install root. Files already tracked as
dotnetup-owned for the same SDK version are not conflicts and should be skipped
for idempotency. Files covered by a pending manifest entry from an interrupted
setup are also not conflicts; dotnetup should complete, repair, or roll them
back under the process-wide lock. Files tracked by dotnetup for an older version
can be replaced during update according to the manifest. Unknown files elsewhere
under the install root are not conflicts and should be left in place.

### Environment

This setup flow must not:

- modify PATH;
- modify shell profile files;
- set user-level or machine-level `DOTNET_ROOT`;
- switch the user's dotnetup access mode;
- install system packages.

### Portability

- Write a portable relative path such as `".dotnet"`, not an absolute
  repository path.
- Include `"$host$"` by default for fallback.
- Use path spelling that works in committed `global.json` files across Windows,
  macOS, and Linux.

## Help text

Help text should state the side effects directly:

```text
--update-global-json
    Create or update global.json with the resolved SDK version. When
    --install-path is provided, also add that path to sdk.paths so a .NET 10+
    host resolves SDKs from that path first. This does not modify PATH, shell
    profiles, or DOTNET_ROOT.
```

The completion message should include:

- installed SDK version;
- install root;
- `global.json` path updated;
- whether `.gitignore` already ignores the repository install root;
- host prerequisite warning if applicable.

## Considered alternatives

### `dotnetup sdk install --local`

Rejected. `local` is ambiguous in dotnetup because it can mean user-local,
non-admin, not-on-PATH, archive-based, or repository-scoped. It also hides the
`global.json` side effect.

### `dotnetup sdk install --project-local` / `--repo-local`

Rejected for v1. These are clearer than `--local`, but they add new flags while
still requiring help text to explain install root, `global.json`, `.gitignore`,
and host prerequisites. Extending `--update-global-json` avoids another
repository-specific flag.

### `dotnetup sdk setup` / `dotnetup sdk bootstrap`

Rejected for v1. These verbs describe the scenario well, but they introduce a
new command surface and overlap with `dotnetup init`. If repository setup grows
beyond SDK installation, a setup/bootstrap verb can be reconsidered.

### `--install-path here`

Rejected for v1. This avoids `--local`, but it still adds a magic value to an
option that normally accepts filesystem paths. Extending `--update-global-json`
instead covers the scenario without a special sentinel and makes the
configuration side effect explicit.

### dotnetup-managed hive referenced from `global.json`

Rejected until the host has a portable placeholder for dotnetup-managed roots.
Absolute user-specific paths should not be written into committed
`global.json` files.

### dotnetup-managed hive projected through `.dotnet` symlink/junction

Rejected for v1 because it trades duplicate SDK payloads for a more complex and
riskier filesystem model. It can be revisited if deduplication becomes important
enough to justify link creation, validation, repair, and cleanup semantics.

## Decisions requested from maintainers

This spec recommends a concrete v1:

- API:
  `dotnetup sdk install [<version-or-channel>] --install-path .dotnet --update-global-json`
- install root: repository-owned `.dotnet/`
- `global.json`: concrete `sdk.version`, explicit roll-forward, and for new
  repository setup, `"paths": [ ".dotnet", "$host$" ]`
- `.gitignore`: warn by default, edit only with `--update-gitignore`
- environment: no PATH/profile/`DOTNET_ROOT` mutation
- safety: do not follow existing install-root symlinks or reparse points

The main maintainer decision is whether this narrow repo-owned v1 is worth
adding now. If the answer is no, the implementation should not proceed with
`--local`; it should wait until dotnetup has a portable managed-hive story or a
different repository bootstrap design.

Maintainers should also confirm that the .NET 10 host `sdk.paths` contract is
stable enough for committed repository files before approving implementation.

Specific decisions to confirm before implementation:

1. Is `--update-global-json` plus an explicit `--install-path` the accepted API
   shape for v1?
2. Is repo-owned `.dotnet/` the accepted install-root model for v1?
3. Is the .NET 10 `sdk.paths` host contract stable enough for committed
   repository `global.json` files?
4. Are the `lts` and `preview` roll-forward values acceptable as host fallback
   approximations, with dotnetup preserving the real channel in the manifest?
5. Should `--update-gitignore` be included in v1, or should v1 warn only?
