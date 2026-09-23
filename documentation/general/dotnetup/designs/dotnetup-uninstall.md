# Proposal: predictable uninstall and relationship-based list output

The relationship between an install spec and an installed version can be a source of confusion when using dotnetup.  Some of the main places this can happen are:

- You might ask it to uninstall a specific version that is installed, and it gives you an error that there's no install spec matching that channel
- You might ask it to uninstall a version or channel, and it successfully removes the spec but doesn't remove any installation, because it's referenced by other specs
- You can see that an SDK is installed, but can’t tell what is keeping it installed.  Separate lists of specs and installations don’t show which channel or repository references the version you want to remove

This proposal aims to improve this by improving the behavior of the uninstall commands and the output of `dotnetup list`.

## Proposed behavior

An **install spec** describes what dotnetup should keep installed: a component
and a channel, exact version, or requirement from a registered `global.json`.
An **installation** is a concrete installed version. Multiple specs can resolve
to the same installation.

`dotnetup sdk uninstall` behaves as follows:

- If the argument matches a command-line or migrated install spec, remove it
  without prompting and run normal garbage collection. If its resolved SDK
  remains installed, explain why and show the other specs resolving to it.
  This applies to both channels and exact versions.
- Otherwise, if the argument names an installed SDK version, ask to remove any
  specs currently resolving to it before uninstalling it.
- If neither matches, report an error.

Repository specs remain independent: matching a channel or version does not
directly match a `global.json` registration.

`dotnetup list` shows specs alongside their resolved installations so users
can see what is installed and why.

The uninstall scenarios below use sdk installs in the examples, but should also apply to runtime installs as appropriate.

## Scenarios

### 1. I remove a spec that alone keeps an SDK installed

I ran `dotnetup sdk install 10.0.103`. No other spec needs that SDK.

```console
> dotnetup sdk uninstall 10.0.103
Removed install spec: SDK 10.0.103 (command line).
Uninstalled SDK 10.0.103.
```

The argument matches the saved exact-version spec. Removing it allows
garbage collection to remove the SDK. No extra confirmation is needed.
`dotnetup list` no longer shows either entry. `dotnetup update` does not
reinstall it on behalf of the removed spec.

If the SDK came from migration instead, its saved spec is `10.0.1xx`, not
`10.0.103`. With no other spec keeping the SDK installed, remove it by naming
that channel:

```console
> dotnetup sdk uninstall 10.0.1xx
Removed install spec: SDK 10.0.1xx (migration).
Uninstalled SDK 10.0.103.
```

If updates have replaced `10.0.103` with a newer patch, the same command removes
the channel spec and that newer SDK instead. No extra confirmation is needed.
The user does not need a different uninstall command or source filter for
migrated specs. Naming the installed exact version instead follows scenario 2.

### 2. I name an installed version with no exact-version spec

I ran `dotnetup sdk install 10.0.1xx`, which installed `10.0.103`. There is no
saved spec for the exact version. The channel currently selects `10.0.103`;
this behavior also applies if an older eligible SDK is installed.

The command targets the installed SDK and asks before removing its install spec:

```console
> dotnetup sdk uninstall 10.0.103
.NET SDK 10.0.103 is required by the following install spec:

  .NET SDK 10.0.1xx

To uninstall .NET SDK 10.0.103, this install spec must be removed.
Would you like to remove this install spec? [y/N]
```

Use singular wording for one spec and plural wording for multiple specs:
"the following install specs", "these install specs must be removed", and
"Would you like to remove these install specs? [y/N]".

Accepting removes the SDK and channel spec. Declining changes nothing.
Unlike a channel that is not registered, an exact installed version is a
meaningful target even without a matching saved spec.

### 3. An SDK is required by a global.json install spec

Use this variation of scenario 2 whenever any spec that must be removed comes
from a registered `global.json`, whether it is the only spec or one of several.
Explain that removing a repository spec stops tracking its file without
modifying the file itself.

In this example, a command-line spec for `10.0.1xx` and two registered
repository files currently select SDK `10.0.103`. There is no standalone spec
for exact version `10.0.103`. The same prompt applies even if another installed
SDK could satisfy some of these specs.

