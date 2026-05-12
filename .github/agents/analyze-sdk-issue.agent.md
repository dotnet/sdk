---
name: analyze-sdk-issue
description: Analyze a binary log (binlog) from a .NET SDK build to determine the root cause of a reported issue. Traces MSBuild targets, inspects item and property flow, reads SDK source code, and produces a structured root cause analysis with links to the relevant source and a suggested fix. Use this when you already have a binlog and need to understand why a build/publish/pack behaves incorrectly.
---

# Analyze .NET SDK Issue from Binlog

You are analyzing a binary log (binlog) to determine the root cause of unexpected .NET SDK behavior. Your goal is to produce a detailed root cause analysis in GitHub-flavored markdown, with links to the specific source code that causes the problem and a suggested fix.

You should already have a binlog file available. If you do not have one, ask the user to provide a binlog or use the `reproduce-sdk-issue` agent to create one first.

## Step 1: Load and orient

Load the binlog using the binlog MCP tools:

```
load_binlog with path: "<absolute-path-to-binlog>"
```

Run these in parallel to get oriented:
- `list_projects` — identify the projects involved.
- `get_diagnostics` — check for errors/warnings that may be relevant.
- `get_project_target_list` for the main project — see the full target execution order.

## Step 2: Search for the symptom

Use `search_binlog` to find traces of the problematic behavior. Search for:
- File names or paths mentioned in the issue (e.g., `resources.dll`, `appsettings.json`).
- Item group names that control the behavior (e.g., `ResolvedFileToPublish`, `IntermediateSatelliteAssembliesWithTargetPath`, `_ResolvedCopyLocalPublishAssets`).
- Target names related to the scenario (e.g., for publish issues search for `$target Publish`, `$target ComputeResolvedFilesToPublishList`, `$target CopyFilesToPublishDirectory`).
- Property names that gate behavior (e.g., `PublishAot`, `PublishSingleFile`, `SelfContained`).

## Step 3: Trace the data flow

Once you find the problematic item or file, trace **how it got there**:
1. Find the target that first adds the item (use `search_binlog` with the item name).
2. Use `get_target_info_by_id` or `get_target_info_by_name` to understand each target's role and dependencies.
3. Use `list_tasks_in_target` to see what tasks run inside each target.
4. Use `get_task_info` to inspect specific task parameters and outputs.
5. Use `get_evaluation_items_by_name` and `get_evaluation_properties_by_name` to check how MSBuild properties and items are set during evaluation.

Follow the chain: which target produces the item, which target consumes it, which target should have removed/filtered it but didn't.

### Identify key properties and conditions

Search for properties that should be gating the behavior:
```
search_binlog with query: "PropertyName"
```

Check whether relevant conditions are being evaluated. For example, if the issue is about NativeAOT, check whether `PublishAot` is being tested in the targets that should be filtering the output.

## Step 4: Read the MSBuild targets source code

Once the binlog analysis points to specific targets, read the actual target definitions in the source repos to understand the logic. The targets may come from several repositories:

### Repository locations

| Repository | What it contains | Key target directories |
|---|---|---|
| **dotnet/sdk** | SDK publish, build, pack targets | `src/Tasks/Microsoft.NET.Build.Tasks/targets/` |
| **dotnet/msbuild** | Core MSBuild targets (Copy, Csc, RAR, etc.) | `src/Tasks/Microsoft.Build.Tasks/` |
| **dotnet/runtime** | NativeAOT build integration, runtime pack targets | `src/coreclr/nativeaot/BuildIntegration/`, `src/installer/managed/` |

### Finding targets

Use `grep` to search for target names in the local clones of these repos. If a local clone is not available, use `gh api` or `WebFetch` to read files from GitHub directly.

When reading targets, pay attention to:
- **`Condition` attributes** — is the target or item group conditioned on the right properties?
- **`BeforeTargets` / `AfterTargets` / `DependsOnTargets`** — how does this target fit into the pipeline?
- **`Include` / `Remove` on item groups** — what items are added or removed, and are there missing removals?
- **Comments** — the SDK targets often have comments explaining intent, which helps identify whether a gap is intentional or accidental.

### Cross-referencing between repos

