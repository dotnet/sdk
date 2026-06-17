# `dotnetup env` command design (replaces `dotnetup defaultinstall`)

Tracks: [#53742](https://github.com/dotnet/sdk/issues/53742) (primary).
Adjacent / forward-compatible with: [#53838](https://github.com/dotnet/sdk/issues/53838) (init walkthrough → two y/n prompts), [#53837](https://github.com/dotnet/sdk/issues/53837) (init walkthrough → summary-first).

---

## Problem

`dotnetup defaultinstall <user|system>` is misnamed and under-specified:

- It's not really about a "default install" — it's about how `dotnet` ends up on your `PATH` and what `DOTNET_ROOT` points at.
- The argument values `user` / `system` mean different things on Windows vs Unix today:
  - Windows `user` ≈ user PATH + `DOTNET_ROOT` (registry-level env vars)
  - Windows `system` ≈ admin PATH + `DOTNET_ROOT`
  - Unix `user` ≈ shell profile, dotnet enabled
  - Unix `system` ≈ shell profile, dotnetup only (no `DOTNET_ROOT`)
- It doesn't persist `DotnetAccessMode` in `dotnetup.config.json`, even though the `init`
  walkthrough writes that exact preference (see `DotnetupConfig.DotnetAccessMode`).
- It pre-dates the PowerShell-profile-on-Windows work, so it doesn't know that
  shell-profile mode is now a valid Windows option.

## Goals

1. Replace `defaultinstall` with a clearly-named command whose argument values map 1:1
   to the renamed `DotnetAccessMode`.
2. Always persist the chosen mode to `DotnetupConfig` so re-runs and the init flow stay
   consistent.
3. **Support re-sync**: re-running `env set <same-mode>` re-applies the mode without
   needing to remember any extra state — useful when something else has clobbered
   the env vars dotnetup manages. The mode is stored in `dotnetup.config.json` and
   shown by `env show`, so the user doesn't need to remember it.
4. Make the command idempotent: running it again with the same mode is a no-op,
   switching modes correctly undoes the previously applied changes.
5. Stay forward-compatible with the planned walkthrough refactors (#53838, #53837):
   those redesigns end up calling the same shared apply helper that this command uses,
   so they don't need to duplicate logic.
6. Manage **whether `dotnetup` itself is on `PATH`** as a first-class, persisted setting
   that is orthogonal to the dotnet-access mode — so the install script only has to
   download `dotnetup`, and running `dotnetup` manages its own `PATH` entry. See
   [`dotnetup` on PATH: an orthogonal setting](#dotnetup-on-path-an-orthogonal-setting).

## Non-goals

- Implementing #53838 (two y/n prompts) or #53837 (summary-first) directly. We'll just
  factor the apply logic into a shared helper that both `init` and the new command call.
- Adding a way to inspect / change which *shell* the profile gets written to beyond the
  existing `--shell` option.
- Cross-machine "machine-wide" mode that affects other users (`AllUsersAllHosts`
  profile, machine-level `HKLM` PATH) — out of scope; requires admin and broader policy.
- Backwards-compat alias for `defaultinstall`. Clean rename.

## Proposal

Replace `dotnetup defaultinstall <user|system>` with a noun-verb `env` command family
backed by a renamed three-value `DotnetAccessMode` enum:

```
dotnetup env set <none|shell|all>   # persist mode in config and apply
dotnetup env show                   # display current mode; if applied state has
                                    # drifted from the configured mode, report that
                                    # too
dotnetup env script                 # print a shell-specific script that exports
                                    # PATH/DOTNET_ROOT for the current dotnet
                                    # install (formerly `dotnetup print-env-script`)
```

`env set` is idempotent — running it again with the same mode is a no-op (and is also
how you re-sync after something else has clobbered your PATH; see the key scenario
below).

Modes:

- `none` — Don't modify your environment; use `dotnetup dotnet` to invoke.
- `shell` — Wire dotnetup into your shell's profile file (only shells that load
  profiles see the user dotnet).
- `all` — Wire dotnetup into your shell profile **and** your user-level
  PATH/DOTNET_ROOT so cmd.exe, IDEs, and shortcuts also see it.

`DotnetAccessMode` enum renames 1:1: `None` / `Shell` / `All`. JSON serialization
follows.

A second, orthogonal setting — whether `dotnetup` itself is on `PATH` — is described in
[`dotnetup` on PATH: an orthogonal setting](#dotnetup-on-path-an-orthogonal-setting)
below. It is still under discussion.

Apply logic lives in a shared `DotnetAccessModeApplier` helper so that `dotnetup init`
and `dotnetup env set` go through the same code path. The walkthrough refactors in
#53838 / #53837 then only need to change the question-asking layer, not the apply
layer.

The existing top-level `dotnetup print-env-script` command moves under the `env`
family as `dotnetup env script` (it generates env-var wiring — same domain). To
avoid breaking managed profile blocks written by previous dotnetup versions, the
old `print-env-script` name is kept as a hidden top-level alias for one release,
then removed.

The rest of this document explains the reasoning behind each choice and the
alternatives considered.

## Key scenario: re-sync after a system .NET update overwrites the system PATH

> A user has chosen `Shell` mode. In `Shell` mode dotnetup writes the user dotnet path
> only to the user's shell profile. That means **only PowerShell** sees the user
> dotnet on PATH — the profile prepends the user dotnet path to `$env:PATH`. `cmd.exe`
> reads no profile, so from cmd `dotnet` still resolves to whatever the Windows
> system/user env-var `PATH` says, which on a typical box is the admin-installed
> `C:\Program Files\dotnet\`.
>
> To make `cmd.exe` (and GUI apps, IDE shortcuts launched from Start menu, etc.) see
> the user dotnet, the user picks `All` mode. That mode prepends the user dotnet path
> to the Windows env-var `PATH` (registry-level, user scope) and removes the Program
> Files dotnet path from the system PATH, **in addition to** writing the shell profile.
>
> Later, a system .NET SDK or runtime installer runs (for example, via a Visual Studio
> update). Its installer adds the Program Files dotnet path back to the system PATH,
> which means the effective PATH will list the Program Files dotnet path, and it will
> take precedence over the user dotnet install.
>
> The fix: rerun `dotnetup env set all`.  This will once again remove the Program Files
> dotnet path from the system PATH. Because `env set` is idempotent, this works whether
> the mode was already applied or not.

The mode the user picked is stored in `dotnetup.config.json` and is shown by
`dotnetup env show`, so the user doesn't need to remember it.

## Naming the noun: `env`, not `path`

The command doesn't only manage `PATH` — it also writes `DOTNET_ROOT` and (in the
"all" mode) edits the Windows env-var registry. Even the "shell profile" mode is
fundamentally a script that exports env vars at shell startup. **Everything we touch
is environment-variable wiring.**

CLI prior art reads `env` unambiguously as "environment variables": `printenv`, `env`,
`Get-ChildItem env:`, `setx`, `direnv`. The "deployment environment" sense doesn't
show up at this layer.

```
dotnetup env set <mode>
dotnetup env show
```

Reads naturally: "configure how dotnetup wires up the environment". Covers PATH,
DOTNET_ROOT, and the profile script in one word.

Rejected nouns: `path` (too narrow), `vars` (too generic), `integration`/`shell`
(narrower than what we do), `setup` (sounds like an installer).

## Naming the modes: `None / Shell / All`

```
DotnetAccessMode.None   // dotnetup doesn't wire anything; user invokes via `dotnetup dotnet`
DotnetAccessMode.Shell  // write to the shell's profile file only
DotnetAccessMode.All    // shell profile + Windows user env-var PATH/DOTNET_ROOT
```

Why these names:

- `None` — unambiguous; matches the `dotnetup defaultinstall none` reading.
- `Shell` — more recognizable than `Profile`. PowerShell users know `$PROFILE` but
  Unix newcomers may not parse "profile" without explanation. "Shell" is the
  user-visible thing it affects.
- `All` — captures "all shells and apps see it", parallel to `None`. Avoids:
  - `System` — collides with "system .NET install" (admin MSI in `C:\Program Files`).
  - `Global` — collides with `dotnet tool install --global`.
  - `Env` — would be `env set env` (awful given the chosen noun).
  - `Everywhere` — verbose.

Help text writes itself:

- `none` — Don't modify your environment; use `dotnetup dotnet` to invoke.
- `shell` — Wire dotnetup into your shell's profile file.
- `all` — Wire dotnetup into your shell profile **and** your user-level
  PATH/DOTNET_ROOT so cmd.exe, IDEs, and shortcuts also see it.

### Renamed `DotnetAccessMode` enum

```
DotnetAccessMode.None  = old DotnetupDotnet       // no PATH wiring
DotnetAccessMode.Shell = old ShellProfile         // shell profile file only
DotnetAccessMode.All   = old FullPathReplacement  // env-var PATH + shell profile
```

No backwards-compat: no config files have shipped. Update the JSON serialization to
write the new names.

## `dotnetup` on PATH: an orthogonal setting

> **Status: design settled.** This section was added after the rest of the document; its
> open questions (below) have all been resolved and it's ready to implement alongside the
> `env` command.

### The problem this solves

The `env` mode above governs how the **managed dotnet** is surfaced. But there is a
second, separate concern tangled into the same mechanism today: whether **`dotnetup`
itself** is on `PATH`.

In `Shell` / `All` mode the managed profile block happens to prepend the dotnetup
directory to `PATH` as a side effect (the `includeDotnet=true` path also adds
`dotnetupDir`). In `None` mode the block is removed entirely — which also drops
`dotnetup` from `PATH`. That is incoherent precisely for `None`, whose whole point is
"invoke .NET via `dotnetup dotnet`": that workflow is impossible unless `dotnetup` is
discoverable on `PATH`.

Today `dotnetup`-on-`PATH` is also partly the responsibility of the standalone install
script (it prints/sets a `PATH` entry for the dotnetup dir). We don't want to rely on
the install script for this. **The install script should only download `dotnetup`;
running `dotnetup` should manage its own `PATH` entry.** That makes dotnetup-on-PATH a
first-class, consistently-managed setting rather than an install-time side effect.

### The model: two orthogonal settings, one derived mechanism

| Setting | Values | Meaning |
| --- | --- | --- |
| **`accessMode`** (dotnet access) | `none` / `shell` / `all` | How the managed **dotnet** is surfaced (the modes above). |
| **`dotnetupOnPath`** (dotnetup discoverability) | `true` / `false` | Whether the **dotnetup** directory is on `PATH` so `dotnetup` can be invoked. |

The two are independent axes. dotnetup-on-PATH sits underneath all three `accessMode` values
and, while installed, is normally on.

**Whether the profile contains a dotnetup-managed section at all is *derived*, not a
third user-facing knob.** The managed block is a single call to `dotnetup env script`
(e.g. `eval "$(dotnetup env script …)"` on Unix; the PowerShell equivalent on Windows).
There are not separate "dotnet" and "dotnetup" lines — the **arguments passed to
`env script`** determine what the generated script wires, and the two settings map onto
those arguments:

- The generated script **wires dotnet** (`DOTNET_ROOT` + the managed dotnet on `PATH`)
  iff `accessMode ∈ {shell, all}`.
- The generated script **adds the dotnetup directory to `PATH`** iff
  `dotnetupOnPath = true` (Unix).
- If neither applies (`accessMode = none` **and** `dotnetupOnPath = false`) → there is nothing
  for the script to do, so the managed block is removed entirely.

These two aspects are the existing `includeDotnet` and dotnetup-dir inputs to
`GenerateEnvScript`. The `env script` surface expresses them as two selection flags,
`--dotnet` and `--dotnetup` (see [`env script` flag surface](#env-script-flag-surface)).
The applier bakes the explicit flags into the profile block at apply time, so the
persisted block is deterministic and never depends on the command's default.

Exposing "touch the profile at all" as a separate setting would add a knob that is only
meaningful on Windows (where dotnetup-on-PATH uses the user `PATH` env var and needs no
profile), so we keep it derived to avoid complexity.

### Mechanism per OS

- **Unix:** the managed block is one `env script` call whose arguments select whether it
  wires dotnet and whether it adds the dotnetup directory. `dotnetupOnPath = false` drops
  the dotnetup-dir argument; the block is removed only when the script would wire nothing.
- **Windows:** dotnetup-on-PATH is written to the **user `PATH` env var** (so cmd,
  PowerShell, and GUI-launched apps all see it — not only PowerShell). For simplicity
  the PowerShell profile block **also** includes the dotnetup directory (driven by the
  same `env script` arguments as Unix), even though that is redundant with the user
  `PATH` entry inside PowerShell. The duplication is harmless — both entries point at the
  same directory — and it keeps profile generation identical across operating systems.
  Consequences: clearing `dotnetupOnPath` must remove it from **both** places on Windows
  (the profile's `env script` arguments and the user `PATH` entry), and `env show` drift
  detection treats the **user `PATH` env var as authoritative** for dotnetup-on-PATH on
  Windows (the profile copy is just a convenience).

Because dotnetup has no `uninstall` command, **`dotnetupOnPath = false` is the way to
remove `dotnetup` from `PATH`** — it removes the Unix profile line and the Windows user
`PATH` entry.

### Config schema

```jsonc
{
  "schemaVersion": "1",
  "accessMode": "shell",   // renamed from "pathPreference"
  "dotnetupOnPath": true   // new; defaults to true when absent
}
```

No `schemaVersion` bump. Builds have shipped **internally** with the original shape
(`pathPreference` + the pre-rename enum spellings `DotnetupDotnet` / `ShellProfile` /
`FullPathReplacement`), so existing internal configs must keep working. Rather than a
versioned migration, the reader tolerates the legacy shape (a read-compatibility shim):

- Accept the legacy `pathPreference` property name as an alias for `accessMode` (prefer `accessMode`
  when both are present).
- Accept the legacy enum spellings and map them: `DotnetupDotnet → none`,
  `ShellProfile → shell`, `FullPathReplacement → all`.
- A missing `dotnetupOnPath` defaults to `true`.

This preserves internal users' chosen mode across the upgrade and avoids the spurious
"config appears to be corrupted" warning that a renamed enum value would otherwise
trigger. The next write rewrites the file in the new shape, so the legacy form naturally
ages out. Because the shim handles continuity, `schemaVersion` stays `"1"`; a bump would
only be warranted if we chose explicit versioned migration instead.

### Command UI (recommended)

Keep everything under the `env` noun. Dotnet access stays the positional argument (the
primary axis); dotnetup-on-PATH is an option. A bare `env set` re-syncs the stored
config.

```
dotnetup env set <none|shell|all>                     # set dotnet access, leave dotnetup-on-PATH as-is
dotnetup env set --dotnetup-on-path <true|false>      # change only dotnetup-on-PATH
dotnetup env set <mode> --dotnetup-on-path <true|false> # set both at once
dotnetup env set                                      # no args: re-apply stored config (fix drift)
dotnetup env clear                                    # fully unwire: == env set none --dotnetup-on-path false
dotnetup env show                                     # report both axes + drift
dotnetup env script                                   # unchanged
```

Examples:

```
dotnetup env set shell                       # dotnet on shell PATH; dotnetup stays on PATH
dotnetup env set none                        # stop exposing dotnet; keep `dotnetup` runnable
dotnetup env set --dotnetup-on-path false    # remove dotnetup from PATH (the "unset", any mode)
dotnetup env set none --dotnetup-on-path false # fully unwire everything
dotnetup env clear                           # same as the line above; the "undo everything" verb
```

`env clear` is the one verb that removes *everything* dotnetup wired into the
environment — it sets `env = none` **and** `dotnetupOnPath = false`, leaving no managed
profile block and no dotnetup `PATH` entry. It is the closest thing to an "env uninstall"
given dotnetup has no `uninstall` command. (This is distinct from the rejected `env reset`
shortcut discussed below, which would only have aliased `env set none` and left dotnetup
on `PATH`.)

`env show`, in sync:

```
dotnetup environment:
  dotnet access    shell      managed dotnet is added to your shell profile
  dotnetup on PATH   yes        via ~/.bashrc

In sync.
```

`env show`, with drift:

```
dotnetup environment:
  dotnet access    shell
  dotnetup on PATH   yes
  ⚠ drift:
    - shell profile is missing the managed dotnet lines
  Run 'dotnetup env set' to re-sync.
```

**Alternative UI considered:** two sibling verbs — `env set <none|shell|all>` for
dotnet and `env dotnetup-path <true|false>` for dotnetup. Cleaner orthogonality, but two
verbs to discover and a less-obvious bare re-sync. Current lean is the single-`set`
form above.

### `env script` flag surface

`env script` keeps its two stable auxiliary options — `--shell <name>` (which provider)
and `--dotnet-install-path | -d <path>` (which install to wire) — and gains two
**selection flags** for *what* the generated script wires:

| Invocation | Generated script wires |
| --- | --- |
| `env script` (no selection) | both dotnet + dotnetup (default; matches today, back-compat) |
| `env script --dotnet --dotnetup` | both (explicit — what the applier bakes into profiles) |
| `env script --dotnetup` | dotnetup only |
| `env script --dotnet` | dotnet only |
| `env script --dotnetup-only` | dotnetup only (hidden legacy alias for `--dotnetup`) |

Semantics are a **selection set**: with no selection flag the script wires both (the
convenient, backwards-compatible default a human gets from typing `env script`); passing
any of `--dotnet` / `--dotnetup` emits **only** the listed parts. Help text states this
explicitly so `--dotnet` isn't misread as additive: *"By default the script wires both
dotnet and dotnetup. Pass `--dotnet` and/or `--dotnetup` to emit only the parts you
list."*

- **Profiles are always explicit.** The applier bakes `--dotnet --dotnetup`,
  `--dotnetup`, or `--dotnet` (never relying on the default), so a persisted block is
  deterministic and unaffected by any future change to the default.
- **`--dotnetup-only` stays as a hidden legacy alias** for `--dotnetup`, so managed
  blocks written by older dotnetup versions (which call the hidden `print-env-script`
  alias with `--dotnetup-only`) keep working through the compat window, then it's
  removed. Combining `--dotnetup-only` with `--dotnet`/`--dotnetup` is contradictory and
  is rejected with an error; in practice only old profiles emit it and never in
  combination.
- "Neither" is not an expressible state — when a mode would wire nothing, the applier
  removes the managed block instead of emitting an empty `env script` call.

### Composition table (what actually gets written)

| `env` | `dotnetupOnPath` | Unix profile (`env script` wires) | Windows user `PATH` | Windows profile (`env script` wires) |
| --- | --- | --- | --- | --- |
| none | true | dotnetup only | dotnetup | dotnetup only |
| none | false | (block removed) | — | (block removed) |
| shell | true | dotnetup + dotnet | dotnetup | dotnetup + dotnet |
| shell | false | dotnet only | — | dotnet only |
| all | true | dotnetup + dotnet | dotnetup + dotnet env vars | dotnetup + dotnet |
| all | false | n/a (`all` is Windows-only) | dotnet env vars | dotnet only |

The Unix and Windows profile columns are driven by the same `env script` arguments
(identical wherever both platforms support the mode; `all` is Windows-only). The Windows
user `PATH` column is the extra Windows-only piece (and, for `all`, is also where the
dotnet env-var wiring lives).

### Init walkthrough

- The existing None/Shell/All prompt sets `env`.
- `dotnetupOnPath` defaults to `true` with **no separate prompt** — you just ran
  `dotnetup`, so keeping it runnable is the obvious default and a prompt adds friction.
- Post-init guidance mentions it (e.g. "`dotnetup` has been added to your PATH; change
  this later with `dotnetup env set --dotnetup-on-path false`").
- Everything stays adjustable afterward via the commands above.

### Open questions

1. **UI shape** *(resolved)*: a flag on `env set` (single-verb model), not a separate
   `env dotnetup-path` verb. dotnet access stays the positional; dotnetup-on-PATH is the
   `--dotnetup-on-path` option.
2. **Option spelling** *(resolved)*: value form `--dotnetup-on-path <true|false>` (explicit,
   script-friendly, and absent = leave unchanged), not a `--no-` flag pair. Accepted as
   the best option despite being slightly verbose — it's an uncommon operation, and the
   `dotnetup-` prefix usefully signals it's about dotnetup itself rather than dotnet.
   (`--self-on-path <true|false>` was considered as a terser form; rejected for being more
   jargon-y.)
3. **Config rename** *(resolved)*: rename `pathPreference` → `accessMode`, with the
   read-compatibility shim above to preserve internal users' configs. The legacy shim is
   kept **permanently for now** (not time-boxed to one release); we can revisit removing
   it later if it ever becomes a maintenance burden.
4. **Init prompt** *(resolved)*: keep the silent default `dotnetupOnPath = true`. We are
   **not** touching the init walkthrough UI in this PR; a dedicated prompt can be added
   later (likely will be), but it's out of scope here.
5. **`env script` flag surface** *(resolved — see [`env script` flag surface](#env-script-flag-surface))*:
   two selection flags `--dotnet` / `--dotnetup`; no selection = both (back-compat
   default); profiles bake explicit flags; `--dotnetup-only` retained as a hidden legacy
   alias for `--dotnetup` during the `print-env-script` compat window.

## Command shape: alternatives considered

Why not the obvious extensions:

- `env apply` (re-apply current config without changing it) — equivalent to
  rerunning `env set <same-mode>`. `env show` tells you the mode if you've
  forgotten. Skipped to keep the verb surface minimal.
- `env reset` — one-token shortcut for `env set none`. Skipped for the same reason.
  Note this is *not* the same as `env clear` (which is kept): `reset` would have left
  dotnetup on `PATH`, whereas `env clear` also clears `dotnetupOnPath`, fully unwiring
  the environment.

Either can be added later without breaking the v1 surface.

Alternative command shapes considered and rejected:

| Syntax                                                          | Why not                                                            |
| --------------------------------------------------------------- | ------------------------------------------------------------------ |
| `dotnetup configure-env [<mode>]`                               | Flat, no verb family; verb-led breaks noun-verb convention.        |
| `dotnetup config set env <mode>` / `dotnetup config apply`      | git-style; lower-level feel; not the dotnetup CLI's style.         |
| `dotnetup enable <mode>` / `disable` / `refresh` / `status`     | `enable`/`disable` overload heavily with channel install verbs.    |
| `dotnetup path set <mode>` / `path show`                        | `path` is narrower than what we do (DOTNET_ROOT, profile script).  |

## How this interacts with #53838 and #53837

- #53838 turns the init walkthrough into:
  - Q1 ("modify shell profile?") → answer Yes/No
  - Q2 ("replace the env-var PATH?") → only shown on Windows if Q1=Yes, only if a
    system .NET install exists
  - Mapping: (No, _) → `None`. (Yes, No) → `Shell`. (Yes, Yes) → `All`.
- #53837 turns the init walkthrough into a summary-first flow. The "PATH Usage"
  line in the summary reflects the same `DotnetAccessMode`. The customization path
  drops into the same prompts as #53838.

In both cases the underlying "given a `DotnetAccessMode`, apply it" logic is the
shared helper this command uses (`DotnetAccessModeApplier.Apply`). Init and `env set`
both go through it. The walkthrough refactors then become mechanical edits to the
question-asking layer, not the apply layer.

## Implementation outline

1. **Rename `DotnetAccessMode` enum** to `None / Shell / All`. Update all callsites +
   JSON serialization. Single commit.
2. **Add `DotnetAccessModeApplier`** in `dotnetup.Library` — single static `Apply` entry
   point that takes a `DotnetAccessMode` and the necessary inputs, performs the
   unwind + apply + config write, and emits user-facing messages via `Spectre.Console`.
   - Unwinding `All` reuses existing `InstallRootManager.GetAdminInstallRootChanges`
     + `ApplyAdminInstallRoot`, which performs the exact inverse of
     `GetUserInstallRootChanges` + `ApplyUserInstallRoot` (re-adds Program Files
     dotnet to system PATH via elevation, removes user dotnet from user PATH,
     unsets user-scope `DOTNET_ROOT`). No "original PATH" snapshot required —
     each `NeedsX` flag guards against no-op cases.
   - `All → Shell` leaves the profile script in place, which still exports the
     user dotnet PATH entry and `DOTNET_ROOT` at shell startup. Only the duplicate
     registry copy gets removed.
   - Edge case: if no admin install exists (`GetProgramFilesDotnetPaths()` empty),
     the unwind has nothing to add back to system PATH and cmd.exe is left with
     no dotnet at all. Surface a warning on the mode switch.
3. **Add new `env` parent command** with `set` / `show` / `script` subcommands under
   `src/Installer/dotnetup.Library/Commands/Env/`. Wire into `Parser.cs`.
   - `env show` displays the current mode and, if applied state has drifted from
     the configured mode, reports the drift. For `All`, drift = user dotnet not at
     the front of user PATH, OR Program Files dotnet present in system PATH, OR
     user-scope `DOTNET_ROOT` ≠ user dotnet path. For `Shell`, drift = managed
     block missing from the configured shell's profile file. All checks are
     read-only (no elevation needed).
   - `env script` is the existing `EnvScriptCommand` (renamed from `PrintEnvScriptCommand`). The
     top-level `dotnetup print-env-script` stays as a hidden alias that delegates
     to `env script`, so managed profile blocks written by earlier dotnetup
     versions keep working. Remove the alias one release later. Newly-written
     managed blocks invoke `env script`.
4. **Delete `DefaultInstallCommand`** + parser + tests; remove from `Parser.cs`. No
   alias.
5. **Refactor `InitWorkflows`** finalize step to call `DotnetAccessModeApplier.Apply`
   instead of its own inline logic.
6. **Tests**:
   - `DotnetAccessModeApplier` per-mode + mode-switch unwind (`All → Shell` correctly
     clears env vars; `Shell → None` removes the managed profile block) + idempotency
     + re-sync.
   - Parser tests for each subcommand.
   - `env show` drift detection: pre-modify the env-var PATH out-of-band, confirm
     `env show` reports the drift.
   - Integration: `env set shell` then `env set none` leaves no managed block in
     any profile file.
7. **Docs**: update `--help` text. Add release-notes entry for the rename. No
   manpage changes (those are generated from docs).

## Risks

- Walkthrough refactor (#53838 / #53837) may want to call the applier with
  `DotnetAccessMode` *plus* "and also do the channel install in the same flow". Make
  sure the applier doesn't try to do install work — keep it strictly about env-mode
  application.
- Renaming the enum touches every callsite. Mechanical and easy with rename refactor,
  but the JSON property name change means any config files in the wild become
  unreadable. Currently zero have shipped, so OK.
