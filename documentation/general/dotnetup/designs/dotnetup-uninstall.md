# Proposal: predictable uninstall and relationship-based list output

**Status:** Draft for discussion, September 11, 2026. This document describes
proposed behavior, not the current command contract or a settled team decision.
Command options explicitly marked **new** do not exist today.

## Experience we want

When I ask to uninstall an SDK, that SDK should actually go away. Before
dotnetup also stops maintaining a requirement for one of my repositories, it
should tell me which repository and let me decide whether to proceed.

`dotnetup list` should help me understand this before I run uninstall: what is
installed, why dotnetup is keeping it, and which install specs would be affected
if I removed it.

The proposed default is:

- Remove the installation I requested, not just a record that refers to it.
- Remove the explicit install spec I am undoing, if there is one.
- Show and offer to remove other install specs that would lose their last
  satisfying installation. Do not remove those specs silently.
- Keep independent specs when another remaining installation satisfies them.
- Leave unrelated installations alone. Uninstall is not whole-root cleanup.

Keep the name `uninstall`; do not require users to learn a separate `untrack`
command for the common case. Retaining intentionally unsatisfied specs is an
alternative discussed below, not the default in this proposal.

## Terms used in the scenarios

An **install spec** is a requirement dotnetup maintains: an explicit channel,
an explicit exact version, or a requirement from a particular `global.json`.
Use "install specs" in the UI rather than "tracked channels"; not every spec
is a channel.

An **installation** is a concrete SDK or runtime component version, in a
particular installation root and architecture. Several specs can select the
same installation. An installation can also share files with another
installation.

A **satisfying installation** meets the actual requirement, not just a similar
version prefix. For `global.json`, that includes the requested version,
roll-forward policy, and prerelease policy. A spec's **selected installation**
is the one dotnetup currently selects from the installed versions. Other
installed versions may be eligible fallbacks after uninstall.

Examples use illustrative SDK versions and `C:\dotnet` as the explicitly
selected or currently configured dotnetup-managed installation root. The same
behavior applies on other platforms. Uninstall does not change `global.json`
contents, project files, PATH, or environment configuration.
Unless noted otherwise, assume another SDK remains in the root; scenario 16
adds confirmation when uninstall would remove the last SDK.

## Scenarios

### 1. I undo an exact-version install

I installed `10.0.103` explicitly. No other spec uses it.

```console
> dotnetup uninstall 10.0.103
Uninstalled SDK 10.0.103 from C:\dotnet (x64).
Removed install spec: SDK 10.0.103 (explicit).
```

No extra confirmation is needed: both changes directly undo my request.
`list` no longer shows either entry. `update` does not reinstall it on behalf
of that removed spec. A later explicit `install` can add it again.

### 2. The SDK was installed for a channel, not by exact version

I installed `10.0.1xx`, which selected `10.0.103`. I now run
`dotnetup uninstall 10.0.103`.

The command finds the physical SDK even though no exact-version spec exists.
It does not tell me "no matching install spec" or require `--source all`.
Because removing it would also stop maintaining `10.0.1xx`, it shows:

```console
> dotnetup uninstall 10.0.103
Uninstall SDK 10.0.103 from C:\dotnet (x64).

This would also remove these install specs:
  SDK 10.0.1xx (explicit)

No remaining installed SDK satisfies these specs.
Future updates will no longer maintain them.
Uninstall the SDK and remove these install specs? [y/N]
```

Accepting removes the SDK and that spec. Declining, or pressing Enter, changes
nothing. Saying "Done" while retaining the requested SDK is not an outcome.

### 3. Several repositories and explicit specs share one SDK

`10.0.103` is selected by an explicit `10.0.1xx` spec and by
`C:\src\app\global.json` and `C:\src\tools\global.json`. No other installed SDK
satisfies those requirements.

Uninstall shows all three specs in one confirmation, including each full
repository file path. Accepting removes the SDK and all three specs in one
operation. It does not remove one record, refresh others, and require me to
run the same command again.

The confirmation explains that those repositories may no longer build with
this installation root. It also says:

```text
The global.json files will not be changed.
To maintain a repository's SDK again, run dotnetup install in that repository.
```

