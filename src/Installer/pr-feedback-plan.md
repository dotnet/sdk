# PR #55511 Feedback Resolution

Resolved against head `89ff202bc3a83311c5198b4cfba6769470e57f1e`.

## Completed Feedback in PR Order

### A1: NativeSelfUpdateFiles.cs — Require native replacement coverage in CI

**Link:** [r4031748934](https://github.com/dotnet/sdk/pull/55511#discussion_r4031748934)
**Comment:** Native replacement tests were inconclusive because CI did not provide two executables.
**Status:** ✅ Done — Windows and Linux publish distinct `0.2.0-native-test.1` and `0.2.0-native-test.2` fixtures, set both executable variables, and require native coverage.
**Code:** [NativeSelfUpdateFiles.cs](../../test/dotnetup.Tests/Utilities/NativeSelfUpdateFiles.cs), [dotnetup-tests.yml](../../eng/pipelines/templates/jobs/dotnetup/dotnetup-tests.yml)

### M2: DotnetupProcessInfo.cs — Distinguish Native AOT from a managed apphost

**Link:** [r4067391096](https://github.com/dotnet/sdk/pull/55511#discussion_r4067391096)
**Comment:** Entry-assembly and process-name checks also accepted a framework-dependent `dotnetup` apphost.
**Status:** ✅ Done — `IsDirectExecution` now also requires a Native AOT runtime, identified by dynamic-code support being unavailable. Managed apphosts no longer enter self-update coordination or qualify for detached-drainer relaunch.
**Code:** [DotnetupProcessInfo.cs](dotnetup.Library/DotnetupProcessInfo.cs), [DotnetupTelemetryDrainProcessTests.cs](../../test/dotnetup.Tests/DotnetupTelemetryDrainProcessTests.cs)

### M1: NonSafeCommandGate.cs — Acquire the activity lock before opening the executable

**Link:** [r4067391115](https://github.com/dotnet/sdk/pull/55511#discussion_r4067391115)
**Comment:** Opening the executable before acquiring the shared activity lock reported an identity failure during the Windows replacement gap.
**Status:** ✅ Done — the gate validates the directory first, attempts the shared activity lock, and only then opens and queries the executable. A Windows regression test removes the canonical path while the updater owns the activity lock and verifies `DotnetupUpdateInProgress`.
**Code:** [NonSafeCommandGate.cs](dotnetup.Library/SelfUpdate/NonSafeCommandGate.cs), [NonSafeCommandGateTests.cs](../../test/dotnetup.Tests/NonSafeCommandGateTests.cs)

### Q1: SelfUpdateStartupTelemetryProcess.cs — Use the product data-directory variable

**Link:** [r4067391136](https://github.com/dotnet/sdk/pull/55511#discussion_r4067391136)
**Comment:** The child process set `DOTNET_TESTHOOK_DOTNETUP_DATA_DIR`, but the product reads `DOTNET_DOTNETUP_DATA_DIR`.
**Status:** ✅ Done — the startup telemetry child now redirects product data and the first-run sentinel to its temporary directory with `DOTNET_DOTNETUP_DATA_DIR`.
**Code:** [SelfUpdateStartupTelemetryProcess.cs](../../test/dotnetup.Tests/Utilities/SelfUpdateStartupTelemetryProcess.cs), [DotnetupPaths.cs](dotnetup.Library/DotnetupPaths.cs)

### A2: SelfUpdateCleanupTests.cs — Restore class-level test placement

**Link:** [r4075842093](https://github.com/dotnet/sdk/pull/55511#discussion_r4075842093)
**Comment:** Two test methods interrupted `RunCleanup` between its `if` and `else`, causing syntax errors.
**Status:** ✅ Done — both tests are class-scope members and `RunCleanup` has a contiguous `if/else`.
**Code:** [SelfUpdateCleanupTests.cs](../../test/dotnetup.Tests/SelfUpdateCleanupTests.cs)

## Summary

- Total comments: 5
- Already resolved: 2
- Quick fixes: 1
- Medium fixes: 2
- Large investigation items: 0
- All comments: ✅ Done

### Files Modified

- `src/Installer/dotnetup.Library/DotnetupProcessInfo.cs`
- `src/Installer/dotnetup.Library/SelfUpdate/NonSafeCommandGate.cs`
- `test/dotnetup.Tests/DotnetupTelemetryDrainProcessTests.cs`
- `test/dotnetup.Tests/NonSafeCommandGateTests.cs`
- `test/dotnetup.Tests/Utilities/SelfUpdateStartupTelemetryProcess.cs`
- `src/Installer/pr-feedback-plan.md`

### Build and Test Status

- Native AOT build with warnings as errors: passed with 0 warnings and 0 errors.
- Focused regression tests: 45 passed, 1 platform-skipped, 0 failed.