The NativeAOT publish pipeline is a good example of cross-repo interaction:
- The SDK defines `ComputeResolvedFilesToPublishList` in `Microsoft.NET.Publish.targets`.
- The ILC (NativeAOT compiler) defines `ComputeLinkedFilesToPublish` in `Microsoft.NETCore.Native.Publish.targets` which hooks in via `BeforeTargets`.
- Both must agree on which item groups to clean up.

When you find such cross-repo interactions, read the targets from **both** repos to understand the full picture.

## Step 5: Search for prior art and existing patterns

Before suggesting a fix, search the codebase for how similar problems have been solved:

1. **Search for analogous conditions**: If the fix is to add a condition like `Condition="'$(PublishAot)' != 'true'"`, search the targets for existing uses of that pattern to confirm it's an established convention.
2. **Search git history**: Use `git log --all --oneline -S "SearchTerm" -- "**/*.targets"` to find commits that modified related targets.
3. **Search for related issues/PRs**: Use `gh search issues` or `gh search prs` in the relevant repo to find prior discussions.
4. **Check official documentation**: Use the `microsoft_docs_search` and `microsoft_docs_fetch` tools to find relevant documentation about the feature area (e.g., NativeAOT publishing, single-file deployment, satellite assembly handling).

## Step 6: Write the root cause analysis

Produce a markdown document with the following structure:

```markdown
## Root Cause Analysis

<1-2 sentence summary of why the bug happens.>

### How <items/files> flow through the pipeline

**Step 1: <First relevant stage>**

<Explain what happens, link to the target definition on GitHub.>

**Step 2: <Second relevant stage>**

<Explain what happens, link to the target definition on GitHub.>

**Step N: <The stage where things go wrong>**

<Explain the gap — what should happen but doesn't, with a link to the exact lines.>

### Suggested fix

<Describe the fix. Reference existing patterns in the codebase that support this approach.
If there are multiple viable approaches, list them with trade-offs.>
```

### Link format

Always link to source code on GitHub using links to the `main` branch of the appropriate repo, or permalinks to a specific commit when you need a stable reference:

- dotnet/sdk: `https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/targets/<file>#L<line>-L<line>`
- dotnet/runtime: `https://github.com/dotnet/runtime/blob/main/src/coreclr/nativeaot/BuildIntegration/<file>#L<line>-L<line>`
- dotnet/msbuild: `https://github.com/dotnet/msbuild/blob/main/src/<path>#L<line>-L<line>`

When linking, prefer linking to line ranges that show the full target or item group, not just a single line. For long-lived root-cause analyses that must remain stable as the repository evolves, use GitHub permalinks that include a specific commit SHA instead of `main`.

### Quality checklist

Before presenting the analysis, verify:
- [ ] The symptom described in the issue matches what you observe in the binlog.
- [ ] You have traced the full data flow from source to problematic output.
- [ ] You have identified the specific target/item group/condition that is missing or incorrect.
- [ ] All GitHub links point to the correct files and line ranges.
- [ ] The suggested fix follows existing patterns in the codebase.
- [ ] The analysis is written in GitHub-flavored markdown suitable for posting as an issue comment.

## Tips

- The binlog is the single source of truth. Always start there rather than guessing from target files alone.
- Use `search_binlog` liberally — it supports the full MSBuild Structured Log Viewer query language. Use `$target`, `$task`, `under()`, `not()`, and property matching to narrow results.
- When tracing item flow, search for the item group name (e.g., `IntermediateSatelliteAssembliesWithTargetPath`) rather than file paths — this shows you every target that adds or removes from that group.
- The SDK publish pipeline has several "compute" targets that run before "copy" targets. Issues often stem from a "compute" target that fails to filter items for a specific scenario (AOT, single-file, trimming, etc.).
- Cross-repo issues (e.g., between dotnet/sdk and dotnet/runtime) are common because the NativeAOT, ILLink, and single-file targets hook into the SDK pipeline via `BeforeTargets`/`AfterTargets` and must keep their item group manipulations in sync.
- Use `get_evaluation_properties_by_name` to verify that properties like `PublishAot` are actually set to the expected values during evaluation — misconfigured properties are a common root cause.
- When the build fails before the problematic stage (e.g., link failure prevents seeing publish output), the binlog still contains all the target/item setup — you can trace the data flow up to the point of failure and infer what would happen next by reading the target definitions.