```console
> dotnetup sdk uninstall 10.0.103
.NET SDK 10.0.103 is required by the following install specs:

  .NET SDK 10.0.1xx
  .NET SDK 10.0.100 (rollForward: latestPatch, allowPrerelease: false)
    From C:\src\app\global.json
  .NET SDK 10.0.103 (rollForward: disable)
    From C:\src\tools\global.json

To uninstall .NET SDK 10.0.103, these install specs must be removed.
This will stop tracking the listed global.json files, but will not change them.
These repositories may no longer build with the installed SDKs.
Future updates will no longer install SDKs for these specs.
To install a repository's SDK again, run dotnetup sdk install in that repository.

Would you like to remove these install specs? [y/N]
```

Use singular or plural wording for install specs and repository files
independently: several specs may include only one `global.json` file.

Accepting removes the SDK and the listed specs, including the repository
registrations. Declining changes nothing. Apply the displayed plan once; do not
remove one record, refresh stale duplicates, and require the same command again.

`dotnetup update` will not restore the removed repository registration.
Running `dotnetup sdk install` in that repository registers it again.

### 4. An older SDK is also installed

Start with an exact-version pin and a channel spec:

```console
dotnetup sdk install 10.0.103
dotnetup sdk install 10.0.1xx
```

Assume the second command installs `10.0.105`. The exact-version spec pins
`10.0.103`, so it survives cleanup. The channel selects `10.0.105`, as does a
registered repository requiring `10.0.100` with `latestPatch` and no prereleases.
There is **no exact-version spec for 10.0.105**.

Uninstalling `10.0.105` requires removing the channel and repository specs
currently selecting it. Do not silently uninstall the SDK because an older
eligible version is present; ask for confirmation before changing anything:

```console
> dotnetup sdk uninstall 10.0.105
.NET SDK 10.0.105 is required by the following install specs:

  .NET SDK 10.0.1xx
  .NET SDK 10.0.100 (rollForward: latestPatch, allowPrerelease: false)
    From C:\src\app\global.json

To uninstall .NET SDK 10.0.105, these install specs must be removed.
This will stop tracking the listed global.json file, but will not change it.
This repository may no longer build with the installed SDKs.
Future updates will no longer install SDKs for these specs.
To install the repository's SDK again, run dotnetup sdk install in that repository.

Would you like to remove these install specs? [y/N]
```

Accepting removes both listed specs and SDK `10.0.105`. The independent
`10.0.103` pin and its installation remain unchanged. Declining keeps both
SDKs and all specs unchanged.

Although `10.0.103` could satisfy the channel and repository requirements,
uninstall does not move those specs onto it. Only specs currently selecting
the targeted SDK are included in the additional-removal prompt.

Uninstall is not a rollback operation. Rolling back while retaining a spec
would need a separate design that can obtain a previous version even when it
is not already installed and defines what subsequent updates do.

If `10.0.105` also had its own saved spec, spec-first matching would apply, as in scenario 5:
remove that spec, then explain that the channel still keeps the newer SDK.

### 5. The argument matches a spec, but other specs keep the SDK

Both a command-line spec for `10.0.1xx` and a repository select `10.0.105`.

`dotnetup sdk uninstall 10.0.1xx` removes the matching channel spec and garbage
collects without prompting. The SDK remains for the repository. Explain the
result and show the remaining specs that currently resolve to that SDK:

```console
> dotnetup sdk uninstall 10.0.1xx
Removed install spec: SDK 10.0.1xx (command line).
.NET SDK 10.0.105 was kept because the following install spec still resolves to it:

  .NET SDK 10.0.100 (rollForward: latestPatch, allowPrerelease: false)
    From C:\src\app\global.json

No SDKs were uninstalled.
```

This is a successful spec removal, not a request for another decision. Leave
the other specs and repository registrations unchanged. List all other specs
currently selecting the retained SDK, using plural wording when needed.

**The same rule applies to exact-version specs.** If `10.0.105` is a saved
command-line spec and a channel also selects that SDK:

```console
> dotnetup sdk uninstall 10.0.105
Removed install spec: SDK 10.0.105 (command line).
.NET SDK 10.0.105 was kept because the following install spec still resolves to it:

  .NET SDK 10.0.1xx

No SDKs were uninstalled.
```

Do not give exact-version specs a special prompt or implicitly remove the
other specs. A later command naming the installed exact version, once there
is no matching standalone spec, follows scenarios 2 or 3.

If removing the matched spec allows garbage collection to uninstall its SDK,
report the removal as in scenario 1. If it has no installed SDK to begin with,
report spec-only removal without targeting unrelated specs.

