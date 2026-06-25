# Project-local SDK setup with dotnetup

## Motivation

Some repositories need a clean, repeatable way to bootstrap the exact .NET SDK
that the repository expects. The common scenario is:

1. A developer clones a repository.
2. The repository has, or wants to create, a `global.json` that pins the SDK.
3. The developer runs a single dotnetup command.
4. The required SDK is installed and future `dotnet` invocations from that
   repository use it without changing the user's PATH, shell profile, or
   machine-wide `DOTNET_ROOT`.

This is especially useful for team bootstrap, CI images with minimal preinstalled
SDKs, contributor onboarding, and repos that need to isolate a pinned SDK from
whatever SDKs are already present on the machine.

.NET 10 adds `global.json` `sdk.paths`, which lets a repository participate in
SDK resolution by adding search roots such as:

```jsonc
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "disable",
    "paths": [ ".dotnet", "$host$" ]
  }
}
```

The `paths` setting makes project-local SDK setup possible without mutating the
user's global environment. dotnetup can install the SDK and update `global.json`
so the host finds that SDK when commands are launched inside the repository.

This document is a discussion spec. It intentionally describes multiple product
and API options rather than committing to one implementation.

## Goals

- Provide a command that installs the SDK needed by the current repository.
- Make the final state clear: dotnetup updates or creates `global.json` so SDK
  resolution uses the configured install root.
- Preserve existing `global.json` content, including unrelated top-level
  sections.
- Avoid changing PATH, shell profiles, user-level `DOTNET_ROOT`, or system-level
  installs for this setup flow.
- Support both exact SDK pins and channel-based setup in a way that maps cleanly
  to `global.json` roll-forward behavior.
- Keep update, uninstall, list, and garbage-collection behavior explainable.
- Decide whether this scenario should use a repository-owned SDK root or a
  dotnetup-managed SDK root referenced from `global.json`.

## Non-goals

- Installing runtimes independently for project-local runtime resolution. This
  document is scoped to SDK setup.
- Replacing the existing dotnetup onboarding modes: Isolation, Terminal Profile,
  and Replacement.
- Making project-local SDKs available outside the repository.
- Modifying Visual Studio, VS Code, shell profile, PATH, or machine-wide
  `DOTNET_ROOT`.
- Defining a complete implementation plan for every option. The first decision
  should be the product/API direction.

## Host prerequisites

The `global.json` `sdk.paths` feature requires a .NET 10 or later host. A
project-local SDK setup command can install a .NET 10+ SDK, but the first command
that launches dotnetup still runs under the host that was found before the
project-local SDK exists.

Implications:

- The machine must have a .NET 10+ host available before `global.json`
  `sdk.paths` can affect normal `dotnet` resolution.
- If an older host runs `dotnet`, it will ignore `sdk.paths`. The setup command
  should detect or document this clearly.
- dotnetup can still install the SDK archive into the chosen root, but the
  repository will not get the intended SDK resolution until a .NET 10+ host is
  used.
- The generated or updated `global.json` should include `$host$` unless the
  chosen behavior deliberately creates a hermetic repo that refuses fallback to
  host-installed SDKs. `$host$` lets SDK resolution continue to consider the
  normal host roots after checking the project-local path.

## Current dotnetup model

dotnetup is currently oriented around managing SDK and runtime installations in
dotnetup-controlled roots. Its install state is tracked through install specs in
the shared dotnetup manifest.

Relevant existing concepts:

- A user can install SDKs with `dotnetup sdk install <version_or_channel>`.
- dotnetup can derive an install spec from `global.json`.
- Install specs include the component, requested version or channel, source, and
  for `global.json`-derived specs, the associated `global.json` path.
- Updates re-resolve active install specs, install newer matching versions when
  available, and garbage collect installations that are no longer referenced.
- Uninstall removes an install spec and then garbage collects. The files may
  remain if another spec still references the same installed version.
- dotnetup's normal setup modes are about how users access dotnetup-managed
  installs: through `dotnetup dotnet`, shell profile/PATH changes, or system
  replacement on Windows.

