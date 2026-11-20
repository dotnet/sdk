---

name: code-review
description: "Review code changes in dotnet/sdk for problems, either a GitHub pull request or local changes in your branch before a PR exists. Use when asked to review a PR, review local or uncommitted changes, do a code review, check a PR or branch for issues, or review pull request changes. Focuses only on identifying problems, not style nits or praise."

---

# PR Code Review

Review a set of changes in **dotnet/sdk** (a pull request or local changes) and report problems only: bugs, security issues, correctness errors, performance regressions, broken MSBuild/SDK contracts, missing regression tests, and violations of repository conventions. Do not comment on style, do not praise, and do not suggest changes that do not fix a problem.

Treat the PR description and linked issues as claims to verify. Every .NET project uses the SDK, so also ask whether the change uses the right approach.

Do not stop after one pass. Tested PRs with many iterations still ship bugs, usually at boundaries that the tests do not cover: other configuration shapes, authenticated or offline environments, empty or malformed input, cross-platform paths, and large or duplicate input. Continue until you find concrete problems or can explain why each risky path is safe.

## Choose the review mode

- **PR review**: the user gives a PR number or URL, or asks to review the open PR for the current branch. The repository is `dotnet/sdk` unless the user names another.
- **Local review**: the user asks to review local, uncommitted, or unpushed changes, or a branch compared to `main`. There is no PR to fetch or post to.

If the user gives no PR, check whether the current branch has one:

```bash
gh pr view --repo dotnet/sdk --json number,title,headRefName 2>/dev/null
```

If this returns a PR, use PR review mode. Otherwise, or if the user asked for local changes, use local review mode.

## Step 1: Check out the PR branch (PR review mode only)

In PR review mode, finish this step before you fetch the diff or file list. You can call `gh pr view` to get the branch name. In local review mode, go to Step 2.

```bash
gh pr view <number> --repo dotnet/sdk --json headRefName --jq '.headRefName'
git branch --show-current
```

If the branches are the same, go to Step 2. If not, ask the user which option to use:

- **Check out the branch (recommended).** The review can then read the surrounding code: MSBuild targets, callers, test assets, and related SDK areas.
- **Use the GitHub diff only.** The working tree does not change, but the review cannot read the surrounding code, so it is less accurate.

To check out the branch, first look for uncommitted changes:

```bash
git status --porcelain
```

If there are uncommitted changes, tell the user and stash them:

```bash
git stash push --include-untracked -m "auto-stash before PR review of #<number>"
```

Then check out the branch. This works for branches in forks too:

```bash
gh pr checkout <number> --repo dotnet/sdk
```

## Step 2: Get the changes

**PR review mode.** Use the `mcp_github_*` tools if the GitHub MCP server is available. If not, use the equivalent `gh` commands.

1. PR details: `mcp_github_pull_request_read`, method `get` (title, description, base branch, author).
2. Changed files: method `get_files`. Paginate if necessary.
3. Diff: method `get_diff`.
4. Existing review comments: method `get_review_comments`. Do not repeat these comments.

**Local review mode.** Use `git`:

1. Find which changes to review: unstaged (`git diff`), staged (`git diff --staged`), all uncommitted (`git diff HEAD`), or the branch compared to its base (`git diff <base>...HEAD`). If this is not clear, ask the user.
2. For a branch diff, the base is `git merge-base HEAD main`, unless the user gives a different base.
3. Changed files: `git diff --name-status <range>`.
4. Diff: the matching `git diff` command.

For each changed file, read the full file, not only the diff hunks. The surrounding code shows target ordering, item-metadata flow, locking, asset-graph invariants, and call patterns that the diff does not show.

## Step 3: Sort the changes by area

The first five areas have the largest effect on users. Examine them most carefully.

