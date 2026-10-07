# PR #56535 Feedback Resolution

## Summary

- **Total feedback items:** 9
- **Already resolved:** 6
- **Quick fixes:** 1
- **Medium fixes:** 1
- **Investigation items:** 1
- **Status:** All items complete

## Feedback in PR Order

### A1: Isolated artifact lookup

**Link:** [r4201173302](https://github.com/dotnet/sdk/pull/56535#discussion_r4201173302)

**Status:** ✅ Already resolved and outdated in the current diff.

### A2: Runtime uninstall redirected-input documentation

**Link:** [r4201173360](https://github.com/dotnet/sdk/pull/56535#discussion_r4201173360)

**Status:** ✅ Already resolved and outdated in the current diff.

### A3: SDK uninstall redirected-input documentation

**Link:** [r4201173384](https://github.com/dotnet/sdk/pull/56535#discussion_r4201173384)

**Status:** ✅ Already resolved and outdated in the current diff.

### A4: Root uninstall redirected-input documentation

**Link:** [r4201173418](https://github.com/dotnet/sdk/pull/56535#discussion_r4201173418)

**Status:** ✅ Already resolved and outdated in the current diff.

### A5: Localize the non-interactive option description

**Link:** [r4201173448](https://github.com/dotnet/sdk/pull/56535#discussion_r4201173448)

**Status:** ✅ Already resolved and outdated in the current diff.

### M1: Consolidate shared uninstall command behavior

**Link:** [r4211417997](https://github.com/dotnet/sdk/pull/56535#discussion_r4211417997)

**Status:** ✅ Done — added an uninstall command base for shared option extraction and workflow invocation, plus one parser-registration helper so SDK and runtime uninstall cannot drift when a shared option is added.

**Code:** [UninstallCommand.cs](dotnetup.Library/Commands/Shared/UninstallCommand.cs#L13), [CommonOptions.cs](dotnetup.Library/CommonOptions.cs#L134), [SdkUninstallCommand.cs](dotnetup.Library/Commands/Sdk/Uninstall/SdkUninstallCommand.cs#L9), [RuntimeUninstallCommand.cs](dotnetup.Library/Commands/Runtime/Uninstall/RuntimeUninstallCommand.cs#L9)

### L1: Validate confirmation globalization behavior

**Link:** [r4211734172](https://github.com/dotnet/sdk/pull/56535#discussion_r4211734172)

**Status:** ✅ Investigated — no code change needed. Input is handled as Unicode, Enter accepts and Escape declines without requiring Latin glyphs, and redirected input has the same default/cancel alternatives. The established localized `dotnet` confirmation implementation also uses resource-backed but consistently translated `y`/`n` bindings.

**Code:** [SpectreDisplayHelpers.cs](dotnetup.Library/Commands/Shared/SpectreDisplayHelpers.cs#L172), [SpectreDisplayHelpers.cs](dotnetup.Library/Commands/Shared/SpectreDisplayHelpers.cs#L266), [InteractiveConsole.cs](../Cli/dotnet/InteractiveConsole.cs#L41)

### Q1: Parameterize source-option guidance

**Link:** [r4211756642](https://github.com/dotnet/sdk/pull/56535#discussion_r4211756642)

**Status:** ✅ Done — the resource now accepts the current source option name and `InstallSource.All` value. Generated XLF files were updated by the build, and an end-to-end regression test verifies the rendered guidance.

**Code:** [UninstallWorkflow.cs](dotnetup.Library/Commands/Shared/UninstallWorkflow.cs#L120), [Strings.resx](dotnetup.Library/Strings.resx#L436), [UninstallEndToEndTests.cs](../../test/dotnetup.Tests/UninstallEndToEndTests.cs#L453)

### A6: Clarify unattended uninstall behavior

**Link:** [comment](https://github.com/dotnet/sdk/pull/56535#issuecomment-6044744663)

**Status:** ✅ Answered in the PR conversation.

## Files Modified

- `src/Installer/dotnetup.Library/Commands/Shared/UninstallCommand.cs`
- SDK and runtime uninstall command/parser implementations
- `src/Installer/dotnetup.Library/CommonOptions.cs`
- `src/Installer/dotnetup.Library/Commands/Shared/UninstallWorkflow.cs`
- `src/Installer/dotnetup.Library/Strings.resx` and generated XLF files
- `test/dotnetup.Tests/UninstallWorkflowTests.cs`
- `test/dotnetup.Tests/UninstallEndToEndTests.cs`

## Validation

- `dotnetup.csproj` build: ✅ 0 warnings, 0 errors
- `UninstallWorkflowTests`: ✅ 19 passed
- `SourceMismatch_GuidanceUsesCurrentOptionNameAndAllValue`: ✅ passed
