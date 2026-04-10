
---
applyTo: "**"
---
- Environment: Windows 11 using PowerShell 7
- Never use `&&` to chain commands; use semicolon (`;`) for PowerShell command chaining
- Prefer PowerShell cmdlets over external utilities when available
- Use PowerShell-style parameter syntax (-Parameter) rather than Unix-style flags
- New class definitions should live inside a separate file. Unless specified, you should almost NEVER create a new class or type definition inside of an existing class. If you do, it should NEVER be public - if that's the case it should live in a separate class file.
- Refrain from returning tuples from methods in most cases - either separate the method, or if it makes sense to share the responsibility, then a record, struct, or class should be returned.
- Always look for existing code which can be used to make sure you're not inventing something already done.
- Follow the single responsibility principle. The name of a class should strictly delegate its purpose. A class should NEVER solve more than one core purpose or type of logic. Example: a class named `InstallPathResolver` should only be responsible for resolving install paths, and should not also be responsible for prompting the user for input. Add comments to the top of classes specifically outlining their responsibility and assumptions.
- If you're about to add a compiler or style warning disable inline, RECONSIDER. Please see if you can minimally fix the code to follow the rule instead. If you still decide to add something like #pragma warning disable, STOP and ask for permission stating why you want to do this.

Code Style:
- An `.editorconfig` at `src/Installer/.editorconfig` governs all dotnetup code. Follow it strictly for new code.
- Key conventions: file-scoped namespaces, `s_` prefix for static fields, `_` prefix for instance fields, file headers, sorted usings, `ConfigureAwait(false)`, `CultureInfo.InvariantCulture` for formatting.
- All projects use `TreatWarningsAsErrors`. Style violations break the build.
- To auto-format a project: `d:\sdk\.dotnet\dotnet format <project.csproj> --no-restore`
- When debugging or iterating on a bug fix, it is fine to temporarily ignore style issues and fix them afterward. Prefer working code over perfect formatting during active troubleshooting.
- Do not reformat unrelated code in the same commit as a bug fix — keep formatting changes in separate commits to preserve clean git blame.

Testing:
- When running tests after a change, first run only the tests relevant to the code you modified. Use `--filter` to target specific test classes or methods (e.g., `--filter "FullyQualifiedName~ParserTests"`) rather than running the entire test suite.
- Only run the full test suite if the targeted tests pass and you have reason to believe the change could affect other areas.

Concurrency:
- Multiple agents or terminals may be building or running tests concurrently in this workspace. To avoid file-lock conflicts on the dotnetup executable and build outputs, **always build and test into an isolated output directory** using `/p:ArtifactsDir=`.
- Choose a short, descriptive name based on what you are working on (e.g., the bug, feature, or test class name). Use that name to create a unique artifacts path under `d:\sdk\artifacts\tmp\`.
- Build command:  `d:\sdk\.dotnet\dotnet build d:\sdk\src\Installer\dotnetup\dotnetup.csproj "/p:ArtifactsDir=d:\sdk\artifacts\tmp\<descriptive-name>\"`
- Test command:   `d:\sdk\.dotnet\dotnet test d:\sdk\test\dotnetup.Tests\dotnetup.Tests.csproj "/p:ArtifactsDir=d:\sdk\artifacts\tmp\<descriptive-name>\"`
- Use the **same** `/p:ArtifactsDir=` value for both the build and the test so the test project picks up the build output.
- Example for a parser fix:
  ```
  d:\sdk\.dotnet\dotnet build d:\sdk\src\Installer\dotnetup\dotnetup.csproj "/p:ArtifactsDir=d:\sdk\artifacts\tmp\parser-fix\"
  d:\sdk\.dotnet\dotnet test d:\sdk\test\dotnetup.Tests\dotnetup.Tests.csproj "/p:ArtifactsDir=d:\sdk\artifacts\tmp\parser-fix\" --filter "FullyQualifiedName~ParserTests"
  ```
- Clean up temporary artifacts directories when you are done: `Remove-Item -Recurse -Force d:\sdk\artifacts\tmp\<descriptive-name>`

Terminal output handling:
- The `run_in_terminal` tool uses a **single shared shell** for all non-background calls. It reads the entire accumulated buffer — NOT just your command's output. After many commands, the buffer exceeds 60KB and gets truncated with `[... PREVIOUS OUTPUT TRUNCATED ...]`.
- **Do NOT** try to work around this by launching a sub-shell (`pwsh -Command "..."`). The tool still reads the outer terminal's full buffer.
- **Recommended: redirect to file**. Pipe output to a temp file, then use `read_file` to read the results:
  ```
  d:\sdk\.dotnet\dotnet build <project> "/p:ArtifactsDir=..." 2>&1 | Out-File d:\sdk\artifacts\tmp\<name>\build-output.txt
  ```
  Then use the `read_file` tool on `d:\sdk\artifacts\tmp\<name>\build-output.txt` to inspect results. Read the last ~50 lines first to check for errors.