Project-local setup is related to, but not identical to, those flows. The key
question is whether dotnetup should create and manage a repository-owned
`.dotnet` root, or whether it should continue to own installs in a dotnetup hive
and only write `global.json` so the repository points to that hive.

## Desired user outcomes

After a successful setup, a repository should be in one of these states,
depending on the install-root model chosen.

### Repository-owned root outcome

```text
repo/
  global.json
  .dotnet/
    dotnet
    sdk/
      10.0.100/
```

`global.json` contains:

```jsonc
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "disable",
    "paths": [ ".dotnet", "$host$" ]
  }
}
```

The repository owns `.dotnet/`, and dotnetup records enough state to update,
uninstall, or garbage collect its own contribution without deleting unknown
files.

### dotnetup-managed root outcome

```text
repo/
  global.json

<dotnetup root>/
  sdk/
    10.0.100/
```

`global.json` contains a path to the dotnetup-managed SDK root:

```jsonc
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "disable",
    "paths": [ "<portable-or-user-specific-dotnetup-root>", "$host$" ]
  }
}
```

The SDK remains inside the dotnetup hive, so existing manifest, update,
uninstall, and garbage-collection rules are more natural. The tradeoff is that
`global.json` may contain a user-specific absolute path unless we define a
portable placeholder or relative indirection.

## Proposed behavior options

### Option A: `dotnetup sdk install --local`

```bash
dotnetup sdk install --local
dotnetup sdk install 10.0.100 --local
dotnetup sdk install 10.0.1xx --local
```

Behavior:

- Resolve the requested SDK version or channel using existing dotnetup rules.
- Install the SDK into the chosen project-local root model.
- Create or update `global.json` in the repository root.
- If a repo-owned root is chosen, optionally add `.dotnet/` to `.gitignore`.
- Do not mutate PATH, profile files, or user-level `DOTNET_ROOT`.

Pros:

- Short and discoverable from the existing install command.
- Matches the scenario name used by many developers: "install locally".
- Easy to combine with existing `sdk install` version/channel arguments.

Cons:

- `local` is overloaded. dotnetup already distinguishes user-level vs
  system-level installs, archive vs system installers, PATH-visible vs
  not-on-PATH installs, and per-user hives. Users may interpret `--local` as
  "install to my user profile" or "do not use admin" instead of "configure this
  repository's `global.json`".
- The flag name does not say that `global.json` will be modified.
- The root `dotnetup install --local` form could be even more ambiguous because
  it hides the SDK-specific nature of the scenario.

### Option B: `dotnetup sdk install --project-local`

```bash
dotnetup sdk install --project-local
dotnetup sdk install 10.0.100 --project-local
```

Pros:

- More explicit than `--local`.
- Communicates that the effect is scoped to the current project/repository.
- Leaves room for other meanings of "local" in dotnetup.

Cons:

- Longer flag.
- Still does not mention `global.json`.
- "Project" can be ambiguous in .NET because it may mean a `.csproj`, not a
  repository root.

### Option C: `dotnetup sdk install --repo-local`

```bash
dotnetup sdk install --repo-local
```

Pros:

- Strongly communicates repository ownership and checkout bootstrap.
- Avoids confusion with `.csproj` project files.

Cons:

- dotnetup would need to define how it finds the repository root. It may need to
  search for `.git`, `global.json`, or a user-provided root.
- Some source trees are not Git repositories, and CI may use source snapshots.
- Still does not mention `global.json`.

### Option D: `dotnetup sdk setup`

```bash
dotnetup sdk setup
dotnetup sdk setup 10.0.100
dotnetup sdk setup 10.0.1xx
```

Behavior:

- Treat `setup` as the repository bootstrap verb.
- Resolve and install the SDK.
- Update `global.json` and any related repo-local files.

Pros:

- Avoids overloading `install` with "also edit repository configuration".
- "Setup" naturally implies configuration work beyond copying SDK files.
- Leaves `sdk install` focused on dotnetup-managed install specs.

Cons:

- Introduces a new verb that overlaps with `dotnetup init`.
- Users may not know whether to run `sdk install` or `sdk setup`.
- Needs careful help text to explain that this is SDK resolution setup, not
  general dotnetup onboarding.

