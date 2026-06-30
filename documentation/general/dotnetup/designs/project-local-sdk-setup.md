# Project-local SDK setup with dotnetup

## Recommendation

Add one explicit SDK setup path:

```bash
dotnetup sdk install [<version-or-channel>] --install-path here
```

The `here` value is a dotnetup sentinel that means:

1. Find the repository SDK configuration root.
2. Install the requested SDK into a repository-owned `.dotnet/` root at that
   location.
3. Create or merge `global.json` so a .NET 10+ host resolves SDKs from
   `.dotnet` before falling back to the normal host roots.
4. Track the install in dotnetup's shared manifest as a repository root so
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

This is the proposed v1. The previous `--local` direction should not be used.

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
- Do not configure runtimes independently for project-local runtime resolution.
- Do not modify PATH, shell profiles, user-level `DOTNET_ROOT`, or machine-level
  `DOTNET_ROOT`.
- Do not point committed `global.json` files at user-specific dotnetup hive paths.
- Do not use symlinks or junctions for the v1 repository projection model.
- Do not solve cross-repository SDK deduplication in v1.
- Do not make hermetic SDK resolution the default.

## Vocabulary

| Term | Meaning |
| --- | --- |
| dotnetup shared manifest | dotnetup's user-level state file that records install specs, dotnet roots, installed versions, and dotnetup-owned subcomponents. |
| install spec | The user's requested component and version/channel, plus where the request came from, such as an explicit command or a `global.json` file. |
| dotnetup hive | dotnetup's user-level managed install store. This is the normal place dotnetup installs SDKs and runtimes outside this proposed repo-owned setup mode. |
| repository root | In this document, the directory dotnetup chooses for repository SDK setup. It is normally the directory containing the nearest `global.json`; if no `global.json` exists and the user supplied a version/channel, it is the current directory. |
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

## User experience

### Existing `global.json`

```bash
git clone https://example/repo
cd repo
dotnetup sdk install --install-path here
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
dotnetup sdk install 10.0.100 --install-path here
```

If no `global.json` exists, dotnetup creates one in the current directory using
the requested version or channel. If the user omits both a version/channel and an
existing `global.json`, dotnetup should fail with a clear message:

```text
No global.json was found. Specify an SDK version or channel, for example:
  dotnetup sdk install 10.0.100 --install-path here
```

This avoids silently turning a repository setup command into "install whatever
dotnetup currently recommends."

### Running from a subdirectory

`here` does not mean "the current directory." It selects repository SDK setup
mode. The SDK root is resolved from the repository SDK configuration root.

For example, if `repo/global.json` exists:

```bash
cd repo/src/App
dotnetup sdk install --install-path here
```

dotnetup updates `repo/global.json` and installs the SDK into `repo/.dotnet/`,
because `sdk.paths` relative entries are resolved from the `global.json`
location.

When the SDK version is read from an existing `global.json` and no explicit
version/channel argument is provided, dotnetup treats that version as an exact
pin unless the existing file already contains a `rollForward` policy that maps
to a channel.

### Channel setup

```bash
dotnetup sdk install 10.0.1xx --install-path here
```

dotnetup resolves the channel to a concrete SDK version, installs that version,
writes the concrete version to `global.json`, and records the original channel in
the dotnetup manifest so `dotnetup sdk update` can advance it later.

## Why `--install-path here`

`--install-path here` is the recommended API because it is explicit enough for
the scenario while staying close to dotnetup's existing install model.

Pros:

- Reuses `sdk install` and the existing install-path concept.
- Avoids the overloaded word `local`.
- Keeps the scenario SDK-specific.
- Lets help text describe the side effects precisely:
  "`here` installs into this repository's `.dotnet/` directory and updates
  `global.json` `sdk.paths`."
- Matches maintainer feedback that `--install-path here` is the most promising
  option among the names considered.

The bare string `here` is a reserved sentinel for this command. Users who need a
literal directory named `here` can spell it as an explicit relative path:

