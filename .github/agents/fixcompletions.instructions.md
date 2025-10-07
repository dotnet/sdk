# Fix Completions Tests Agent

This agent automates the process of updating CLI completions snapshot test files when completion outputs change.

## Trigger

This agent is triggered when a user comments `/fixcompletions` on a pull request.

## Description

The dotnet CLI has snapshot-based tests for command completions across different shells (bash, zsh, powershell, etc.). When CLI commands are modified (new commands, options, or changes), the snapshot files need to be updated. This agent automates that process by:

1. Building the repository
2. Running the completion tests to generate new `.received.` files
3. Comparing and updating the `.verified.` snapshot files
4. Committing the changes back to the PR

## Instructions

You are an expert at managing snapshot-based tests in .NET projects. When invoked:

### Step 1: Build the Repository

First, ensure the repository is built with the latest changes:

```bash
./build.sh
```

This may take 5-10 minutes. Use `async=false` with a timeout of at least 600 seconds (10 minutes) for the build command.

### Step 2: Run the Completion Tests

Run the specific completion snapshot tests to generate the new `.received.` files:

```bash
# Use the repo-local dotnet
./.dotnet/dotnet test test/dotnet.Tests/dotnet.Tests.csproj --filter "FullyQualifiedName~VerifyCompletions"
```

This will run the `VerifyCompletions` test which generates completion snapshots for all supported shells. Allow at least 300 seconds (5 minutes) for this command.

**Important**: The tests will likely FAIL if there are differences between current and expected snapshots. This is expected and correct behavior - don't be alarmed by test failures.

### Step 3: Compare the Snapshots

Run the `CompareCliSnapshots` MSBuild target to copy the `.received.` files from the artifacts directory to the test project:

```bash
cd test/dotnet.Tests
dotnet restore /t:CompareCliSnapshots
```

Or from the repository root:

```bash
dotnet restore test/dotnet.Tests/ /t:CompareCliSnapshots
```

### Step 4: Review the Changes

Before accepting the changes, review them to ensure they make sense:

```bash
git status
git diff test/dotnet.Tests/CompletionTests/snapshots/
```

Look for:
- New command options that were added
- Removed options (if commands were deprecated)
- Changed command descriptions
- New subcommands

If the changes are obviously incorrect or the diff shows unexpected modifications, report them to the user. Otherwise, proceed with updating the snapshots - the human reviewer of the PR will verify the changes are appropriate.

### Step 5: Update the Verified Snapshots

If the changes look correct, run the `UpdateCliSnapshots` target to rename `.received.` files to `.verified.`:

```bash
cd test/dotnet.Tests
dotnet restore /t:UpdateCliSnapshots
```

Or from the repository root:

```bash
dotnet restore test/dotnet.Tests/ /t:UpdateCliSnapshots
```

This will move/rename all `.received.*` files to `.verified.*` in the `test/dotnet.Tests/CompletionTests/snapshots/` directory.

### Step 6: Commit the Changes

Use the `report_progress` tool to commit and push the snapshot updates:

- Commit message: "Update CLI completions snapshots"
- PR description: Update the checklist to show completion snapshots have been updated

### Step 7: Verify Final State

After committing, verify that:
1. All `.received.` files have been renamed to `.verified.`
2. No `.received.` files remain in the repository
3. Only `.verified.*` files are committed

## Important Notes

- **Always build before running tests** - the tests need the latest built CLI tools
- **Test failures are expected** when snapshots don't match - this is not an error
- **Review changes carefully** - snapshot updates should match the PR's intended changes
- The snapshots are in: `test/dotnet.Tests/CompletionTests/snapshots/`
- Each shell has its own subdirectory: bash, zsh, pwsh, etc.
- Only commit `.verified.*` files, never commit `.received.*` files

## Troubleshooting

If the build fails:
- Check the error messages carefully
- Some CI-only failures are expected and can be ignored if they're unrelated to your changes
- Report any blocking build errors to the user

If tests timeout:
- The completion tests can be slow - allow at least 5 minutes
- If tests hang, try running them again

If no `.received.` files are generated:
- Make sure the tests actually ran and detected differences
- Check that the build completed successfully
- The tests might have passed (no changes needed)

## Example Workflow

When a user comments `/fixcompletions`:

1. Build: `./build.sh` (with 600s timeout)
2. Test: `./.dotnet/dotnet test test/dotnet.Tests/dotnet.Tests.csproj --filter "FullyQualifiedName~VerifyCompletions"`
3. Compare: `dotnet restore test/dotnet.Tests/ /t:CompareCliSnapshots`
4. Review: `git diff test/dotnet.Tests/CompletionTests/snapshots/` - verify changes are correct
5. Update: `dotnet restore test/dotnet.Tests/ /t:UpdateCliSnapshots`
6. Commit: Use `report_progress` with message "Update CLI completions snapshots"
7. Inform: Let the user know the snapshots have been updated successfully

## References

- See `documentation/project-docs/snapshot-based-testing.md` for more details
- Example snapshot update: https://github.com/dotnet/sdk/pull/50999/commits/2d10a8f601a0adf52b98eba279670280f3740b40
- Test file: `test/dotnet.Tests/CompletionTests/DotnetCliSnapshotTests.cs`
- MSBuild targets: `test/dotnet.Tests/dotnet.Tests.csproj` (lines 101-113)
