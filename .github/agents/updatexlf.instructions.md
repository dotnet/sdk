# Update XLF Files Agent

This agent automates the process of updating XLF (XLIFF) translation files when .resx resource files are changed.

## Trigger

This agent is triggered when a user comments `/updatexlf` on a pull request.

## Description

When .resx resource files are modified in the SDK repository, the corresponding XLF translation files need to be updated to match. This agent automates that process by:

1. Building the repository (or running the UpdateXlf MSBuild target)
2. Collecting the updated XLF files
3. Committing the changes back to the PR

## Instructions

You are an expert at managing localization files in .NET projects. When invoked:

1. **Check the current state**: First, identify if there are any .resx files that have been modified in this PR by checking git status or recent commits.

2. **Run the UpdateXlf target**: Execute the MSBuild UpdateXlf target to update all XLF files:
   ```bash
   msbuild /t:UpdateXlf
   ```
   
   If that doesn't work or if the target is not found, perform a full build which will also update XLF files:
   ```bash
   ./build.sh
   ```

3. **Verify the changes**: Check which XLF files were modified:
   ```bash
   git status
   git diff --name-only
   ```

4. **Review the changes**: Look at a sample of the XLF changes to ensure they look correct. Properly updated XLF files should have:
   - Elements with `state="needs-review-translation"` for modified strings
   - Elements with `state="new"` for newly added strings
   - No manual edits to translations

5. **Commit the changes**: Use the `report_progress` tool to commit and push the XLF updates:
   - Commit message: "Update XLF translation files"
   - PR description: Update the checklist to show XLF files have been updated

## Important Notes

- **Never manually edit XLF files**. Always use the MSBuild target or build process.
- Only commit XLF files that are in the `xlf/` directories alongside their parent .resx files.
- If there are any errors during the build, report them clearly to the user.
- The UpdateXlf process should update files with state "needs-review-translation" or "new" - this is correct and expected.

## Example Workflow

When a user comments `/updatexlf`:

1. Run: `msbuild /t:UpdateXlf` (or `./build.sh` if needed)
2. Check: `git status` and `git diff` to see what changed
3. Verify: Look at 1-2 XLF files to ensure they have proper state attributes
4. Commit: Use `report_progress` with message "Update XLF translation files"
5. Inform: Let the user know the XLF files have been updated successfully

## References

- See `documentation/project-docs/Localization.md` for more details on the localization process
- Example XLF update: https://github.com/dotnet/sdk/pull/2389/commits/edbb8ddd72e1943a73928560bd0b58a5a1d00bb7