```bash
dotnetup sdk install 10.0.100 --install-path ./here
```

On Windows, the equivalent explicit spelling is:

```pwsh
dotnetup sdk install 10.0.100 --install-path .\here
```

All other install-path values keep their existing meaning. The repository setup
behavior is only triggered by the sentinel `here`.

`here` is therefore a mode selector, not the directory name. The SDK always goes
into `.dotnet/` under the resolved repository SDK configuration root.

## Relationship to `--update-global-json`

Existing `dotnetup sdk install --update-global-json` updates the resolved
concrete SDK version in an existing `global.json`.

`--install-path here` intentionally implies a broader repository configuration
write:

- it may create `global.json`;
- it writes or updates `sdk.version`;
- it writes the appropriate `sdk.rollForward` and `sdk.allowPrerelease` values;
- it merges `.dotnet` and `$host$` into `sdk.paths`.

This is an exception to the normal "install does not edit `global.json` unless
asked" behavior because the purpose of `here` is repository SDK setup. Without
the `global.json` write, installing into `.dotnet/` would not reliably affect SDK
resolution.

If the user specifies both `--install-path here` and `--update-global-json`, the
result is the same as `--install-path here`; dotnetup may accept the combination
but should not require both flags.

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

Use a repository-owned `.dotnet/` root for v1.

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

For `--install-path here`, dotnetup should find the SDK configuration root using
this order:

1. If a `global.json` is found by walking upward from the current directory, use
   the directory containing that `global.json`.
2. If no `global.json` exists and the user supplied a version or channel, use the
   current directory and create `global.json` there.
3. If no `global.json` exists and the user did not supply a version or channel,
   fail with the message shown above.

Do not require a `.git` directory. CI source snapshots, source archives, and
non-Git worktrees should still be valid.

The upward walk has a ceiling:

- If dotnetup encounters a `.git` directory or file while walking upward, it
  checks that directory for `global.json` and then stops. It should not cross the
  Git repository boundary and mutate an unrelated parent `global.json`.
- If there is no `.git` boundary, dotnetup may walk to the filesystem root.
  In non-interactive mode, if the discovered `global.json` is not in the current
  directory, dotnetup should fail with a message telling the user to run the
  command from the directory containing `global.json` or provide an explicit SDK
  version/channel. This avoids surprising CI mutations when no repository
  boundary constrains the walk.

Before writing files, dotnetup should print the resolved repository SDK
configuration root and `global.json` path. This is especially important when no
`.git` boundary exists and the selected `global.json` may be in a parent
directory.

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
- Merge `.dotnet` into `sdk.paths` instead of replacing unrelated path entries.
- Write or preserve `sdk.rollForward` according to the version/channel mapping.
- Write or preserve `sdk.allowPrerelease` when the requested SDK requires
  prerelease resolution.
- Preserve `$host$` if present.
- Add `$host$` by default if it is missing.
- Avoid duplicate path entries.
- Prefer the portable relative path `".dotnet"` for the repo-owned root.
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
| `lts`* | latest LTS SDK | resolved concrete version | `latestPatch` | omitted | `lts` |
| `preview`* | latest preview SDK | resolved concrete version | `latestMajor` | `true` | `preview` |

The `lts` and `preview` rows are approximations for host fallback behavior:
`global.json` cannot directly express "LTS channel" or "preview channel".
dotnetup preserves the exact requested channel in its manifest and uses that
manifest entry during `dotnetup sdk update`.

For all channel rows, `rollForward` describes host fallback behavior. dotnetup
uses the stored manifest channel, not the `global.json` roll-forward value, to
decide how far `dotnetup sdk update` may advance the installed SDK.
Tools that read only the committed `global.json` will not see the original
dotnetup channel for `lts` or `preview`; they only see the host fallback
approximation written there.

For exact version pins, update should not advance the repository SDK. For channel
installs, update may install a newer matching SDK and update `global.json`
`sdk.version` to the new concrete version.