| Area | Paths | Review focus |
| --- | --- | --- |
| CLI | `src/Cli/**` | Command parsing, help text, error messages, exit codes, `.resx` and localization, telemetry, test-runner contracts, other ways to start the command |
| MSBuild tasks and targets | `src/Tasks/Microsoft.NET.Build.Tasks/**`, SDK `**/*.props`, `**/*.targets` | Property and item-metadata flow, target ordering, path normalization (absolute or relative, package-folder roots), effects on build, pack, publish, restore, and test |
| Templates | `template_feed/**`, `src/*TemplateLocator*/**`, `test/dotnet-new.IntegrationTests/Approvals/**` | Generated project contracts, option combinations, package references, language and runner parity, approval baselines |
| Static Web Assets and Web SDKs | `src/StaticWebAssetsSdk/**`, `src/RazorSdk/**`, `src/WebSdk/**`, `src/BlazorWasmSdk/**`, `src/WasmSdk/**` | Asset and endpoint graph invariants, unique identity, OS-aware path normalization, references that stay valid after transforms, scale |
| Versions and dependency flow | `eng/**`, `eng/Version.Details.xml`, `eng/Versions.props`, `NuGet.config`, `global.json`, `Directory.Packages.props` | Dependency flow, semantic version order, source-build, servicing branches, backflow, bootstrap acquisition, effects on downstream repos |
| Compatibility tools | `src/Compatibility/**` (ApiCompat, GenAPI) | Same semantics as the compiler and language, diagnostics, generated reference output, framework differences |
| Watch and containers | `src/Dotnet.Watch/**`, `src/Containers/**` | Process lifetime, cancellation, file-system races, cleanup |
| Analyzers | `src/Microsoft.CodeAnalysis.NetAnalyzers/**` | Correct Roslyn diagnostics on all supported frameworks |
| Build and infrastructure | `eng/**`, `.github/workflows/**`, `.vsts*.yml`, `Directory.Build.props`, `Directory.Build.targets`, `*.slnx` | Unwanted side effects, broken conditions, CI telemetry correlation |
| Tests | `test/**` | Regression tests for the real scenario, correct target and platform conditions, tests that fail without the fix |

## Step 4: Review the code

Read the diff and the code around each change. If the branch is checked out, or in local review mode, read files from the workspace. If you use the GitHub diff only, get files from the PR branch with `mcp_github_get_file_contents`.

### Make your own assessment first

Before you read the PR description, answer these questions from the code:

1. What does the change do? Compare the old and new behavior.
2. Why is it necessary?
3. Is this the right approach? Can an existing SDK mechanism do the same thing more simply? Does it keep the existing abstractions and ownership boundaries?
4. What problems do you see? Bugs, edge cases, missing validation, thread safety, performance, broken contracts, missing tests.

Then read the description, labels, and linked issues, and verify their claims. If you found a problem that the description does not mention, do not make it weaker because the description sounds reasonable. Look at related implementations, parallel templates, and test assets, because a fix in one place often must also go into the others. Recent `git log` history can show reverted approaches or incomplete migrations.

### Compare with existing implementations

If the new code does something that an existing SDK component already does, it should use that component. If it implements the logic again, find the existing implementation and compare the two. New code can be correct C# and still behave differently at the edges. A diff-only review does not find these differences, and they are often the most important findings in this repo.

