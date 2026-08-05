---
name: generate-release-notes
description: Generate .NET SDK release notes by comparing two git branches, investigating PRs for user-facing changes, and producing a structured markdown document with contributor acknowledgments. Use this when asked to create or draft release notes for a new .NET SDK release.
---

# Generate .NET SDK Release Notes

You are generating release notes for the .NET SDK by comparing two git branches.
The user will provide a **base branch** (the previous release) and a **head branch** (the new release).

## Step 1: Discover commits and PRs

1. Run `git log --oneline --no-merges <base>..<head> -- src/` to get the list of commits unique to the head branch.
2. Extract PR numbers from commit messages using the pattern `(#NNNNN)`.
3. **Filter out noise** — skip commits/PRs that are:
   - Localization updates (xlf file changes only)
   - Dependency version bumps from `dotnet-maestro[bot]` or darc
   - Backflow merges between branches
   - Infrastructure-only changes (CI, build scripts) unless they affect user-facing behavior
4. Record the remaining PR numbers as candidates for investigation.

## Step 2: Investigate each PR

For each candidate PR, fetch the PR details from `dotnet/sdk` on GitHub:

- **Title and description**: Understand what the PR does.
- **Author and `author_association`**: Record whether the author is `MEMBER`, `CONTRIBUTOR`, or `NONE`.
  - `MEMBER` = Microsoft employee or org member (has maintainer/contributor access)
  - `CONTRIBUTOR` = has previously had a PR merged but is not a member
  - `NONE` = first-time or external contributor
- **Labels**: Use area labels to help categorize.
- **Linked issues**: Follow linked issues in the PR body for specs, documentation, or upstream context.

Determine if the PR is **user-facing** — does it change CLI behavior, add new commands/options, add analyzers, change MSBuild properties/targets, or otherwise affect how developers use the SDK?

Skip PRs that are purely internal refactoring, test-only changes, or infrastructure fixes with no user-visible impact.

## Step 3: Organize into sections

Group the user-facing changes into these four major sections:

### 1. CLI command improvements

Group by the specific `dotnet` command affected (`dotnet run`, `dotnet test`, `dotnet watch`, `dotnet format`, `dotnet build`, etc.). Each command gets its own `###` subsection. Multiple related changes to the same command should be grouped under `####` sub-headings within that command's section.

### 2. Code analyzers

List new or changed analyzers in a table format:

```markdown
| Analyzer ID | Description | PR |
|-------------|-------------|-----|
| [CAXXXX](docs-link) | Short description | [#NNNNN](pr-link) |
```

### 3. MSBuild SDK properties and targets

New or changed MSBuild properties, items, or targets that expose new SDK capabilities. Include XML examples showing usage.

### 4. Other changes

Infrastructural, niche, or cross-cutting changes that don't fit the above categories but are still user-visible (e.g., encoding changes, verbosity changes).

## Step 4: Write feature descriptions

For each feature, write a concise description that includes:

- **What changed**: A clear explanation of the new behavior.
- **Why it matters**: The user scenario or pain point this addresses.
- **How to use it**: Code examples, CLI invocations, or MSBuild XML snippets as appropriate.
- **Links**: Link to the PR, and if available, link to specs (check `documentation/specs/` in the repo), linked issues, or documentation.

## Step 5: Contributor acknowledgments

### Inline thanks

For each listed feature where the PR author's `author_association` is NOT `MEMBER` (i.e., the contributor does not have maintainer/contributor org access to the SDK repo), add a thank-you line in that feature's section:

```markdown
Thank you to [@username](https://github.com/username) for this community contribution!
```

### Community contributors section

At the bottom of the document, add a `## Community contributors` section listing ALL non-member commit authors from the branch diff (not just featured PR authors). To build this list:

1. Run `git log --no-merges --format="%an|%ae" <base>..<head> -- src/` to get all commit authors.
2. For each unique author, look up their GitHub username (from noreply email or by fetching the commit from GitHub).
3. Check their `author_association` on a PR they authored in `dotnet/sdk` — exclude `MEMBER` authors.
4. Also exclude bots (`dotnet-maestro[bot]`, `github-actions[bot]`, `Copilot`).
5. List the remaining community contributors alphabetically by GitHub username.

## Step 6: Final document structure

```markdown
# .NET SDK in .NET <version> - Release Notes

<Intro paragraph>

<Table of contents with nested bullets>

## CLI command improvements
### `dotnet <command>`: Feature title
...

## Code analyzers
### New analyzers
| table |

## New .NET SDK capabilities
### Feature title
...

## Other changes
### Feature title
...

## Community contributors
- [@username](link)
...

.NET SDK updates in .NET <version>:
- [What's new in .NET <version>](docs-link) documentation
```

## Output

Create the release notes as a markdown file at `documentation/release-notes/` in the repo. Commit it on a new branch named `dev/<user>/release-notes-<version>`.
