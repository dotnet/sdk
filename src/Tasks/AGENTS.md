# Tasks Agent Instructions

Guidance for changes under `src/Tasks` (MSBuild tasks, targets, and SDK build messages).

## Where things live

`Microsoft.NET.Build.Tasks` and `Microsoft.NET.Build.Extensions.Tasks` build for
both `net472` and current .NET, so code added to them must stay
`net472`-compatible (no .NET-only APIs). `Common` is linked shared source
compiled into each, so it inherits that constraint; `sdk-tasks` is
current-.NET-only:

| Project | Role |
|---------|------|
| `Microsoft.NET.Build.Tasks` | The core `Microsoft.NET.Sdk` task assembly **and** the targets that compose `dotnet build`. Ships in the SDK. |
| `Common` | Common shared source **linked into** both task assemblies — not its own package. |
| `Microsoft.NET.Build.Extensions.Tasks` | Tasks/targets for desktop/.NET Framework projects built **outside** the SDK. |
| `sdk-tasks` | Build-time-only tasks for **this repo's own** build/packaging — never shipped to users. |

### Inside `Microsoft.NET.Build.Tasks`

- Root `*.cs` — the MSBuild `Task` classes.
- `targets/` — the shipping `.targets`/`.props` that drive a build;
  `Microsoft.NET.Sdk.props` / `Microsoft.NET.Sdk.targets` are the top-level entry
  points, and `Microsoft.NET.Sdk.Common.targets` registers the diagnostic tasks.
- `sdk/` — `Sdk.props` / `Sdk.targets`, the entry points when the SDK is referenced
  via the `<Sdk>` attribute.
- `FrameworkPackages/` — per-TFM runtime framework version data.

## Multithreaded tasks and caches

Apply `[MSBuildMultiThreadableTask]` to each migrated concrete task. Tasks that resolve
paths or access the environment also implement `IMultiThreadableTask`, defaulting to
`TaskEnvironment.Fallback`. Resolve filesystem operands using the injected context
without changing output paths, diagnostics, or invalid-input behavior. Keep original
inputs through shared helpers when they govern validation or control flow; audit every
filesystem use in those helpers.
[`GetAbsolutePathPreservingInvalidArguments`](Common/TaskEnvironmentExtensions.cs)
retains BCL argument validation, including Framework hosts' legacy-path switch, without
letting accepted whitespace operands resolve against a shared process CWD.
Choose the actual host runtime, not the task's target TFM, when applying that policy:
a Framework task assembly can also execute in .NET MSBuild.
On Windows .NET hosts, only ASCII spaces trigger empty-path rejection; valid
Unicode-whitespace file names and parent directories (such as U+00A0) still need
project-relative resolution.

For expensive shared initialization, use
[`TaskObjectCache`](Microsoft.NET.Build.Tasks/TaskObjectCache.cs), as
[`LockFileCache`](Microsoft.NET.Build.Tasks/LockFileCache.cs) and
[`ResolveTargetingPackAssets`](Microsoft.NET.Build.Tasks/ResolveTargetingPackAssets.cs)
do. Keep cache keys complete and preserve the intended registered-object lifetime.
The helper scopes entries by their owning generic type to avoid collisions between SDK
assemblies, coordinates initialization per retained engine-owned entry, retries thrown
initialization failures, and does not retain task factories. Cached values must support
concurrent consumers and must not require engine-managed disposal. Do not introduce a
process-wide dictionary of per-project locks.

## Build diagnostics (NETSDK errors / warnings / info)

These are conventions that aren't visible from the code alone — getting them wrong
breaks localization or silently collides with another PR.

- **Diagnostics are raised from targets, never as literal text.** Use
  `<NETSdkError/>`, `<NETSdkWarning/>`, or `<NETSdkInformation/>` (registered in
  `Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.Common.targets`) and reference
  the message **by `ResourceName`** from the shared `Common/Resources/Strings.resx`:

  ```xml
  <NETSdkError Condition="'$(TargetFramework)' == ''"
               ResourceName="TargetFrameworkEmpty"
               FormatArguments="$([MSBuild]::Escape('$(SomeValue)'))" />
  ```

- **The NETSDK code lives in the value, not in metadata.** In `Strings.resx` the
  `<value>` begins with the code (`NETSDK1238: The property '{0}' ...`), and the
  `<comment>` carries the localization directives — `{StrBegins="NETSDK1238: "}` plus
  `{Locked="{0}"}` per placeholder (or `{Locked="--option"}` for literal flags).
- **Message entries are append-only.** Add new strings at the **end** of the resx with
  the **next available** NETSDK number, and update the trailing
  `<!-- The latest message added is <Name>. -->` guard comment — it exists so two PRs
  adding a message conflict in git instead of silently reusing a code. (The root
  instructions carry the one-line version of this rule.)
- **`.xlf` files are generated**, never hand-edited — regenerate via `/t:UpdateXlf`.

Diagnostics are covered by tests under `test/Microsoft.NET.Build.Tests/`.