### Option E: `dotnetup sdk bootstrap`

```bash
dotnetup sdk bootstrap
dotnetup sdk bootstrap 10.0.100
```

Pros:

- Describes the clean-checkout/team onboarding scenario well.
- Less likely to be confused with a normal install.
- Signals repository mutation more clearly than `install --local`.

Cons:

- "Bootstrap" may be less familiar to some users.
- Could imply it should install dotnetup itself or all repository dependencies.
- Adds a new verb.

### Option F: `--install-path .dotnet --update-global-json`

```bash
dotnetup sdk install 10.0.100 --install-path .dotnet --update-global-json
```

Pros:

- Uses the existing `--install-path` concept.
- Makes both side effects explicit: install location and `global.json` update.
- Avoids a new special term such as `local`.
- Provides a composable primitive for advanced users.

Cons:

- Verbose for the primary bootstrap scenario.
- Easy to specify unsafe or non-portable paths without guardrails.
- Does not by itself define `.gitignore`, symlink/reparse-point, manifest, or
  garbage-collection behavior for repo-owned roots.
- Could imply that any install path is equally supported for updates/uninstalls,
  which may not be true.

### Option G: `--install-path here`

```bash
dotnetup sdk install 10.0.100 --install-path here
```

Behavior:

- Treat `here` as a special value meaning "use the repository's SDK setup
  convention".
- It could map to `.dotnet` plus `global.json` update, or to a dotnetup-managed
  root referenced from `global.json`.

Pros:

- Builds on an existing option that maintainers have already identified.
- Shorter than specifying `.dotnet --update-global-json`.
- Avoids the overloaded `--local` name.

Cons:

- Special string values can be surprising in a path option.
- `here` must be reserved or escaped if a user actually has a directory named
  `here`.
- The phrase still does not say `global.json` will be modified.
- The current directory is not always the repository root.

### Option H: dotnetup-managed hive plus `global.json` paths

```bash
dotnetup sdk setup 10.0.100
```

Behavior:

- Install into the normal dotnetup-managed SDK root.
- Update `global.json` `sdk.paths` to include a dotnetup-managed root.
- Track the install spec as coming from that repository's `global.json`.

Pros:

- Preserves dotnetup's product direction as the manager of its own install hive.
- Existing update, uninstall, list, and garbage-collection behavior maps more
  naturally.
- Avoids leaving large SDK payloads in each repository checkout.
- Reduces the risk that `git clean`, manual deletes, or repo cleanup tools break
  dotnetup's manifest state.

Cons:

- `global.json` may need an absolute, user-specific path, which is bad for
  committed repository files.
- A portable placeholder for the dotnetup hive would require support from the
  host or a convention that expands before writing `global.json`.
- The repository is not self-contained; another developer will still need to run
  dotnetup setup to populate their own hive and rewrite the path if absolute.
- It may be less obvious where the SDK was installed.

## Install-root alternatives

### Alternative 1: repository-owned `.dotnet/`

dotnetup installs archives under a `.dotnet/` directory rooted at the repository
or at the directory containing the relevant `global.json`.

Update/uninstall implications:

- The dotnetup manifest must distinguish repo-owned roots from the normal
  dotnetup hive.
- Install specs should record the `global.json` path and the repo-owned root
  path.
- `dotnetup sdk update` must be able to re-resolve channel specs and install a
  new SDK into `.dotnet/`.
- `dotnetup sdk uninstall` should remove only the install spec. It should delete
  SDK components only when no remaining spec references them.
- Garbage collection must not delete unknown files in `.dotnet/`. A repository
  may have files created by other tools, users, or previous experiments.
- If the repo deletes or edits `global.json`, dotnetup needs a policy for stale
  specs. Existing global.json-derived spec cleanup rules may apply, but the
  product should decide whether a missing `global.json` also allows removal of
  the repo-owned SDK payload.

Tracking implications:

- The manifest may need a root kind such as `dotnetupManaged` vs `repository`.
- List output should make clear that the root is repository-owned and associated
  with a specific `global.json`.
- A repo-owned root should not be used as a general shared install root for other
  repositories unless they explicitly reference it.

Pros:

