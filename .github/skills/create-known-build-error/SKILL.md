---
name: create-known-build-error
description: >-
  Create and validate Known Build Error GitHub issues for flaky or infrastructure
  build/test failures. USE FOR: creating known build error issues, validating
  ErrorMessage/ErrorPattern JSON against real build failures, checking pattern
  specificity to avoid false positives, filing issues with the Known Build Error
  label. DO NOT USE FOR: investigating CI failures themselves (use ci-analysis),
  debugging test crashes (use ci-crash-dump), general build troubleshooting.
---

# Create Known Build Error

Create a GitHub issue to track a known build or test error so that Build Analysis
can automatically match it against future failures and (optionally) retry builds.

## Workflow overview

```
1. Gather input    → Build URL / PR number / build ID
2. Fetch failures  → Use AzDO + Helix tools to list failed steps and tests
3. User selects    → Which failure(s) to create known build errors for
4. Draft pattern   → Construct ErrorMessage or ErrorPattern JSON
5. Validate        → Test pattern against the triggering build's actual logs
6. Specificity     → Test pattern against recent passing/other builds to check for false positives
7. File issue      → Create GitHub issue with proper template and label
```

## Step 1: Gather input

Ask the user for one of:
- An **AzDO build URL** (e.g. `https://dev.azure.com/dnceng-public/.../_build/results?buildId=NNNN`)
- An **AzDO build ID** (numeric)
- A **GitHub PR URL or number** — then use `hlx-azdo_builds` with `prNumber` to find the build

If not provided, check whether the current session has an associated PR and offer to use its latest CI build.

## Step 2: Fetch failures from the build

Use the AzDO and Helix MCP tools to gather failure data:

### Build-level failures
1. `hlx-azdo_timeline` with `filter: "failed"` → get failed stages/jobs/tasks and their log IDs
2. For each failed task, use `hlx-azdo_search_log` with `pattern: "error"` to find error lines
3. `hlx-azdo_build_analysis` → check if any known issues already match this build

### Test-level failures
1. `hlx-azdo_test_runs` → list test runs for the build
2. `hlx-azdo_test_results` with failed test runs → get failing test names, error messages, stack traces
3. `hlx-azdo_helix_jobs` → get Helix job IDs for failed legs
4. For interesting failures, use `hlx-helix_logs` to get console output

Present a numbered list of distinct failures to the user and ask which one(s) they want to
create known build errors for. Group related failures when possible.

## Step 3: Construct the error pattern

Read the references file `references/pattern-quality.md` for detailed guidance on
writing high-quality patterns.

**Key rules when drafting patterns:**

1. **Prefer error messages over test names.** Using a fully qualified test name as the pattern
   is essentially equivalent to disabling that test — any failure mode will match. Instead,
   use the actual error message or assertion text.

2. **Use ErrorMessage (string contains) when the error text is stable.** This is simpler and
   less error-prone than regex.

3. **Use ErrorPattern (regex) when variable parts exist.** Replace timestamps, paths, machine
   names, GUIDs, and other variable segments with appropriate regex patterns:
   - Timestamps → `\\d{4}-\\d{2}-\\d{2}` or `.*`
   - Paths → `.*` or `[^ ]+`
   - GUIDs → `[0-9a-f-]+`
   - Numbers → `\\d+`

4. **Be specific enough to avoid false positives** but general enough to catch all instances
   of the same root cause.

5. **For multi-line matching**, use an array of strings. All entries must match in order,
   each on a subsequent line (not necessarily consecutive). This is useful for assertions like:
   ```json
   { "ErrorMessage": ["Assert.True() Failure", "Actual:   False"] }
   ```

6. **JSON escaping**: Special characters in JSON strings must be escaped. Backslashes in regex
   patterns must be double-escaped: `\\.` in regex becomes `\\\\.` in JSON (but in the JSON
   value it reads as `\\.`).

### Pattern template

```json
{
  "ErrorMessage": "",
  "ErrorPattern": "",
  "BuildRetry": false,
  "ExcludeConsoleLog": false
}
```

- Set **only one** of `ErrorMessage` or `ErrorPattern` (leave the other as `""` or omit it).
  `ErrorPattern` takes priority if both are populated.
- Set `BuildRetry: true` only for transient/infrastructure failures (network timeouts,
  agent connectivity) where a retry is likely to succeed.
- Set `ExcludeConsoleLog: true` to skip Helix console log searching. Use this when the
  pattern would produce false positives in verbose console output but the error reliably
  appears in AzDO test results or timeline messages.

## Step 4: Validate the pattern against the triggering build