### 6. I name a channel that is not registered

```console
> dotnetup sdk uninstall 10.0.1xx
No saved SDK install spec matches '10.0.1xx'.
Run dotnetup list to see installed SDKs and their install specs.
To remove an installed SDK, specify its full version.
```

Return a not-found failure without removing anything, even if `10.0.105` is
installed for some other reason.

This also covers `latest`, `lts`, `preview`, and `daily`: if the named spec
exists, remove it and garbage collect as in scenario 5; otherwise error.
There is no separate "resolve a moving channel for uninstall" experience, no
lookup of the newest downloadable SDK, and no network requirement for uninstall.

### 7. I deleted a repository or changed its SDK requirement

Treat the registered `global.json` as the live requirement, not as a spec
whose cached contents need separate approval to change.

| Change | What the user sees |
| --- | --- |
| Repository or `global.json` deleted | Its spec disappears automatically on reconciliation. No prompt to accept the deletion. |
| Valid file no longer specifies an SDK | The old SDK requirement is removed automatically. |
| Version or roll-forward policy changed | The existing repository entry reflects the new requirement, not an extra historical entry. |
| File is malformed or cannot be read, for example because access is denied | Keep the registration visible with the path and error, but do not use its cached requirement to retain an SDK or block uninstall. Do not treat the error as confirmed deletion. |

Malformed content and access failures can both persist indefinitely, so neither
should keep an SDK installed indefinitely. The previously selected SDK may be garbage
collected if no valid spec keeps it under normal garbage collection.
Explicit uninstall is not blocked by the invalid or unreadable registration
and does not remove it or prompt to remove it. The registration stays visible
with its error, without retaining an installation. Any other specs currently
selecting the SDK still follow the normal uninstall rules.

Do not silently substitute the last successfully read requirement as an active
reference. If the file becomes readable and valid again, reconcile its current
requirement normally. Its previously selected SDK may have been removed, so
the requirement may be unsatisfied until the user installs an SDK again. This
also means a temporary read or parse failure can allow cleanup of an SDK the
repository still needs; the design accepts that tradeoff to avoid indefinite
retention.

`dotnetup list` reflects those changes without deleting SDK files. The next
cleanup-capable operation can collect SDKs no remaining spec keeps.
A changed requirement may appear without a
satisfying installation until `dotnetup sdk install` is run in the repository.

Do not add `dotnetup sdk uninstall --global-json ...` for this initial design.
Deleting the repository naturally stops tracking it. Stopping tracking while
keeping the repository on disk is a possible future niche operation; it should
not drive an overloaded uninstall syntax now. A physical-version uninstall
can still prompt to remove repository registrations as in scenario 3.

### 8. The version's files are missing, or it was never installed

If an exact-version spec exists but its SDK files are already missing,
remove the matching spec without another confirmation. Report:

```text
Removed install spec: SDK 10.0.103 (command line).
SDK 10.0.103 was already missing; no SDK files were removed.
```

If neither a matching spec nor the exact installation exists, return a
not-found failure. Do not uninstall another version.

Pre-existing unsatisfied specs remain visible in `dotnetup list`; they are
not additional removal targets simply because a different SDK is uninstalled.

### 9. SDKs share files, or removal fails

Removing an SDK deletes its private content, but keeps runtime, host, pack, or
other files used by remaining installations. This is normal behavior and does
not need a separate "Shared runtime files retained" message. Removing an SDK
does not necessarily remove every runtime that came in its archive. The overall
uninstall output still needs refinement; focus on what was removed, SDKs kept
because of other specs, and any failures rather than routine shared-file handling.

If a locked file or another error prevents physical removal, return failure
and distinguish removed files from remaining files. Preserve enough state to
show an incomplete installation and support retry or repair; do not claim
rollback after partially deleting files.

A successful spec-only removal is different from a failed SDK removal.
The former says the SDK was kept for other specs; the latter must not
discard specs on the assumption that deleting the SDK succeeded.

If the last SDK is removed, say that no SDK remains in the managed installation.
Do not silently select a system installation, rewrite environment settings, or
remove dotnetup itself. A separate last-SDK confirmation is a policy question,
not assumed by the examples above.

## Scripts and cancellation

Spec-first removal behaves the same with or without an interactive terminal:
remove the matching spec, then report any retained SDK and the specs selecting
it. Do not expand to other specs. Without an interactive terminal, a
physical-version uninstall that needs additional removals instead fails without
mutation and reports the affected specs.