- `global.json` can use a portable relative path: `".dotnet"`.
- The repository's expected SDK setup is easy to inspect.
- Works well for clean checkouts and CI caches keyed by repo.

Cons:

- Duplicates SDK payloads across repositories.
- Creates large local directories that users may accidentally commit unless
  ignored.
- Introduces a non-dotnetup-owned install root into dotnetup's tracking model.
- Requires careful handling of symlinks/reparse points and unknown files.

### Alternative 2: dotnetup-managed root referenced by `global.json`

dotnetup keeps SDKs in the normal dotnetup root and writes `global.json` paths
that point to that root.

Update/uninstall implications:

- Existing manifest tracking and garbage collection remain mostly unchanged.
- A `global.json` install spec can reference the normal dotnetup root.
- `dotnetup sdk update` can update the managed install and preserve the
  `global.json` relationship.
- Uninstall removes the install spec and allows normal garbage collection.

Tracking implications:

- The repository relationship is the install spec source, not a new install root.
- List/info output should show which `global.json` files reference the managed
  root.

Pros:

- Aligns with dotnetup's current direction as an install manager.
- Avoids duplicate SDK payloads in every checkout.
- Reduces file-system safety issues inside repositories.

Cons:

- The path in committed `global.json` may not be portable.
- If the path is user-specific, committing it is undesirable.
- Requires a story for how another user runs setup and gets their own path.
- Does not produce a fully repo-contained SDK root.

### Alternative 3: hybrid

Provide both models behind explicit APIs:

- A repo-owned mode for teams that want `.dotnet/` in the checkout.
- A dotnetup-managed mode for teams that want central management and
  deduplication.

Pros:

- Supports both product directions.
- Lets early adopters choose the model that matches their repository policy.

Cons:

- More API surface.
- More help text and docs.
- More complex update/uninstall/list behavior.
- Maintainers still need to choose a default.

## Version, channel, and prerelease behavior

The command should accept the same SDK version or channel inputs as
`dotnetup sdk install`.

### Exact version pins

```bash
dotnetup sdk setup 10.0.100
```

Expected `global.json`:

```jsonc
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "disable",
    "paths": [ ".dotnet", "$host$" ]
  }
}
```

Behavior:

- Install exactly `10.0.100`.
- Write `rollForward: "disable"` unless the existing `global.json` already has a
  deliberate roll-forward policy that the command is told to preserve.
- Updates should not move this install spec to a newer SDK unless the user
  changes the requested version or opts into a channel.

### Feature band channels

```bash
dotnetup sdk setup 10.0.1xx
```

Expected `global.json`:

```jsonc
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestPatch",
    "paths": [ ".dotnet", "$host$" ]
  }
}
```

Behavior:

- Resolve the channel to the latest available SDK in the feature band.
- Write the resolved SDK version because `global.json` requires a concrete
  `sdk.version`.
- Use a roll-forward policy that allows newer SDKs in the feature band while not
  crossing to other feature bands. `latestPatch` matches dotnetup's documented
  mapping for `10.0.1xx` channels.
- Track the install spec as `10.0.1xx` so `dotnetup sdk update` can re-resolve
  it later.

### Major/minor channels

```bash
dotnetup sdk setup 10.0
```

Expected `global.json` should use a concrete resolved version and a roll-forward
policy that matches the user's intent. Candidate policies:

- `latestFeature` if `10.0` should stay within the same major/minor but move
  across feature bands.
- `latestMinor` if the product wants to allow movement to later minor releases.

This requires a maintainer decision because dotnetup channel semantics and
`global.json` roll-forward semantics are not identical.

### `latest`, `lts`, and `preview`

```bash
dotnetup sdk setup latest
dotnetup sdk setup lts
dotnetup sdk setup preview
```

Behavior:

- Resolve the channel to a concrete version and write that version to
  `global.json`.
- Track the original channel in dotnetup's manifest so updates can continue to
  follow the channel.
- Choose a roll-forward value that reflects the channel. For `preview`, set or
  preserve `allowPrerelease: true` so the host can select prerelease SDKs.

Open issue:

