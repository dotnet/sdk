# Project-local SDK setup

## Scenario

Some repositories want a self-contained SDK setup that can be bootstrapped from a clean checkout without asking every contributor to install a matching SDK into their user-wide dotnetup root first. The expected result is:

- A .NET SDK installed under the repository, normally in `.dotnet`.
- A `global.json` that uses .NET 10 SDK `paths` so normal `dotnet` commands resolve the repository SDK first and then fall back to the host SDK.
- A `.gitignore` entry that prevents the SDK archive contents from being committed.
- No PATH, profile, DOTNET_ROOT, or default-install-root mutation.

This scenario still requires a .NET 10 or later host on PATH because the host is the component that understands `global.json` `sdk.paths`.

## Proposed command shape

```bash
dotnetup sdk install 10.0.100 --local
dotnetup install 10.0.100 --local
```

`--local` means "configure this repository to use an SDK installed next to its `global.json`." It is intentionally limited to SDK installs because `global.json` SDK resolution does not select runtimes.

## Behavior

1. Find the nearest `global.json` in the current directory or a parent directory.
2. If no `global.json` exists, create one in the current directory.
3. Install the resolved SDK into `.dotnet` next to that `global.json`.
4. Merge `sdk.paths` into `global.json` as `[".dotnet", "$host$"]`.
5. Preserve unrelated top-level `global.json` sections such as `tools` and `msbuild-sdks`.
6. Add `.dotnet/` to the adjacent `.gitignore` idempotently.
7. Record the install spec as global.json-sourced so update/uninstall logic can reason about it.

Exact version requests write `rollForward: "disable"`. Channel requests write a roll-forward policy matching the requested channel scope and store the resolved SDK version.

## API alternatives considered

| Option | Pros | Cons |
| --- | --- | --- |
| `--local` | Short; common CLI term for "put it in this repo"; does not overload existing `--install-path`; maps directly to the end result. | "Local" can also mean user-local vs system-wide, archive vs system installer, or PATH visibility. Needs documentation to define it as project-local SDK setup. |
| `--project-local` | More explicit than `--local`. | Longer and less discoverable; still requires explaining global.json and `.dotnet` behavior. |
| `--install-path .dotnet --update-global-json` | Reuses existing options. | Easy to miss required `sdk.paths`, `.gitignore`, host validation, exact-version pinning, and update-tracking semantics; more chances for partially configured repos. |
| `--install-path here` or another sentinel value | Reuses the existing install-path concept. | Makes `--install-path` perform more than path selection; special string values are harder to compose with explicit paths and require separate documentation anyway. |
| Keep all SDKs in the dotnetup root and only update global.json | Preserves the dotnetup-managed hive model. | Does not satisfy repositories that want checkout-local SDK contents and does not provide an offline/team bootstrap artifact in the repo root. |

## Why not only `--install-path`

`--install-path` answers "where should the archive be extracted?" Project-local SDK setup is a higher-level workflow:

- It edits `global.json`.
- It depends on a .NET 10 host capability.
- It chooses `.dotnet` relative to the project boundary, not necessarily the shell's current directory.
- It updates `.gitignore`.
- It should avoid PATH/profile/default-root mutation even though normal installs may perform onboarding steps.

Keeping this as a dedicated switch makes the user's intent explicit and lets dotnetup validate the whole workflow rather than accepting a path and leaving the repository half-configured.

## Install root tradeoff

The existing dotnetup model generally manages SDKs in the dotnetup install root and tracks project demand through global.json-derived install specs. Project-local SDK setup intentionally creates a second, repository-owned root. That is useful when the repository wants its bootstrap command to leave the required SDK inside the checkout.

This does create tradeoffs:

- dotnetup update and uninstall need to retain enough manifest metadata to distinguish project-local roots from the default dotnetup root.
- Garbage collection must not remove a repo-local SDK that is still referenced by that repo's global.json.
- Users may have multiple copies of the same SDK across repositories.

If the product direction is to avoid repository-owned SDK roots, the same global.json `paths` setup could point at a dotnetup-managed root instead. That would make this feature more of a "configure global.json for dotnetup" workflow than a "project-local SDK" workflow, and the command name should reflect that difference.

