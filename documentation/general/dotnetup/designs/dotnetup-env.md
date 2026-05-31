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
- It doesn't persist `PathPreference` in `dotnetup.config.json`, even though the `init`
  walkthrough writes that exact preference (see `DotnetupConfig.PathPreference`).
- It pre-dates the PowerShell-profile-on-Windows work, so it doesn't know that
  shell-profile mode is now a valid Windows option.

## Goals

1. Replace `defaultinstall` with a clearly-named command whose argument values map 1:1
   to the renamed `PathPreference`.
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
backed by a renamed three-value `PathPreference` enum:

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

`PathPreference` enum renames 1:1: `None` / `Shell` / `All`. JSON serialization
follows.

Apply logic lives in a shared `PathPreferenceApplier` helper so that `dotnetup init`
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
PathPreference.None   // dotnetup doesn't wire anything; user invokes via `dotnetup dotnet`
PathPreference.Shell  // write to the shell's profile file only
PathPreference.All    // shell profile + Windows user env-var PATH/DOTNET_ROOT
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

### Renamed `PathPreference` enum

```
PathPreference.None  = old DotnetupDotnet       // no PATH wiring
PathPreference.Shell = old ShellProfile         // shell profile file only
PathPreference.All   = old FullPathReplacement  // env-var PATH + shell profile
```

No backwards-compat: no config files have shipped. Update the JSON serialization to
write the new names.

## Command shape: alternatives considered

Why not the obvious extensions:

- `env apply` (re-apply current config without changing it) — equivalent to
  rerunning `env set <same-mode>`. `env show` tells you the mode if you've
  forgotten. Skipped to keep the verb surface minimal.
- `env reset` — one-token shortcut for `env set none`. Skipped for the same reason.

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
  line in the summary reflects the same `PathPreference`. The customization path
  drops into the same prompts as #53838.

In both cases the underlying "given a `PathPreference`, apply it" logic is the
shared helper this command uses (`PathPreferenceApplier.Apply`). Init and `env set`
both go through it. The walkthrough refactors then become mechanical edits to the
question-asking layer, not the apply layer.

## Implementation outline

1. **Rename `PathPreference` enum** to `None / Shell / All`. Update all callsites +
   JSON serialization. Single commit.
2. **Add `PathPreferenceApplier`** in `dotnetup.Library` — single static `Apply` entry
   point that takes a `PathPreference` and the necessary inputs, performs the
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
   - `env script` is the existing `PrintEnvScriptCommand` moved under `env`. The
     top-level `dotnetup print-env-script` stays as a hidden alias that delegates
     to `env script`, so managed profile blocks written by earlier dotnetup
     versions keep working. Remove the alias one release later. Newly-written
     managed blocks invoke `env script`.
4. **Delete `DefaultInstallCommand`** + parser + tests; remove from `Parser.cs`. No
   alias.
5. **Refactor `InitWorkflows`** finalize step to call `PathPreferenceApplier.Apply`
   instead of its own inline logic.
6. **Tests**:
   - `PathPreferenceApplier` per-mode + mode-switch unwind (`All → Shell` correctly
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
  `PathPreference` *plus* "and also do the channel install in the same flow". Make
  sure the applier doesn't try to do install work — keep it strictly about env-mode
  application.
- Renaming the enum touches every callsite. Mechanical and easy with rename refactor,
  but the JSON property name change means any config files in the wild become
  unreadable. Currently zero have shipped, so OK.