Removing tracking is not deleting the repository's requirement. Dotnetup must
not rediscover and silently re-add the removed repository specs on the next
general `update`. A new `install` from that repository is an intentional request
to add its spec again.

### 4. Another installed SDK can take over

`10.0.103` and `10.0.105` are installed. The latter has an explicit exact-version
spec. An explicit `10.0.1xx` spec and a repository requirement with minimum
`10.0.100`, `latestPatch`, and no prereleases both select `10.0.105`.

I run `dotnetup uninstall 10.0.105`.

Dotnetup removes the SDK and its explicit `10.0.105` spec. It retains the channel
and repository specs because `10.0.103` satisfies both. The output states that
those specs now select `10.0.103`; it does not describe them as deleted or broken.
There is no additional-spec-removal confirmation.

It also warns:

```text
SDK 10.0.1xx is still maintained. A future update may install 10.0.105 again
or install a newer eligible version.
```

This is intentional: uninstall is not a version blacklist. If my goal is to
stop maintaining that channel entirely, I should name the channel instead.
Independent repository specs may still maintain versions in that channel.

### 5. Only some of the other specs have a fallback

In the previous scenario, one repository instead requires a minimum of
`10.0.105` with `latestPatch`. `10.0.103` is not a fallback for that repository.

The confirmation separates the outcomes:

| Outcome | Install spec |
| --- | --- |
| Removed as directly requested | Explicit `10.0.105` |
| Also removed, subject to confirmation | That repository's `global.json` |
| Kept, now selecting `10.0.103` | Explicit `10.0.1xx` and any compatible repository specs |

Accepting removes only the listed specs. The same rule applies to prereleases:
a preview below a requested minimum, or one excluded by `allowPrerelease`, is
not a reason to claim that a repository still has a working SDK.

### 6. I name a channel rather than a version

I run `dotnetup uninstall 10.0.1xx` to undo `dotnetup install 10.0.1xx`.

**Proposed distinction:** naming a channel removes the explicit spec with that
name, if present, and targets the single installed SDK currently selected for
it. The plan shows the exact version. It does not mean "remove every SDK whose
version starts with 10.0.1."

If `10.0.105` is selected and `10.0.103` is also installed, the plan removes
`10.0.105` and the explicit `10.0.1xx` spec. It leaves `10.0.103` alone.
Independent specs selecting `10.0.105` follow the same fallback and confirmation
rules as an exact-version uninstall.

If no explicit `10.0.1xx` spec exists, dotnetup may still select an installed
version that satisfies the channel. It must show the concrete target before
acting. Repository specs are independent requirements, not implicitly selected
for removal just because their displayed channel is also `10.0.1xx`.

This gives `uninstall <channel>` an "undo this install request" meaning, while
`uninstall <version>` means "remove this particular installation." Both actually
remove the selected installation.

### 7. I name a broad or moving channel

For `latest`, `lts`, `preview`, or `daily`, selecting a target must not mean
looking up the newest downloadable SDK and trying to uninstall something I
never installed.

Use the channel's locally established selection and eligibility information,
and print the concrete target. If that information is insufficient to select
unambiguously, do not guess from the highest version number. List the installed
candidates and ask me to rerun with an exact version.

Uninstall must work without network access. It must not download a replacement
SDK to make the operation safe, or silently broaden the request to all matching
versions. Metadata needed for reliable named-channel relationships may need to
be retained at install/update time.

### 8. I stop maintaining one repository, but keep a shared SDK

Two repository specs select `10.0.103`. I stop working on one repository.
I should not have to uninstall a working SDK or remove the other repository's
spec to express this.

Proposed **new** form:

```console
dotnetup uninstall --global-json C:\src\app\global.json
```

This selects that repository's spec by file identity, not its current derived
channel. It removes the spec and removes its selected installation only if no
other remaining spec needs that installation. If the SDK stays, the output
explicitly says "Removed repository install spec; SDK 10.0.103 kept for ..."
and lists the other specs. It does not claim the SDK was uninstalled.

This is a spec-targeted operation, unlike uninstalling a version. It should
show the distinction in help and in its plan. The path selector is mutually
exclusive with a version/channel argument. An explicit `--install-path`
selects the root as usual; otherwise, if that file is tracked in multiple roots,
require root disambiguation rather than removing all of them.