- Compare behavior: what input each one accepts, how each one orders and normalizes values, and which options each one uses. If the SDK accepts an input in one place, it must not fail in the other. Give the file and line of both implementations.
- If the new code writes output that another component reads (a project-file edit, a generated directive, a manifest), make sure that the output agrees with what the reader expects, including order and default values.
- If a top-level CLI command or alias is added or renamed in `DotNetCommandDefinition.cs`, or if `<Name>CommandParser.ConfigureCommand` or `Parser.ConfigureManagedActions` changes the subcommands, options, `ICustomHelp`, or completion sources shown in help for symbols without an explicit `HelpName`, make sure that `Parser.CreateCommandForGenericHelp`, `Parser.TryParseGenericCommandHelp`, and (for help that is filled at run time) `AotPrintHelpAction` agree with the change.
- For each changed CI entry point, check `DOTNET_CLI_TELEMETRY_SESSIONID`. A workflow or pipeline with more than one job must set it at workflow or pipeline scope. A workflow with one job can set it at job scope. The value must use the format in the [developer guide](../../../documentation/project-docs/developer-guide.md#ci-workflow-telemetry-correlation). Flag a missing value, a value set per step, or a changed value. Without it, the `dotnet` processes of a run cannot be correlated.

### Examine test coverage

Do not accept "tests pass" or "there are tests" as proof. For each change to production behavior, find:

1. **Changed behavior**: the code paths, tasks, targets, properties, or item metadata that changed.
2. **Affected surfaces**: where users or systems can see the change: CLI output and exit codes, the build, pack, publish, or restore graph, generated templates and projects, Static Web Assets endpoints and manifests, NuGet package contents, version selection, analyzer diagnostics, source-build, or downstream projects.
3. **Regression risks**: how existing scenarios can break: target order, lost item metadata, path normalization, version order, platform, TFM, or RID conditions, package-folder roots, source-build compared to a full build, servicing branches, and assumptions about concurrency or MSBuild nodes.
4. **Necessary tests**: the tests that fail without the fix or that would find a later regression.
5. **Missing tests**: affected behavior that neither the PR tests nor existing tests cover.

A PR can have many tests and still not have the test that is necessary. Do not ask for tests of a surface that the change does not affect. Do not ask for tests of documentation, comments, mechanical renames, or refactors that keep the same behavior. Bug fixes must have a test that failed before the fix.

| Change type | Expected tests |
| --- | --- |
| CLI behavior, help text, error messages, exit codes, telemetry, resource strings | Tests in `test/dotnet.Tests/` or the area `*.Tests` project, including other ways to start the command. The test checks what the user sees, not an internal detail. |
| MSBuild tasks and targets, metadata, paths, target order | Integration tests through the build, pack, publish, or restore entry point. A unit test of a task is not sufficient when the risk crosses target boundaries. |
| Templates and generated projects | Integration and approval tests in `test/dotnet-new.IntegrationTests/` for each option, language, runner, and package combination that the code handles differently. |
| Static Web Assets and Web SDK | Integration tests that check asset and endpoint identity, manifest contents, and valid references. A baseline update alone does not prove graph behavior. See the SWA skills. |
| Versions, dependency flow, bootstrap, compatibility tools | Tests for the version order, framework, RID, or source-build dimension. One resolved graph in CI does not prove order or backflow. |
| Analyzers and compatibility semantics | Tests for the important language and API-shape variants on the supported frameworks, not only the easiest case. |

A new snapshot, approval file, manifest, or baseline only proves that the output changed. If the behavior changed, look for a test that checks the behavior. Make sure that a large baseline diff is an intended change.

### What to flag

Flag only real problems. Each finding must identify a concrete issue. Approximate priority:

1. **Missing regression tests**: see "Examine test coverage". Give the code path, the risk, and the necessary test. A skipped or partial test does not prove that a scenario works.
2. **MSBuild semantics**: lost or changed item metadata, unclear absolute, relative, or package-folder paths, changes to target order or side effects that you did not trace through build, pack, publish, restore, and test.
3. **CLI and localization contracts**: help text with the wrong arity or behavior, errors that do not tell the user what to do, text that does not agree with CLI conventions, user-visible strings that are not in `.resx`.
4. **Template output**: option combinations that produce different package references or project files, lost parity between parallel templates, SDK workarounds for behavior that the `dotnet/templating` engine owns (report those upstream).
5. **Versions and servicing**: string or file order used instead of semantic version order; dependency-flow changes that ignore source-build, servicing, backflow, bootstrap, or downstream repos; central package changes with unwanted TFM or RID effects.
6. **Threads, lifetime, cancellation, scale**: shared mutable state or file-system races in tasks that can run in parallel, assumptions about MSBuild nodes, missing cancellation or cleanup in long-running or multi-process code, identity assumptions or quadratic algorithms on large graphs.
7. **Asset and endpoint graphs**: duplicate asset or endpoint identities, path comparisons that ignore the OS, references that become invalid after transform, compression, pack, or publish.
8. **Analyzer and compatibility semantics**: diagnostics, reference output, or compatibility results that are different from compiler or language semantics on a supported framework.
9. **Bugs**: logic errors, off-by-one errors, null dereferences, missing `await`, incorrect disposal.
10. **Security**: injection, exposed credentials, insecure defaults.
11. **Changed contracts**: a refactor that changes behavior. Examples: a property that threw an exception now returns a default; a validation or override is removed; `SingleOrDefault` (throws on duplicates) becomes `FirstOrDefault`.
12. **Missing validation at boundaries**: external input that is not validated, or missing null checks at public or internal entry points. Do not flag null checks for parameters that the type system makes non-null.
13. **Resource leaks**: an `IDisposable` (for example `CancellationTokenSource`, `SemaphoreSlim`, or a process handle) that is not disposed, also when the code was moved from a different location.
14. **Missing or incorrect comments**: a target or property decision, runtime mapping, or cross-repo boundary that a maintainer cannot understand from the code and that has no comment; a comment that the code no longer agrees with; a workaround with no issue link.
15. **Repository conventions**: a violation of the [repository guardrails](../../../AGENTS.md#guardrails) or the nearest area `AGENTS.md` that the build does not find. Examples: a hand-edited generated file, or a fix in this repo for behavior that a different repo owns.
16. **Incorrect AI or contributor docs**: the change makes a statement in an AI-facing or contributor-facing file incorrect (a command, file path, guardrail, workflow, convention, or architecture description), and the PR does not update that file. Examples are the root or area `AGENTS.md`, `.github/memory/*.md`, `.github/skills/*/SKILL.md`, `.claude/skills/*/SKILL.md`, `.github/agents/*.agent.md`, and files in `documentation/`. Also flag new statements about architecture, product behavior, or ownership that have no link to a primary source (code, project file, ADR, design document, issue, or PR), links that do not support the statement, and guesses written as facts. Do not flag internal changes that no file describes. Do not ask for sources for routine instructions or facts that are clear from the code.

### Maintainer checklist

Long-time human reviewers of this repo often found these problems. Check each item that applies. The PR numbers are examples. A checklist finding is a ⚠️ warning, unless it causes incorrect behavior, data exposure, or a broken contract; then it is an ❌ error.

1. **Test mechanics**: Each theory case uses a unique test directory, and a second local run still passes (#11317, #16521). Tests do not change process-wide state, such as culture or environment variables, without the locks in [`test/AGENTS.md`](../../../test/AGENTS.md) (#29559). Tests use `TestProject` and the existing helpers and assets when they can express the scenario. Change project XML (for example with `TestProject.ProjectChanges`) or add an asset in `test/TestAssets/` only when they cannot (#3275, #18459). Expected values are hard-coded, not calculated by the code under test (#16530). Use `Assert.Throws`, not try/catch with a forced failure. Do not use arbitrary short timeouts. Do not make an assertion weaker so that it accepts nondeterministic results (#55618, #54801). The test must go through the code path that it claims to test (#2196).
2. **Inputs used together**: For a new or changed option or property, examine conflicting options, other spellings of the same input (`--configuration Debug` and `-p:Configuration=Debug`), letter casing (`True` and `true`), default values, solutions with mixed TFMs, and projects with more than one target framework (#19402, #26808, #29155).
3. **CLI parser conventions**: Option names are lowercase (#24446). Do not use a short alias that already has a different meaning in the CLI, such as `-f` (#18502). Boolean flags use `Arity = ArgumentArity.Zero` (#26963). In System.CommandLine, `Option<bool>` accepts an optional value by default (arity `ZeroOrOne`), so without this setting `--flag false` parses and the forwarding code can ignore the value. An option with a fixed set of values restricts them in the parser, with an enum or `AcceptOnlyFromAmong`. Flag an option that accepts any value (#29017). Validation is in the parser, not in the command handler (#28333). Arguments are a `string[]`, not one joined string (#26155). Split `key=value` input only at the first `=` (#28059). Parse-error handling must not break commands that forward arguments, such as `dotnet msbuild` (#23677).
4. **Telemetry**: Hash values that a user can define, such as custom target names and TFMs, in the same way as the existing events (#26427, #27042). Do not rename existing telemetry properties (#29131).
5. **Errors and diagnostics**: Catch only the expected exception type, and only around the operation that can fail (#43393, #26369). When the code ignores an expected failure, keep the details in verbose output, and let unexpected errors fail (#16891, #19124). A message tells the user why the problem occurred and includes the related value, such as the workload ID or the `global.json` path. Its tone agrees with the other messages of the command. CLI messages do not refer to Visual Studio, unless Visual Studio owns the affected state, such as workloads that the Visual Studio Installer manages (#25358, #25733, #28811).
6. **Versions and TFMs**: Do not hard-code the latest TFM. Use `NETCoreAppMaximumVersion` or the existing helpers (#18815). Compare the framework identifier and version, or use the NuGet TFM parser. Do not parse TFM strings by hand (#24656, #18216). Make sure that the change uses the correct value: the runtime of the SDK or the target framework of the app (#36195, #52242). Do not use private MSBuild properties when a supported property exists (#37139).
7. **Consumers and later operations**: Find where the changed behavior goes next: design-time builds and IntelliSense, `run`, `test`, `publish`, and later commands such as `workload update` (#12633, #18889, #19124).
8. **Package and publish output**: When packaging or publishing changes, compare the files produced before and after the change, including the files for each platform (#923, #1843).
9. **Async and wasted work**: No busy-wait loops (#26870). No blocking on async code (#52581). Do not load the project graph again when its inputs did not change (#56723).
10. **Layer boundaries**: Do not add console UI dependencies, such as Spectre, to headless projects (#53540). Do not inherit from a collection type only to reuse its code; use composition (#16823, #24005).
11. **Changes across repos and releases**: The change must not need a simultaneous merge in another repo (#12106). Check the minimum MSBuild version (#11358) and the target branch for servicing fixes (#14798).
12. **Unrelated changes**: Flag whitespace-only edits in files that the PR does not otherwise need to change (#51624, #25991). Flag unintended resets of `.xlf` translations in merge PRs (#23977).

### What not to flag

- Style that `.editorconfig`, `dotnet format`, or analyzers handle.
- Naming, wording, or formatting preferences that a human reviewer would call a "nit".
- Problems that `TreatWarningsAsErrors`, code-style enforcement, central package management, or SDL checks find. CI runs these on Windows, Linux, and macOS.
- Missing XML doc comments, unless a public API has no documentation.
- `.xlf` files that are not regenerated during development. The `.resx` file is the source.
- Refactors of unrelated code.
- More tests when existing tests already supply the input that failed and assert the changed behavior.
- Abstractions or reuse designs when there is no second use case.
- Problems that you cannot support with evidence from the diff or the code. Do not say that an API "does not exist", "is deprecated", or "is unavailable" only from training data. If you are not sure, ask, or report it as low confidence.

### Moved code

When code moves to a different file (for example, a task, target, or helper is extracted), review it as new code:

- Flag existing problems in the moved code, and mark them "Pre-existing issue, good opportunity to fix during this refactoring."
- If a type, target, or method is replaced, compare the old and new implementations. Look for removed overrides, different exceptions, weaker validation, removed invariant checks, and changed metadata or target order.
- If a class or target is removed, make sure that all callers and dependent targets still work.

## Step 5: Show the findings to the user

Do not post a review automatically. Show the findings as a numbered list, with the most important first. For each finding, give:

- **Severity**: ❌ **error** (fix before merge: bugs, security, broken invariants, missing regression tests for behavior changes) or ⚠️ **warning** (should fix: performance, missing validation, differences from SDK patterns). Formatting, whitespace, unrelated edits, and leftover or commented-out code are never errors.
- **Confidence**: High, Medium, or Low.
- **Verification**: what you checked and what you did not check, for example "I read the span semantics in the code but did not run a repro".

Every finding needs a file and line and a real mechanism. Do not remove a real concern because it is an edge case or because you could not confirm it. Report it with Low confidence. Use Low only when you are not sure, not to hedge a problem that you verified. Do not report problems that you cannot connect to the code.

Then ask the user what to do. The user can say, for example, "Add 1, 3, 5 as comments", "Add all", or "Add none".

In local review mode, stop here. There is no PR to post to.

## Step 6: Post the selected comments (PR review mode only)

### AI disclosure

If you post with the credentials of a person (not a Copilot or bot account), add a short visible note to the review summary, such as a `> [!NOTE]` alert, that says the content was AI-generated. Do not add it only if the user asks you not to.

### Auto-merge check

Before you submit a review with `event: "APPROVE"`, check for auto-merge:

```bash
gh pr view <number> --repo dotnet/sdk --json autoMergeRequest --jq '.autoMergeRequest'
```

If the result is not null and the review has comments, tell the user:

> **Warning:** This PR has auto-merge enabled. If you approve it, it can merge before the author reads your comments. Do you want to:
>
> 1. **Approve anyway**: submit as APPROVE. Auto-merge can start immediately.
> 2. **Comment only**: submit as COMMENT, so the author can fix the problems first.

Wait for the answer. For option 2, use `event: "COMMENT"`.

### Post the review

1. Create a pending review: `mcp_github_pull_request_review_write`, method `create`, with no `event`.
2. Add each selected finding with `mcp_github_add_comment_to_pending_review`:
   - `subjectType`: `LINE` for a line, `FILE` for a file
   - `side`: `RIGHT` for new code
   - `path`: the relative file path
   - `line`: the line number in the diff
   - `body`: the problem and how to fix it
3. Submit: `mcp_github_pull_request_review_write`, method `submit_pending`:
   - Use `event: "APPROVE"` only if the user asked to approve, and auto-merge is off or the user accepted the warning.
   - Otherwise use `event: "COMMENT"`. Use `"REQUEST_CHANGES"` only if the user asks for it.
   - In the summary, give the number of problems in each category, and the AI disclosure.
   - If the user selected no findings, do not create a review. Tell the user that you posted nothing.

## Comment rules

- One problem in each comment.
- Give the exact lines, property, item, target, or condition, and how you verified the problem (for example, "I checked all callers, and none validate this metadata").
- If the fix is not clear, suggest one. Suggested code must compile and use the style of the file.
- If the same problem occurs many times, comment on the first one and list the others.
- The style of an existing file has priority over general rules.
- Do not repeat existing review comments.
- If you are not sure, ask for human review. Do not approve. An incorrect "looks good" is much worse than an unnecessary escalation. Keep two questions separate: is the code correct, and does the change fix the full problem?