Automation needs a deliberate way to approve affected-spec removal for a
physical-version uninstall. The option remains to be designed; it must not
change matching-spec removal into a physical-version operation.
No new switch is presented as available in this draft.

In an interactive plan, Cancel changes nothing. File changes while the prompt
is open require revalidation before applying it. Automatic reconciliation of
an edited/deleted `global.json` needs no approval, but must not cause an already
approved plan to remove a different SDK or forget an additional live repository.

## Make the relationships visible in dotnetup list

Use one table for SDKs and runtimes with columns in this order: Component,
Install spec, Source, Installed version. Keep one row per install spec, plus
installation-only rows for components without their own specs. Repeating a
component/version pair shows that multiple independent specs select the same
installation. Column order does not change the sorting rules below.

The component is part of the install spec, but is displayed separately from
its channel/version and selection policy. A `global.json` path identifies the
source of the requirement, not the spec itself.

Use these source labels rather than `Explicit`:

| Source | Meaning |
| --- | --- |
| Command line | Added by a command such as `dotnetup sdk install 10.0.1xx`. |
| Migration | Added when bringing an existing SDK into dotnetup management, as in scenario 1. |
| Repository file path | The registered `global.json` supplying the SDK requirement. |

Migration is a proposed distinct origin, not a claim about today's stored types.

```text
Component        Install spec                                                  Source                      Installed version
---------------  ------------------------------------------------------------  --------------------------  -----------------
SDK              10.0.100 (rollForward: latestPatch, allowPrerelease: false)   C:\src\app\global.json      10.0.105
SDK              10.0.1xx                                                      Migration                   10.0.105
SDK              10.0.103                                                      Command line                10.0.103
SDK              10.0.300 (rollForward: disable)                               C:\src\other\global.json    Not installed
SDK              Unavailable                                                   C:\src\tools\global.json    Unknown
.NET Runtime     10.0                                                          Command line                10.0.5
ASP.NET Core     10.0                                                          Command line                10.0.5
Windows Desktop  10.0                                                          Command line                10.0.3
.NET Runtime     9.0                                                           Command line                9.0.8
ASP.NET Core     9.0                                                           Command line                9.0.8

10 install specs; 7 installations (2 SDKs, 5 runtimes)

Warning: Cannot read C:\src\tools\global.json: access denied.
```

For runtime specs, show `10.0`, not command-line syntax such as `runtime@10.0`:
the Component column already says which runtime it applies to. For repository
specs, show the version and relevant selection policy from the file in the
Install spec column, and the file path in Source. Do not reduce a repository's
minimum version and roll-forward policy to a lossy channel label.

Label repository policy values with their `global.json` property names:
`rollForward: disable` means no SDK roll-forward, not a disabled install spec.
Likewise, use `rollForward: latestPatch` and `allowPrerelease: false` rather
than unlabeled abbreviations. Keep the values inline when they fit. Do not
force each policy onto its own line; wrap within the same Install spec cell
only when terminal width requires it. The example shows the wide-terminal
layout, not a requirement to fit long specs and paths on one line everywhere.

There is no separate Requirement / status column. Version-selection information
belongs to the spec; installed-version availability belongs in Installed version.
Show exceptional diagnostics below the table, identifying the affected source
or component/version, rather than reserve a mostly empty column for them.

The example deliberately gives the older SDK a command-line exact-version pin.
The migrated `10.0.1xx` spec selects `10.0.105`, the latest installed patch in
that feature band; it does not pin the originally migrated `10.0.103`.

The Windows example includes the .NET, ASP.NET Core, and Windows Desktop
runtimes. Only show components present in the managed installation; the table
does not imply that all three runtime types are available on every platform.

Group by ordering rather than adding divider lines or blank rows between every
group. Use two top-level groups: SDKs first, then all runtimes together. Within
each group, sort by resolved installed version, newest first, using semantic
version ordering rather than alphabetical ordering.

For runtimes with the same version, use a fixed component order: .NET Runtime,
ASP.NET Core, Windows Desktop. Then break ties by install spec and source in
ascending text order. This keeps runtime components from the same release
together instead of separating them into component-first lists. The example
shows both aligned runtime versions and a Windows Desktop runtime at a different
patch level.

Put specs with no usable resolution at the end of their respective SDK or
runtime group: Not installed first, then Unknown, with the same component,
spec, and source tie-breakers. Keep a header rule, but default to a compact
body, especially when most groups contain only one row.