This is the main additional UX choice to review: a path selector on `uninstall`
keeps the common vocabulary small, but a separate spec-removal command would
make the distinction more explicit.

### 9. I switched branches and global.json changed

The same repository used to require `10.0.1xx` and now requires `10.0.3xx`.
Both SDK versions are still installed.

`list` shows one logical repository spec, with the current requirement. Its
identity is the normalized file path within the root, architecture, and
component, not the old channel value. Existing duplicate records are treated
as one spec; different repository paths remain independent.

Uninstall refreshes and consolidates those requirements **before** computing
the removal plan. Removing the old SDK does not also remove the repository spec
if it now selects the new SDK. Removing the new SDK shows the actual current
repository requirement in the confirmation.

Refreshing a requirement does not authorize deleting its formerly selected
SDK as a side effect of uninstalling something else. For example, uninstalling
`8.0` must not delete an old `10.0.204` installation. Install/update cleanup
policy can be considered separately.

If the changed file now points to a different installation root, display that
the registration needs reconciliation; uninstall does not silently migrate
tracking or delete content in the other root.

### 10. A repository file disappeared or cannot be read

`list` keeps the repository visible, with a state such as "global.json missing"
or "cannot read requirement," rather than silently treating it as unnecessary.
Likewise, a valid file that no longer specifies an SDK is shown as having no
current SDK requirement, pending removal of its old registration.

Uninstall of a possibly related SDK shows the last known requirement and the
uncertainty. It asks for explicit consent to remove that repository registration
along with the SDK; it never asserts that another version satisfies an unreadable
requirement. If the missing information prevents even identifying the affected
set, fail with the path and reason rather than expanding the deletion set.

The path-targeted operation in scenario 8 can remove a stale registration even
if the file no longer exists. Removing a stale registration must not require
recreating or repairing a repository that I intentionally deleted.

### 11. Files are missing, or the requested version was never installed

If the requested SDK and a directly matching explicit spec are both absent,
report "SDK ... is not installed at ..." and return a not-found failure. Do not
uninstall a different version or prune unrelated specs.

If an exact explicit spec remains but its SDK files are already missing, show
a spec-only cleanup plan: "SDK files are already missing; remove this install
spec?" Report the cleanup separately from physical removal. Do not cascade into
other already-unsatisfied specs as though this request had broken them.

`list` distinguishes missing or invalid installations from valid installations.
Pre-existing unsatisfied specs are visible; they are not removed as incidental
cleanup during another SDK's uninstall.

### 12. SDK and runtime installations share files

Removing an SDK removes its SDK installation and private content. Runtime,
host, pack, or other files still needed by remaining installations stay.
The result may say "Shared runtime files retained." It must not imply that
removing an SDK necessarily removes every runtime that came in its archive.

For `dotnetup runtime uninstall runtime@10.0.3`, the target is that runtime
component, not an SDK with a similar version. The install-spec rules otherwise
apply in the same way.

There is an important limit: if the requested runtime files are also required
by a remaining SDK installation, dotnetup cannot both remove that runtime and
preserve the SDK. Refuse physical removal with an explanation naming the
dependent installation. Do not silently uninstall the SDK, or remove just a
standalone runtime record and claim that the runtime disappeared.

The initial proposal does not add a force option that breaks remaining
installations, or recursively uninstalls other components.

### 13. The same version exists in another installation root

`dotnetup uninstall 10.0.103 --install-path C:\dotnet` only acts within
`C:\dotnet`. An installation at `D:\tools\dotnet` and its specs are unaffected.
Print the selected root and architecture in the plan and result.

Without `--install-path`, preserve the current uninstall root-selection rule:
use the configured dotnetup-managed root, otherwise the default managed root.
Do not infer ownership from whichever `dotnet` happens to be first on PATH.

Do not add cross-root or all-architecture deletion implicitly. If a future
multi-architecture layout makes the target ambiguous, require explicit
disambiguation. Files installed with `--untracked` or owned by another installer
are not uninstall targets.

### 14. I run uninstall in a script, or decline a prompt

A removal that only undoes the directly named explicit spec and installation
can proceed without interaction. Removing additional specs requires consent:

