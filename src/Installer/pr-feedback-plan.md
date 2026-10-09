# PR Feedback Plan

## Medium Fixes

### M1: Restrict RID preservation to linux-musl
**Link:** [r4235028560](https://github.com/dotnet/sdk/pull/56569#discussion_r4235028560)
**Comment:** "We should probably change this so we only special case `linux-musl` and otherwise keep the old logic."
**Status:** ✅ Done — Preserved only the portable `linux-musl` RID family and restored host-platform detection for all other runtime identifiers.
**Code:** [DotnetupUtilities.cs L52](Microsoft.Dotnet.Installation/Internal/DotnetupUtilities.cs#L52), [DotnetupUtilitiesTests.cs](../../test/dotnetup.Tests/DotnetupUtilitiesTests.cs)

## Summary

- Total comments: 1
- Medium fixes: 1, all ✅
- Files modified:
  - `src/Installer/Microsoft.Dotnet.Installation/Internal/DotnetupUtilities.cs`
  - `test/dotnetup.Tests/DotnetupUtilitiesTests.cs`
- Validation:
  - `DotnetupUtilitiesTests`: 4 passed, 0 failed
  - Product and test analyzer/style builds: succeeded with 0 warnings and 0 errors
