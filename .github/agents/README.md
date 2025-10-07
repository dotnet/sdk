# GitHub Copilot Agents

This directory contains custom agent instructions for GitHub Copilot to automate common maintenance tasks in the .NET SDK repository.

## Available Agents

### `/updatexlf` - Update Translation Files

Automatically updates XLF (XLIFF) translation files when .resx resource files are changed.

**When to use:** Comment `/updatexlf` on a PR when:
- You've modified `.resx` files
- CI is failing due to outdated `.xlf` files
- You want to avoid cloning the branch locally just to update translations

**What it does:**
1. Runs `msbuild /t:UpdateXlf` (or builds the repository if needed)
2. Collects all updated `.xlf` files
3. Commits the changes to your PR branch

See [updatexlf.instructions.md](updatexlf.instructions.md) for detailed agent instructions.

### `/fixcompletions` - Update CLI Completion Snapshots

Automatically updates CLI completion snapshot test files when CLI commands are modified.

**When to use:** Comment `/fixcompletions` on a PR when:
- You've added or modified CLI commands, options, or subcommands
- The `VerifyCompletions` tests are failing
- You want to update completion snapshots without building locally

**What it does:**
1. Builds the repository
2. Runs the completion tests to generate new `.received.` files
3. Runs the `CompareCliSnapshots` and `UpdateCliSnapshots` MSBuild targets
4. Commits the updated `.verified.*` snapshot files to your PR branch

See [fixcompletions.instructions.md](fixcompletions.instructions.md) for detailed agent instructions.

## How It Works

These agents are triggered when you comment the specific slash command on a pull request. GitHub Copilot will:

1. Clone the PR branch into a sandbox environment
2. Execute the instructions defined in the corresponding `.instructions.md` file
3. Use the `report_progress` tool to commit and push changes back to your PR
4. Comment on the PR to confirm completion or report any errors

## For Agent Developers

Each agent instruction file should:
- Clearly describe the trigger command and purpose
- Provide step-by-step instructions for the automation
- Include error handling and troubleshooting guidance
- Use the `report_progress` tool to commit changes
- Reference relevant documentation and example PRs

## Additional Resources

- [Developer Guide](../../documentation/project-docs/developer-guide.md#github-copilot-automation-commands)
- [Localization Guide](../../documentation/project-docs/Localization.md)
- [Snapshot Testing Guide](../../documentation/project-docs/snapshot-based-testing.md)