- **Alternative: filter inline** if you only need pass/fail: `2>&1 | Select-String "error|Build succeeded" | Select-Object -Last 15`
- **Alternative: background terminal**. Use `isBackground: true` and then `get_terminal_output` with the returned ID. Each background call gets a fresh terminal with no buffer pollution.

PR Feedback Resolution:
- When asked to resolve PR feedback (e.g., "resolve PR feedback for https://github.com/dotnet/sdk/pull/12345"), follow this workflow:

1. **Gather comments** — Use the GitHub MCP server tools (`mcp_github_pull_request_read`, `mcp_github_list_pull_requests`, etc.) or git MCP tools to fetch all review comments and conversation threads from the PR. Identify which comments are unresolved vs already resolved/outdated.

2. **Create a plan document** — Generate `src/Installer/pr-feedback-plan.md` with all comments organized by size category:
   - **Already Resolved** — Comments that are outdated or already addressed. Table format with columns: #, File, Comment, Link, Status.
   - **Quick Fixes** — Renames, comment additions, single-line changes.
   - **Medium Fixes** — Multi-file refactors, method extractions, logic changes.
   - **Large / Investigation Items** — Architecture changes, cross-cutting concerns, items needing research.
   Each item gets a unique ID (Q1, Q2, ..., M1, M2, ..., L1, L2, etc.), a link to the GitHub discussion comment, the reviewer's comment text, and a status field.

3. **Execute fixes** — Work through items in size order (Quick first, then Medium, then Large). Use subagents (the `Explore` agent) for research on larger items. For each item:
   - Build after each fix: `d:\sdk\.dotnet\dotnet build d:\sdk\src\Installer\dotnetup\dotnetup.csproj "/p:ArtifactsDir=d:\sdk\artifacts\tmp\pr-feedback\"`
   - Run relevant tests after groups of related fixes.
   - Mark items ✅ Done in the plan document as they are completed.

4. **Update the plan document** — After all items are complete, **reorder** the entries in `pr-feedback-plan.md` to match the order they appear on the GitHub PR conversation page (i.e., by file path and line number as GitHub displays them, not by size category). This makes it easy for the author to walk through the PR on github.com and close/resolve each comment in order. Include a summary section containing:
   - Total comments, counts per category, all marked ✅
   - A "Files Modified" list
   - Build and test status

5. **Link format in the plan** — Use workspace-relative links for code references so they open in the IDE, NOT GitHub blob URLs. Include specific line numbers for the changed code. Example:
   - ✅ Correct: `[InstallWalkthrough.cs](dotnetup/Commands/Shared/InstallWalkthrough.cs)`
   - ✅ Correct with line: `[InstallWalkthrough.cs L36](dotnetup/Commands/Shared/InstallWalkthrough.cs#L36)`
   - ❌ Wrong: `[InstallWalkthrough.cs](https://github.com/dotnet/sdk/blob/.../InstallWalkthrough.cs)`
   - For GitHub discussion links, use the `r<id>` format: `[r2948551306](https://github.com/dotnet/sdk/pull/53464#discussion_r2948551306)`

6. **Handle TODOs** — For items that cannot be fully resolved and require a TODO:
   - Do NOT leave silent TODOs in code. Instead, create a separate `.md` file under `src/Installer/issues/` describing the follow-up work.
   - Format the `.md` as a GitHub issue body: title, description, acceptance criteria, and relevant code links.
   - Include the `dotnetup` label in the frontmatter or title so it can be filed with that label.
   - Be prepared to file these issues via the GitHub MCP server tools when asked.

7. **Example plan entry** (for a completed Quick Fix):
   ```markdown
   ### Q1: InstallWalkthrough.cs L36 — Remove unused parameter
   **Link:** [r2948948414](https://github.com/dotnet/sdk/pull/53464#discussion_r2948948414)
   **Comment:** "Why are we reserving this?"
   **Status:** ✅ Done — Removed `channelVersionResolver` parameter from constructor and the discard assignment. Updated caller in InstallWorkflow.cs.
   **Code:** [InstallWalkthrough.cs](dotnetup/Commands/Shared/InstallWalkthrough.cs#L36), [InstallWorkflow.cs](dotnetup/Commands/Shared/InstallWorkflow.cs#L42)
   ```
