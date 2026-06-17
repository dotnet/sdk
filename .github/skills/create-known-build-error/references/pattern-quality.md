# Pattern Quality Guide for Known Build Errors

## Pattern quality hierarchy (best → worst)

### ✅ Excellent: Specific error message substring
```json
{ "ErrorMessage": "Could NOT find ZLIB (missing: ZLIB_LIBRARY ZLIB_INCLUDE_DIR)" }
```
Matches exactly the failure mode. Won't match unrelated failures.

### ✅ Good: Error message with variable parts replaced by regex
```json
{ "ErrorPattern": "An instance of analyzer Microsoft\\.CodeAnalysis\\.CSharp\\.Analyzers\\..* cannot be created from" }
```
Captures the error class while allowing for different analyzer names.

### ✅ Good: Multi-line matching for assertion failures
```json
{ "ErrorMessage": ["Assert.Equal() Failure", "Expected: 200", "Actual:   500"] }
```
Narrows the match to a specific assertion with specific values.

### ⚠️ Acceptable: Error message with moderate specificity
```json
{ "ErrorMessage": "Failed to retrieve information" }
```
Matches a known failure pattern but could match different package restore failures.
Consider whether that's desirable.

### ❌ Poor: Fully qualified test name only
```json
{ "ErrorMessage": "System.Net.Http.Tests.HttpClientTest.SendAsync_Timeout" }
```
**This matches ANY failure of this test**, regardless of the actual error. It's equivalent
to disabling the test. If the test fails for a new, different reason, it will still be
marked as a known issue and potentially allow broken code to merge.

**Instead, use the actual error/assertion message from the test failure.**

### ❌ Poor: Overly generic pattern
```json
{ "ErrorMessage": "error" }
```
or
```json
{ "ErrorPattern": ".*failed.*" }
```
Will match nearly every build failure. Useless and dangerous.

## Guidelines for choosing ErrorMessage vs ErrorPattern

| Scenario | Use | Example |
|---|---|---|
| Error text is stable, no variable parts | `ErrorMessage` | `"Could NOT find ZLIB"` |
| Error has timestamps, paths, or GUIDs | `ErrorPattern` | `"error CS8034: Unable to load .* from .*"` |
| Need to match a class of similar errors | `ErrorPattern` | `"The command .+ failed"` |
| Simple substring match is sufficient | `ErrorMessage` | `"The agent did not connect"` |

## Common variable parts to replace with regex

| Variable | Regex replacement | Example |
|---|---|---|
| Timestamps | `\\d{4}-\\d{2}-\\d{2}[T ]\\d{2}:\\d{2}:\\d{2}` or `.*` | `2024-01-15T10:30:00` |
| File paths | `[^ ]+` or `.*` | `/home/runner/work/...` |
| GUIDs | `[0-9a-fA-F-]+` | `9ee6d478-d288-...` |
| Build numbers | `\\d+` | `1467879` |
| Package versions | `\\d+\\.\\d+\\.\\d+` | `6.0.100` |
| Machine names | `[^ ]+` | `Build.Internal-10` |

## Regex flags used by Build Analysis

The matching engine uses these .NET regex options:
- **Singleline**: `.` matches newline characters
- **IgnoreCase**: Case-insensitive matching
- **NonBacktracking**: Linear-time matching, safe for untrusted patterns

Test your patterns at [regex101.com](https://regex101.com/) using:
- Flavor: `.NET (C#)`
- Flags: Single line, Insensitive, (NonBacktracking is not available on regex101 but
  means you cannot use backreferences, lookahead, or lookbehind)

## JSON escaping

Special characters in JSON strings must be escaped:

| Character | JSON escape |
|---|---|
| `"` | `\"` |
| `\` | `\\` |
| Newline | `\n` |
| Tab | `\t` |

For regex patterns, this means regex backslashes are double-escaped:
- Regex `\.` → JSON `"\\."` 
- Regex `\d+` → JSON `"\\d+"`
- Regex `\\` (literal backslash) → JSON `"\\\\"`

## Multi-line matching

When a single line isn't specific enough, use an array:

```json
{
  "ErrorMessage": ["Assert.True() Failure", "Actual:   False"]
}
```

Rules:
- All entries must match in the order specified
- Each entry matches against a single line
- Lines must appear consecutively in the log
- Do NOT mix ErrorMessage and ErrorPattern in the same array
- Array syntax works for both ErrorMessage and ErrorPattern

## ExcludeConsoleLog guidance

The search space for known issue matching includes:

| Source | Type | Excluded by flag? |
|---|---|---|
| AzDO build log (full job log) | Build errors | No |
| AzDO timeline error messages | Build errors | No |
| Test ErrorMessage field | Test errors | No |
| Test StackTrace field | Test errors | No |
| Helix console log | Test errors | **Yes** |

Set `ExcludeConsoleLog: true` when:
- Your pattern might match normal diagnostic output in Helix logs
- The error reliably appears in AzDO test results (ErrorMessage/StackTrace)
- You're getting false positive matches from verbose Helix output

## Checking for existing known issues

Before creating a new known issue, check:
1. [Known Build Errors project board](https://github.com/orgs/dotnet/projects/111/views/2)
2. Search the repo: `https://github.com/dotnet/<REPO>/issues?q=is%3Aopen+is%3Aissue+label%3A%22Known+Build+Error%22`
3. Use `hlx-azdo_build_analysis` on the build — it shows existing known issue matches