- `global.json` does not directly express "latest", "lts", or "preview" as
  channels. The manifest can, but the committed file contains the currently
  resolved version. The design should state clearly that `dotnetup sdk update`
  is responsible for advancing the version in `global.json` for channel-based
  setups.

### Specific prerelease versions

```bash
dotnetup sdk setup 11.0.100-preview.4.26230.115
```

Expected `global.json`:

```jsonc
{
  "sdk": {
    "version": "11.0.100-preview.4.26230.115",
    "rollForward": "disable",
    "allowPrerelease": true,
    "paths": [ ".dotnet", "$host$" ]
  }
}
```

Behavior:

- Exact prerelease versions should be pinned with `rollForward: "disable"`.
- `allowPrerelease` should be set to `true` unless the host behavior already
  makes it unnecessary for exact prerelease pins. Setting it is clearer.

## `global.json` merge and preservation requirements

The setup command should never blindly overwrite `global.json`.

Requirements:

- Preserve unknown top-level sections such as `tools`, `msbuild-sdks`, and any
  future sections.
- Preserve unknown properties inside `sdk` unless the command is explicitly
  updating that property.
- Preserve formatting as much as practical. If the existing parser supports
  comments and trailing commas, the command should accept them rather than
  rejecting otherwise valid repository files.
- If comments and trailing commas are accepted on read but not preserved on
  write, the command should document or warn that `global.json` will be
  normalized.
- If `sdk.version` exists, do not replace it without making the intended change
  clear to the user.
- If `sdk.paths` exists, merge the selected path into it rather than discarding
  unrelated paths.
- Preserve `$host$` if present. Add `$host$` by default for this setup flow
  unless the user explicitly requests a hermetic setup.
- Avoid duplicate path entries. Normalize path comparison in a
  platform-appropriate way.
- Use portable path values when possible. Prefer `".dotnet"` over an absolute
  repository path for repo-owned roots.

For a repo-owned root, the default `sdk.paths` should be:

```jsonc
"paths": [ ".dotnet", "$host$" ]
```

For a dotnetup-managed root, the default depends on the product decision:

- If a portable placeholder exists, use that placeholder plus `$host$`.
- If only an absolute path is possible, decide whether this feature should write
  a user-specific path into `global.json` at all.

## `.gitignore` behavior

If the selected model creates a repository-owned `.dotnet/` root, dotnetup should
help prevent accidental commits of SDK payloads.

Options:

1. Automatically add `.dotnet/` to the nearest `.gitignore`.
2. Prompt in interactive mode and require an explicit flag in non-interactive
   mode.
3. Print a warning but leave `.gitignore` untouched.
4. Do nothing and document that teams should ignore `.dotnet/`.

Recommendation for discussion:

- If the command creates `.dotnet/`, it should at least warn when `.dotnet/` is
  not ignored.
- Automatically editing `.gitignore` is convenient, but it is another repository
  mutation beyond `global.json`. That may be surprising in CI or scripted flows.
- A non-interactive default could be: update `global.json`, install SDK, warn if
  `.dotnet/` is not ignored, and offer `--update-gitignore` for automatic edits.

## Security and portability considerations

### Symlinks and reparse points

For a repository-owned `.dotnet/` root, dotnetup should not follow an existing
`.dotnet` symlink, junction, mount point, or other reparse point unless the user
explicitly opts in.

Rationale:

- A malicious or compromised repository could point `.dotnet` outside the
  checkout.
- Installing archives through such a path could overwrite files the user did not
  intend dotnetup to manage.
- Garbage collection through such a path is especially dangerous.

Safer default:

- If `.dotnet` exists and is a symlink/reparse point, fail with a clear error.
- If `.dotnet` exists and is a normal directory, only manage files that dotnetup
  installed and tracked.

### `global.json` safety

- Do not overwrite `global.json` blindly.
- If `global.json` is malformed, fail with a clear diagnostic and leave the file
  unchanged.
- Write updates atomically where possible.
- Consider creating a backup only if that is consistent with dotnetup's existing
  file-editing patterns.

### Environment mutation

This setup flow should not:

- Modify PATH.
- Modify shell profile files.
- Set user-level or machine-level `DOTNET_ROOT`.
- Switch the user's dotnetup mode.