This is the critical differentiation of this skill. **Always validate before filing.**

### 4a. Positive validation — does it match the triggering failure?

Run the `scripts/Validate-KnownIssuePattern.ps1` script to test the pattern against
the actual error text from the build:

```powershell
& .github/skills/create-known-build-error/scripts/Validate-KnownIssuePattern.ps1 `
  -ErrorText "<the actual error text from the build>" `
  -ErrorMessage "<the proposed ErrorMessage>"
# or
& .github/skills/create-known-build-error/scripts/Validate-KnownIssuePattern.ps1 `
  -ErrorText "<the actual error text from the build>" `
  -ErrorPattern "<the proposed ErrorPattern>"
```

If the script is not available or impractical, do the matching manually:
- For `ErrorMessage`: check that each line of the actual error contains the corresponding
  ErrorMessage string (case-insensitive `String.Contains`).
- For `ErrorPattern`: check that each regex matches the corresponding line with flags:
  `Singleline`, `IgnoreCase`, `NonBacktracking`.

**The pattern MUST match the triggering failure.** If it doesn't, iterate.

### 4b. Specificity validation — does it match too broadly?

Search **other failure messages** from the same build to check for unintended matches.
Use `hlx-azdo_search_log` across other failed (and ideally some passing) legs.

Additionally, search recent builds for false positives:
1. Use `hlx-azdo_builds` to find 3-5 recent completed builds for the same pipeline
2. For each, use `hlx-azdo_search_log` with the proposed error pattern/message as the
   search text
3. If the pattern matches failures in unrelated builds, it's too generic

**Report findings to the user:**
- ✅ "Pattern matched the triggering failure in leg X"
- ✅ "Pattern did NOT match 3 other recent builds — good specificity"
- ⚠️ "Pattern also matched build #1234 (leg Y) — this may be the same issue recurring"
- ❌ "Pattern matched 5/5 recent builds including passing ones — too generic, needs refinement"

If too generic, suggest refinements and iterate with the user.

### 4c. Test history validation (optional, for test failures)

For test failures, check the test's recent history:
1. Use `hlx-azdo_test_results` to find the test run and result IDs
2. Note the test case name and look for historical failure patterns
3. If the same test has failed with different error messages, ensure the pattern only
   matches the specific failure mode being tracked

## Step 5: Set BuildRetry and ExcludeConsoleLog

Ask the user about these flags with guidance:

**BuildRetry:**
- Recommend `true` for: network timeouts, NuGet restore failures, agent connectivity
  issues, Helix machine provisioning errors
- Recommend `false` for: test assertion failures, build compilation errors, anything
  that suggests a real code problem

**ExcludeConsoleLog:**
- Recommend `true` when: the error pattern is generic enough to match noise in verbose
  Helix console logs, or the error only appears in AzDO-reported test results
- Recommend `false` (default) when: the error might only appear in Helix console logs

## Step 6: File the GitHub issue

Use the `create_issue` tool with:

**Title:** A clear, concise description of the failure. Examples:
- "Tracking issue for NuGet restore timeouts in CI"
- "Flaky test: System.Net.Http.Tests.HttpClientTest.SendAsync_Timeout"
- "CMake ZLIB not found on Ubuntu helix machines"

**Labels:** `["Known Build Error"]`

**Body:** Use this template:

````markdown
## Build Information
Build: <AzDO build URL>
Build error leg or test failing: <leg name or test name>
Pull request: <PR URL if applicable>

## Error Message

```json
{
  "ErrorMessage": "<validated message>",
  "ErrorPattern": "<validated pattern>",
  "BuildRetry": <true|false>,
  "ExcludeConsoleLog": <true|false>
}
```

## Validation Results
- **Triggering build:** ✅ Pattern matched
- **Specificity check:** <results from step 4b>
- **Pattern type:** ErrorMessage / ErrorPattern
- **Recommendation:** <any notes about the pattern>
````

After filing, inform the user that Build Analysis will automatically scan builds from
the last 24 hours and all future builds against this known issue.

## Important notes

- **Do NOT use this for actual build breaks.** If the failure is caused by the PR's own
  changes, this is not a known build error — it's a real failure that needs fixing. Build
  Analysis will allow PRs to merge if they match a known issue, so misuse can let broken
  code through.
- Known issues are scoped: infrastructure issues go in `dotnet/dnceng`, repository issues
  go in the repo where the failure occurs.
- The `Known Build Error` label must exist in the target repository.
- View the [Known Build Errors project board](https://github.com/orgs/dotnet/projects/111/views/2)
  to check for existing known issues before creating duplicates.