Keep rows selecting the same component/version adjacent without hiding repeated
values: do not merge cells, leave versions blank, or substitute ditto marks.
Every row should remain meaningful when copied on its own. Whether sparse
separators or subtle shading improve readability is worth trying with realistic
small and large inventories; neither is required by this draft. Any styling
must also remain readable without color.

"Installed version" means the version selected from what is installed, not the
latest version available to download. Resolve repository rows using the full
`global.json` requirement, not just the abbreviated display. "Not installed"
means no satisfying installation of that component was found; "Unknown" means
the requirement could not be evaluated. Do not turn a read error into a claim
that nothing is installed.

The table describes current selection, not exclusive ownership or every
potential match. In this example, `10.0.103` could also satisfy `10.0.1xx` and
the app repository, but both currently select `10.0.105`, so neither gets
another row for `10.0.103`. Uninstall uses these current selections to identify
the affected specs; it does not plan alternative selections after removal.
Long source paths and version-selection policies should wrap within their
columns rather than be silently truncated.

Count distinct component/version installations, not rows or version strings
alone, in the total. For example, .NET Runtime `10.0.5` and ASP.NET Core
`10.0.5` count separately, as do different versions of the same runtime
component. This list presentation does not expand the SDK uninstall proposal
into a runtime-command redesign.

Do not hide installed components that have no selecting spec of their own.
Add rows to the same table with `-` in the Install spec column. In Source, a
runtime supplied by an SDK is labeled "Included with SDK <version>"; a managed
SDK left after a repository deletion can be labeled "No current install specs."
These are installation-only rows, not synthetic
specs. They count toward installation totals but not spec totals.

If a runtime is both supplied by an SDK and selected by a runtime spec, annotate
the existing runtime row's Source with that shared relationship rather than count
another installation. List SDK-supplied runtimes even without a standalone runtime
install record, while keeping internal host and pack directories out of this
SDK/runtime inventory. This requires deriving shared-component relationships,
not just displaying the current manifest's top-level installation records.

Do not call managed components without their own specs "untracked":
`--untracked` already means files dotnetup does not manage. A confirmed-deleted
repository disappears from the spec table; a malformed or unreadable file
remains visible with Unavailable version information, Unknown resolution,
and its parse or access error.
This row does not represent an active reference that keeps an SDK installed;
do not display its cached requirement as if it were current.

Invalid installations mark the version as invalid and show the validation
error below the table. With `--no-verify`, show an installation-health-unverified
notice rather than implying installed files were checked.
Listing reflects live repository requirements, but does not install or delete
SDK content.

### JSON and other output surfaces

Keep `dotnetup list --format json` and its existing `installSpecs` and
`installations` arrays. Adopting friendlier text labels does not require renaming
those properties or flattening the JSON model into the text table's repeated
version values.

Relationship/status fields and migration provenance need a separately reviewed
machine-readable contract, including stable identities. Migration-origin
changes must account for existing consumers rather than being treated as a
cosmetic text change. Likewise, exposing SDK-supplied runtimes needs an additive
inventory representation rather than silently reinterpreting the existing
`installations` array as including subcomponents. Other users of the shared
installation lister should use the same user-facing labels and relationships.

## Correctness prerequisites and cleanup scope

Relationship calculation needs to be consistent across operations. Duplicate
repository records are an existing correctness bug, not a new uninstall scenario.

- Refresh repository requirements before matching specs or computing what
  garbage collection would remove.
- Keep one logical entry per registered repository file, not one per historical
  derived channel. Consolidate legacy duplicates.
- Apply the full SDK requirement, including version lower bounds, roll-forward,
  and prerelease policy, when determining which installed SDK each spec
  currently selects.
- Keep malformed or unreadable repository registrations visible with diagnostics,
  but exclude their cached requirements from SDK retention and uninstall blockers.
- Use the same relationships in list, uninstall planning, and cleanup.

This proposal preserves normal garbage collection across the managed
installation. Cleanup may remove other SDKs no remaining spec keeps, including
SDKs made unused by repository changes or deletion. It is not limited to the
version or channel named in the uninstall command. Report the actual removals.

## Remaining decisions