The only expected persistent mutations are the SDK install root, dotnetup
tracking state, `global.json`, and possibly `.gitignore` if that option is
chosen.

### Portability

- Prefer relative paths in committed files.
- Avoid user-profile absolute paths in `global.json` unless the managed-root
  model explicitly accepts that tradeoff.
- Use path separators that are valid in `global.json` across platforms.
- Consider how a `global.json` committed on Windows behaves on Linux and macOS.

## Update behavior

Exact-version setup:

- `dotnetup sdk update` should not advance an exact version pinned with
  `rollForward: "disable"`.
- If the SDK payload is missing, update or repair could reinstall the exact
  version.

Channel setup:

- The manifest should remember the original channel (`10.0.1xx`, `latest`,
  `preview`, etc.).
- `dotnetup sdk update` should resolve the channel again.
- If a newer matching SDK exists, install it and update `global.json`
  `sdk.version` to the new concrete version.
- The roll-forward policy should remain consistent with the channel.

Global.json changes outside dotnetup:

- If the user edits `global.json` to a different version or paths list,
  dotnetup should reconcile the manifest spec on the next update/list/GC, using
  the same policy as other global.json-derived specs.
- If the user removes the dotnetup path from `sdk.paths`, dotnetup should not add
  it back silently unless the user runs the setup command again.

## Uninstall and garbage collection

Repository-owned root:

- Uninstall should remove the install spec and optionally remove dotnetup-owned
  SDK files that are no longer referenced.
- It should not delete `.dotnet/` wholesale unless it is empty and dotnetup can
  prove it only contains dotnetup-owned files.
- It should not remove or rewrite unrelated `global.json` content.
- It should decide whether to remove its path from `sdk.paths` as part of
  uninstall. Removing the path is intuitive, but could break a repo that still
  expects `.dotnet` to be populated by another mechanism.

dotnetup-managed root:

- Existing uninstall and garbage-collection behavior applies more directly.
- The command still needs a policy for whether uninstall removes the managed root
  path from `global.json`.

Open issue:

- Should uninstall edit `global.json`, or should repository configuration only
  change when the setup command runs? Editing is convenient but can be
  surprising.

## Help text requirements

Regardless of API shape, help text should make the side effects explicit.

Example for a setup-style command:

```text
Install the SDK required by this repository and update global.json so the
.NET 10+ host resolves SDKs from the configured SDK path. This command does not
change PATH, shell profiles, or DOTNET_ROOT.
```

Example warning for an overloaded `--local` flag:

```text
--local installs for this repository and updates global.json. It does not mean
user-level install, PATH setup, or system replacement.
```

## Open questions

1. Which API should be used for the primary scenario?
   - `dotnetup sdk install --local`
   - `dotnetup sdk install --project-local`
   - `dotnetup sdk install --repo-local`
   - `dotnetup sdk setup`
   - `dotnetup sdk bootstrap`
   - `dotnetup sdk install --install-path .dotnet --update-global-json`
   - `dotnetup sdk install --install-path here`

2. Should dotnetup support root-level `dotnetup install --local`, or should this
   remain SDK-specific to avoid implying runtime/local environment setup?

3. Which install-root model should be the default?
   - Repository-owned `.dotnet/`
   - dotnetup-managed hive referenced from `global.json`
   - Both, with an explicit flag

4. If the dotnetup-managed hive model is chosen, how can `global.json` remain
   portable across machines?

5. If the repository-owned `.dotnet/` model is chosen, what should dotnetup track
   in its manifest so updates, uninstall, list, and garbage collection remain
   safe?

6. Should setup automatically edit `.gitignore`, only warn, or require an
   explicit `--update-gitignore` flag?

7. Should uninstall remove dotnetup's path from `global.json`, or should it only
   remove dotnetup install specs and files?

8. What is the exact mapping from dotnetup channels to `global.json`
   `rollForward` and `allowPrerelease` values?

9. Should a hermetic mode exist that writes only `".dotnet"` without `"$host$"`?
   If so, what should it be called?

10. How should the command find the repository/configuration root: nearest
    `global.json`, nearest `.git`, current directory, or an explicit `--root`?
