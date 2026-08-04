# PR #55596 feedback plan

## Summary

- Total comments: 3
- Already resolved: 1
- Quick fixes: 2
- Status: all addressed

## Comments

### R1: Remove obsolete install-path hook

**Link:** [r3714882418](https://github.com/dotnet/sdk/pull/55596#discussion_r3714882418)

**Status:** Done before this pass. Removed `DOTNET_TESTHOOK_DEFAULT_INSTALL_PATH` from the migration E2E child environment.

### Q1: Correct dotnetup executable paths in Unix env examples

**Link:** [r3714882480](https://github.com/dotnet/sdk/pull/55596#discussion_r3714882480)

**Status:** Done. The Bash, Fish, and PowerShell examples now use `/home/user/.dotnetup` for the executable directory while retaining `/home/user/.local/share/dotnetup/dotnet` as `DOTNET_ROOT`.

### Q2: Make README setup output consistent

**Link:** [r3714882520](https://github.com/dotnet/sdk/pull/55596#discussion_r3714882520)

**Status:** Done. Updated the setup banner to `0.2.0-dev` and the install output to the new default Windows hive.

## Files modified

- [documentation/general/dotnetup/designs/unix-environment-setup.md](../../documentation/general/dotnetup/designs/unix-environment-setup.md)
- [documentation/general/dotnetup/README.md](../../documentation/general/dotnetup/README.md)
- [src/Installer/pr-feedback-plan.md](pr-feedback-plan.md)

## Validation

- Markdown diagnostics: no errors
- `git diff --check`: passed
- Stale path/version search: no matches