| Decision | Current position in this draft |
| --- | --- |
| User-facing name | Try "Install spec" for both SDKs and runtimes; terminology remains open. |
| List layout | Component, Install spec, Source, Installed version. SDKs first, then all runtimes; resolved version descending within each group, then component, spec, and source. Missing/unknown resolutions go last within their group. Include installation-only rows. |
| Source labels | Command line, Migration, Repository. Migration creates a latest-patch channel spec, such as `10.0.1xx` for SDK `10.0.103`, with the same retention behavior as a command-line spec for that channel. |
| Migration provenance | If a migrated spec is subsequently requested on the command line, preserve useful provenance without requiring duplicate uninstalls. Decide whether to retain multiple origin annotations or replace the displayed origin. |
| Match a spec or version first? | Match saved standalone specs first, whether channel or exact version; fall back to an installed exact version only. |
| Match repository-derived channel strings directly? | Not by default. Keep repository registrations independent and show them as additional effects. |
| Roll back while retaining specs? | Outside this uninstall proposal. Offer to remove specs currently selecting the targeted SDK, not reassign them to an older installation. A rollback design should not depend on a previous version already being installed. |
| Noninteractive approval | Define how automation approves affected-spec removal for physical-version uninstall without changing matching-spec behavior. |
| Additional commands | A separate `untrack` command, repository-path uninstall syntax, and undo are outside this proposal. |
| Remove one repository registration while keeping its files? | Defer a dedicated UI. File deletion/change is authoritative without confirmation. |
| Existing `--source` behavior | Compatibility/migration policy still needs design; do not invent or silently change its meaning here. |
| Custom paths and architecture UI | Outside this proposal. Omit architecture noise from ordinary messages. |
| Runtime uninstall and bulk removal | Do not redesign these commands as part of the initial SDK proposal. |
| Last-SDK warning | Report the resulting empty state; decide separately whether it warrants an additional prompt. |

## Current behavior and supporting issues

The current implementation is context, not evidence that the proposed experience
already works:

- [`UninstallWorkflow.Execute`](../../../../src/Installer/dotnetup.Library/Commands/Shared/UninstallWorkflow.cs)
  matches stored specs by component, version/channel string, and source before
  calling garbage collection. It can report that target installations remain.
- [`GarbageCollector.Collect`](../../../../src/Installer/dotnetup.Library/GarbageCollector.cs)
  refreshes repository specs, retains the latest matching installation for each,
  and removes other installations across the root.
- [`GlobalJsonChannelResolver.ResolveChannel`](../../../../src/Installer/dotnetup.Library/GlobalJsonChannelResolver.cs)
  reduces repository requirements to channel strings. Correct SDK selection
  cannot assume that this preserves the full SDK requirement.
- [`InstallationLister`](../../../../src/Installer/dotnetup.Library/Commands/List/ListCommand.cs)
  displays separate "Tracked channels" and "Installed versions" sections.
  [`InstallSpec` and `Installation`](../../../../src/Installer/dotnetup.Library/DotnetupManifestData.cs)
  are separate manifest concepts.
- The current [`SDK install`](../reference/dotnetup-sdk-install.md) and
  [`SDK uninstall`](../reference/dotnetup-sdk-uninstall.md) references document
  the SDK-qualified command names used throughout this draft.

| Issue | Relevance |
| --- | --- |
| [dotnet/sdk#56228](https://github.com/dotnet/sdk/issues/56228) | Noah's original proposal separates untracking from physical uninstall and retains unsatisfied specs with an override. This draft instead removes matching specs without prompting and confirms affected-spec removal when targeting an installed version with no matching standalone spec. |
| [dotnet/sdk#56225](https://github.com/dotnet/sdk/issues/56225) | Stable repository identity and consolidation of duplicate records. |
| [dotnet/sdk#56226](https://github.com/dotnet/sdk/issues/56226) | Refresh requirements before matching; avoid repeated identical uninstalls. |
| [dotnet/sdk#56227](https://github.com/dotnet/sdk/issues/56227) | Preserve minimum versions, roll-forward, and prerelease semantics when determining satisfaction. |
| [dotnet/sdk#53396](https://github.com/dotnet/sdk/issues/53396) | Prerelease-policy correctness as well as documentation. |
| [dotnet/sdk#55312](https://github.com/dotnet/sdk/issues/55312) | Example of uninstall triggering collection of another unused SDK. Normal garbage collection is preserved by this proposal. |

This revision changes only the proposal. Command reference, runtime messages,
and implementation should change together once the behavior is agreed.