| Situation | Proposed behavior |
| --- | --- |
| Interactive terminal | Show the complete plan, including affected paths; default to cancel. |
| Redirected input / non-interactive execution | Show the plan and fail without mutation when additional consent is needed. |
| **New** `--yes` / `-y` | Accept the displayed plan, including the explicitly listed additional spec removals. Still print it. |
| Decline or cancel | Do not remove installations or specs; return non-success. |

`--yes` is consent, not a force bypass. It cannot bypass ownership checks,
unknown target selection, or shared-file safety. Before applying an accepted
plan, revalidate it if the manifest or repository files changed while I was
reading the prompt. Do not delete a newly affected spec under an old approval.

### 15. A file is locked, or removal is interrupted

If dotnetup cannot remove the requested installation, return failure and name
the remaining files or installation. Do not finish with an unqualified "Done."
Do not discard specs on the assumption that removal succeeded.

If some files were removed before failure, report partial removal rather than
claiming rollback. Preserve enough state for `list` to show the incomplete
installation and for retry or repair to work. Retrying should not require
hand-editing the manifest.

### 16. I removed the last SDK in my active root

Warn in the plan that no SDK will remain in this root, even if no additional
specs need removal. Require confirmation, or `--yes` in a script. On success,
make the empty SDK state clear; runtimes may still be present.

Do not silently switch to a system installation or rewrite shell profiles.
Dotnetup itself remains installed. `dotnetup install <spec>` can populate the
root again; uninstalling dotnetup or resetting its environment is outside this
proposal.

## Make the relationships visible in dotnetup list

Keep installation root as the top-level grouping, include architecture, then
show each concrete installation once with its selected install specs beneath it.
Do not keep two unrelated-looking sections that require me to correlate them.

Illustrative text layout:

```text
.NET managed by dotnetup

  C:\dotnet (x64)

    SDK 10.0.105
      Install specs:
        10.0.105             explicit
        10.0.1xx             explicit
        global.json          C:\src\app\global.json
          version: 10.0.100; rollForward: latestPatch; allowPrerelease: false

    SDK 10.0.103
      No install spec currently selects this installation.
      Eligible fallback for: 10.0.1xx; C:\src\app\global.json

    Runtime 10.0.3
      Install specs:
        runtime@10.0         explicit

    Install specs without a usable installation:
      global.json            C:\src\other\global.json
        version: 10.0.300; rollForward: disable
        No satisfying SDK installed.

    Install specs requiring attention:
      global.json            C:\src\old\global.json
        File missing; last known requirement: SDK 9.0.3xx

  Total: 3 installations
```

The tree describes current selection, not exclusive ownership. One spec appears
under its selected installation, not under every version it could match.
Fallback annotations explain scenario 4 without duplicating the spec as though
it were another independent registration. A runtime included only as an SDK
subcomponent is not counted as an additional standalone installation; when
needed, label it as shared content instead.

Specs from different repository paths remain distinct even when their
requirements are identical. Full paths are available in text output. Use
"No install spec currently selects this installation," not "untracked":
`--untracked` already means files dotnetup does not manage. An unselected
managed installation may still be a useful fallback and is not automatically
deleted by `list` or an unrelated uninstall.

Invalid installations have a visible validation error rather than appearing
usable. With `--no-verify`, label installation health as unverified; any
satisfaction/fallback annotations are conditional on those files being usable.
Unknown channel eligibility or unreadable repository requirements belong in
"requiring attention," not under a guessed selected installation.

`list` and uninstall must use the same requirement semantics and reconciliation
rules. Listing does not install SDKs, remove specs, or perform garbage collection.
It can reflect current repository contents without treating observation as
permission to delete the old SDK or registration.

### JSON and other output surfaces

Keep `list --format json` and its existing `installSpecs` and `installations`
arrays. Do not replace the machine-readable contract with the text tree.
Relationship/status fields would be an additive, separately reviewed extension,
including a way to identify endpoints by root, architecture, component,
version, and spec identity. Do not require scripts to infer relationships from
display strings or tree indentation.

Other output using the shared installation lister should use the same terms and
relationships. Update help and command reference only when the behavior ships;
the current reference should not claim this draft is implemented.

## Choices and tradeoffs still worth reviewing