The setup command should be idempotent. Re-running
`dotnetup sdk install --install-path here` when the requested SDK is already
installed, the manifest entry is current, and `global.json` already contains the
expected `sdk.paths` entry should be a fast verify-and-skip operation. It should
report that the repository SDK setup is already up to date rather than
redownloading or silently doing nothing.

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
          "installSource": "global.json",
          "globalJsonPath": "C:\\src\\my-repo\\global.json"
        }
      ],
      "installations": [
        {
          "component": "sdk",
          "version": "10.0.102",
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

If the repository is moved, the old absolute paths may no longer exist. Commands
that read the manifest should handle that gracefully:

- `list` should report missing repository roots as stale rather than failing or
  silently omitting them.
- `update` should skip missing repository roots by default and prune stale
  entries during manifest cleanup.
- `gc` should remove stale manifest entries for missing repositories, but it
  cannot delete files that are already gone.
- Running `dotnetup sdk install --install-path here` from the new location
  creates or refreshes the manifest entry for the new absolute paths.

## Update behavior

For repo-owned roots, `dotnetup sdk update` should:

1. Use the same discovery walk as `--install-path here` to find the repository
   SDK configuration root for the current working directory.
2. Re-read the associated `global.json`.
3. Match that `global.json` path and `.dotnet` root to a repository install spec
   in the dotnetup manifest.
4. If no repository install spec exists for the current directory, do not update
   any repository roots. Continue normal dotnetup-managed root update behavior,
   if applicable, and print an informational message that repository SDK setup
   can be enabled with `dotnetup sdk install --install-path here`.
5. If the user removed `.dotnet` from `sdk.paths`, do not add it back silently.
   Report that the repo setup is no longer active and skip that spec unless the
   user runs setup again.
6. For exact pins, reinstall the exact version only if repair is needed.
7. For channel specs, re-resolve the channel, install the newer SDK into
   `.dotnet/`, and update `global.json` `sdk.version`.
8. Update `sdk.allowPrerelease` to match the stored manifest channel. If a
   channel no longer requires prerelease SDKs, remove `allowPrerelease` only when
   dotnetup previously wrote it or the user confirms the edit.
9. Garbage collect only dotnetup-owned files that are no longer referenced.

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

## Uninstall and garbage collection

Uninstall should remove dotnetup's install spec and dotnetup-owned SDK files when
they are no longer referenced.

Uninstall should not edit `global.json` by default.

Reasoning:

- `global.json` is repository configuration.
- Removing a path entry can break a repository that expects `.dotnet/` to be
  populated by CI, scripts, or a future dotnetup setup run.
- A user who wants to undo the repository configuration can edit `global.json`
  explicitly.

Garbage collection for repository roots must be conservative:

- Do not sweep unknown files in `.dotnet/`.
- Do not delete files that are not tracked as dotnetup-owned.
- Do not delete `.dotnet/` unless it is empty after removing dotnetup-owned files.
- If manifest state is missing or corrupt, fail safe and leave repository files
  in place.
- If a repository root recorded in the manifest no longer exists, prune the
  stale manifest entry without treating it as an error.

The manifest should record enough information to support this:

- root kind: dotnetup-managed or repository;
- repository root path;
- `.dotnet` root path;
- associated `global.json` path;
- requested version or channel;
- resolved installed version;
- dotnetup-owned subcomponents.

## `.gitignore` behavior

If `.dotnet/` is not ignored, dotnetup should warn by default:

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

- add `.dotnet/` to the nearest repository `.gitignore` if one exists;
- create `.gitignore` in the configuration root if none exists;
- avoid duplicate entries;
- preserve existing content.

When checking whether `.dotnet/` is ignored, dotnetup should prefer the same
semantics as Git, including parent `.gitignore` files, `.git/info/exclude`, and
global ignore configuration when available. Shelling out to `git check-ignore`
or using an equivalent ignore matcher is preferable to naive string matching.

If Git ignore evaluation is unavailable or returns an error because the source
tree is not a Git repository, dotnetup should fall back to checking `.gitignore`
files in the configuration root. If no `.gitignore` is found, warn
unconditionally.

## CI usage

The CI story is the same as the clean-checkout story: CI should run dotnetup
before running SDK commands.

Example:

```bash
dotnetup sdk install --install-path here
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

This feature does not make CI hermetic by default because `"$host$"` remains in
`sdk.paths`. Teams that require hermetic SDK resolution should wait for an
explicit hermetic option rather than relying on v1 defaults.

## Security and portability requirements

### Symlinks and reparse points

For v1, `.dotnet` must be a normal directory.

If `.dotnet` already exists and is a symlink, junction, mount point, or other
reparse point, dotnetup should fail unless a future explicit opt-in is designed.

Reasoning:

- A repository can contain or create a path that points outside the checkout.
- Installing through that path could overwrite files outside the intended root.
- Garbage collection through that path could delete files dotnetup does not own.
- The v1 proposal intentionally rejects symlink/junction projection, so following
  existing reparse points would contradict the safety model.

### Existing `.dotnet`

If `.dotnet/` exists as a normal directory:

- dotnetup may install into it;
- dotnetup must not delete unknown files;
- if files conflict with the SDK being installed, dotnetup should fail with a
  clear diagnostic rather than overwrite unrelated content.

### Environment

This setup flow must not:

- modify PATH;
- modify shell profile files;
- set user-level or machine-level `DOTNET_ROOT`;
- switch the user's dotnetup access mode;
- install system packages.

### Portability

- Write `".dotnet"`, not an absolute repository path.
- Include `"$host$"` by default for fallback.
- Use path spelling that works in committed `global.json` files across Windows,
  macOS, and Linux.

## Help text

Help text should state the side effects directly:

```text
--install-path here
    Install the SDK into this repository's .dotnet directory and update
    global.json sdk.paths so a .NET 10+ host resolves SDKs from .dotnet first.
    This does not modify PATH, shell profiles, or DOTNET_ROOT.
```

The completion message should include:

- installed SDK version;
- install root;
- `global.json` path updated;
- whether `.gitignore` already ignores `.dotnet/`;
- host prerequisite warning if applicable.

## Considered alternatives

### `dotnetup sdk install --local`

Rejected. `local` is ambiguous in dotnetup because it can mean user-local,
non-admin, not-on-PATH, archive-based, or repository-scoped. It also hides the
`global.json` side effect.

### `dotnetup sdk install --project-local` / `--repo-local`

Rejected for v1. These are clearer than `--local`, but they add new flags while
still requiring help text to explain install root, `global.json`, `.gitignore`,
and host prerequisites. `--install-path here` reuses an existing concept and is
closer to maintainer feedback.

### `dotnetup sdk setup` / `dotnetup sdk bootstrap`

Rejected for v1. These verbs describe the scenario well, but they introduce a
new command surface and overlap with `dotnetup init`. If repository setup grows
beyond SDK installation, a setup/bootstrap verb can be reconsidered.

### `--install-path .dotnet --update-global-json`

Rejected for v1 as the primary UX. It is explicit, but too verbose for the main
scenario and makes it easy to pick a non-portable path. `here` gives dotnetup a
safe convention instead of accepting arbitrary paths as repository configuration.

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

- API: `dotnetup sdk install [<version-or-channel>] --install-path here`
- install root: repository-owned `.dotnet/`
- `global.json`: concrete `sdk.version`, explicit roll-forward, and
  `"paths": [ ".dotnet", "$host$" ]`
- `.gitignore`: warn by default, edit only with `--update-gitignore`
- environment: no PATH/profile/`DOTNET_ROOT` mutation
- safety: do not follow existing `.dotnet` symlinks or reparse points

The main maintainer decision is whether this narrow repo-owned v1 is worth
adding now. If the answer is no, the implementation should not proceed with
`--local`; it should wait until dotnetup has a portable managed-hive story or a
different repository bootstrap design.

Maintainers should also confirm that the .NET 10 host `sdk.paths` contract is
stable enough for committed repository files before approving implementation.
