# PR Feedback Plan

## Quick Fixes

### Q1: BundleUpgradeCodes.cs L23 — Use Windows installer terminology
**Link:** [r4544332085](https://github.com/dotnet/sdk/pull/57016#discussion_r4544332085)
**Comment:** "On windows the proper terminology is Upgrade Related."
**Status:** ✅ Done — Renamed `ShareFamily` to `UpgradeRelated` and updated both callers.
**Code:** [BundleUpgradeCodes.cs](dotnetup.Library/WindowsInstaller/BundleUpgradeCodes.cs#L23), [InstallerOrchestratorSingleton.cs](dotnetup.Library/InstallerOrchestratorSingleton.cs)

## Summary

- Total comments: 1 (Quick Fixes: 1); all resolved.
- Files modified: `dotnetup.Library/WindowsInstaller/BundleUpgradeCodes.cs`, `dotnetup.Library/InstallerOrchestratorSingleton.cs`.
- Build status: Passed as part of the focused test-project build.
- Test status: Blocked at launch because runtime `11.0.0-rc.2.26508.107` is not installed; only `11.0.0-rc.2.26504.105` is available.