| Choice | Recommendation and tradeoff |
| --- | --- |
| Keep unsatisfied specs after uninstall? | Not by default. Removing newly unsatisfied specs after consent avoids leaving hidden requests that can reinstall what I just removed. Consider a future explicit keep-specs option if temporary removal is common. |
| Add `untrack`? | Defer it. The path-targeted form handles one-repository removal, but its spec-only behavior is a reason to revisit a separate command if `uninstall` becomes too overloaded. |
| Remove all matching versions for a channel? | No. Remove the displayed selected version and named explicit spec; leave other versions alone. Bulk removal needs a separate, explicit plan. |
| Preserve `--source`? | Do not let it filter physical exact-version targets. Retire it from the default experience; during transition, reject supplied `--source` with migration guidance rather than silently reinterpret an old script. A future spec-management surface could retain source filters. |
| Guarantee a version never returns? | No blacklist in this proposal. Retained specs may request it on update. Explain that consequence when retaining a channel; deleting every broadly compatible spec would be too destructive. |
| Suggest `update` instead? | When uninstall would drop a moving spec, offer a short hint that `update` may replace the old SDK while preserving the requirement. Never run update as part of uninstall. |

Channel selection, the repository-path form, retirement of `--source`, and
last-SDK confirmation are proposed refinements for review, not existing
agreements. Hashing repository files is an implementation choice, not part of
the user contract.

## Current behavior and supporting issues

The current implementation differs materially from this proposal:

- [`UninstallWorkflow.Execute`](../../../../src/Installer/dotnetup.Library/Commands/Shared/UninstallWorkflow.cs)
  matches stored specs by component, version/channel string, and source before
  calling garbage collection. It can report that target installations remain.
- [`GarbageCollector.Collect`](../../../../src/Installer/dotnetup.Library/GarbageCollector.cs)
  refreshes repository specs, retains the latest matching installation for each,
  and removes other installations across the root. This explains how an
  unrelated old SDK can disappear during uninstall.
- [`GlobalJsonChannelResolver.ResolveChannel`](../../../../src/Installer/dotnetup.Library/GlobalJsonChannelResolver.cs)
  currently reduces repository requirements to channel strings.
  Correct fallback decisions are a prerequisite for this proposal, not a
  capability to assume from the current matcher.
- [`InstallationLister`](../../../../src/Installer/dotnetup.Library/Commands/List/ListCommand.cs)
  currently displays separate "Tracked channels" and "Installed versions"
  sections. Its JSON model already names the requirements `installSpecs`.
  [`InstallSpec` and `Installation`](../../../../src/Installer/dotnetup.Library/DotnetupManifestData.cs)
  are separate manifest concepts.

Public issue context:

| Issue | Relevance |
| --- | --- |
| [dotnet/sdk#56228](https://github.com/dotnet/sdk/issues/56228) | Noah's original proposal separates untracking from physical uninstall and retains unsatisfied specs with an override. This draft intentionally chooses a different default. The review comment requests an interactive prompt. |
| [dotnet/sdk#56225](https://github.com/dotnet/sdk/issues/56225) | Stable repository-spec identity and consolidation of duplicate records. |
| [dotnet/sdk#56226](https://github.com/dotnet/sdk/issues/56226) | Refresh requirements before matching; avoid repeated identical uninstalls. |
| [dotnet/sdk#56227](https://github.com/dotnet/sdk/issues/56227) | Preserve minimum versions, roll-forward, and prerelease semantics when determining satisfaction. |
| [dotnet/sdk#53396](https://github.com/dotnet/sdk/issues/53396) | Existing prerelease-policy issue, with Noah's follow-up explaining why it affects correctness, not just documentation. |
| [dotnet/sdk#55312](https://github.com/dotnet/sdk/issues/55312) | Concrete report of uninstalling `8.0` removing an unrelated `10.0.204` SDK. |
| [dotnet/sdk#53393](https://github.com/dotnet/sdk/issues/53393) | Earlier root-command scope discussion. This draft preserves the current SDK alias rather than making uninstall remove dotnetup itself. |

The scenarios above are the proposed acceptance criteria. Shipping this design
requires changing physical removal and requirement evaluation, not merely
renaming the command or reorganizing `list`.
