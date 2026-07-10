---
emoji: 🏷️
name: Issue Triage
description: Triage newly opened or edited issues with labels, assignment, and a short note
on:
  issues:
    types: [opened, edited]
engine: copilot
permissions:
  contents: read
  issues: read
  copilot-requests: write
tools:
  github:
    allowed-repos: "${{ github.repository }}"
    min-integrity: unapproved
safe-outputs:
  report-failure-as-issue: false
  add-labels:
    # dotnet/sdk classification labels. Area-* identify the affected component;
    # the type labels classify the kind of issue. Special-purpose labels
    # (needs-info, cookie, Test Debt, performance, dotnetup, ...) are added in commit 7.
    allowed:
      # Area (component) labels
      - Area-acquisition
      - Area-AIEngineering
      - Area-ApiCompat
      - Area-AspNetCore
      - Area-CLI
      - Area-ClickOnce
      - Area-CodeFlow
      - "Area-Common templates"
      - Area-Compilers
      - Area-Containers
      - "Area-dotnet AOT"
      - "Area-dotnet new"
      - "Area-dotnet test"
      - "Area-dotnet test (MTP)"
      - "Area-dotnet test (VSTest)"
      - Area-esproj
      - Area-External
      - Area-Format
      - Area-FSharp
      - Area-GenAPI
      - area-Host
      - Area-ILLink
      - Area-ImplicitUsings
      - Area-Infrastructure
      - Area-Install
      - Area-Linux
      - Area-MacOS
      - Area-Microsoft.CodeAnalysis.NetAnalyzers
      - Area-MSBuild
      - Area-NativeAOT
      - Area-NetSDK
      - Area-NuGet
      - Area-Performance
      - Area-ReadyToRun
      - Area-Roslyn
      - Area-Run
      - Area-run-file
      - area-runtime
      - Area-SBOM
      - Area-SdkResolvers
      - Area-Security
      - area-Single-File
      - Area-Snap
      - Area-SourceBuild
      - Area-SourceLink
      - Area-StaticWebAssets
      - area-System.Console
      - Area-Telemetry
      - Area-Templates
      - "Area-Test templates"
      - Area-Tooling
      - Area-Tools
      - Area-TraversalSdk
      - Area-Trimming
      - area-unified-build-BuildFailure
      - area-vendored-sync
      - Area-VMR
      - Area-VS
      - Area-WasmSdk
      - Area-Watch
      - Area-WebSDK
      - Area-WindowsSDK
      - Area-Workloads
      # Type labels
      - Bug
      - enhancement
      - "Feature Request"
      - question
      - documentation
      - Task
    max: 6
  remove-labels:
    allowed: [untriaged]
  assign-to-user:
    # TODO(commit 3-6): owners are resolved from CODEOWNERS and load-balanced; fill the allowlist then.
    allowed: []
    max: 2
  add-comment:
    max: 1
---

# Issue Triage

## Task

Read the triggering issue title and body and triage by meaning, not keyword matching.

### Labels to apply

- Apply only labels from the configured `add-labels.allowed` list.
- Apply exactly one `Area-*` component label for the area the issue is genuinely about, and a type label (`Bug`, `enhancement`, `Feature Request`, `question`, `documentation`, or `Task`) when the kind is clear.
- Apply a second `Area-*` label only if the issue genuinely spans two components.
- Ignore terms mentioned only in passing (for example in file paths, build flags, or examples).
- Understand synonyms and short forms so related terms map to the right label (for example "web assets" -> `Area-StaticWebAssets`, "global tool" / "dotnet tool" -> `Area-Tools`).
- Do not invent labels.

### Owner lookup and routing

Read the repository's root `CODEOWNERS` file to look up the owner(s) for the area you labeled. (This section is adapted from Azure/azure-sdk-for-net's triage workflow to how dotnet/sdk CODEOWNERS actually works.)

#### How dotnet/sdk CODEOWNERS is structured

`CODEOWNERS` (at the repository root) is organized into sections. Each section begins with an `# Area-<Name>` comment naming the matching `Area-*` label, followed by one or more path patterns and their owners:

```
# Area-WebSDK
/src/WebSdk/ @vijayrkn
/test/Microsoft.NET.Sdk.Publish.Tasks.Tests/ @vijayrkn

# Area-NuGet
/src/Cli/dotnet/Commands/NuGet @dotnet/nuget-team
/src/Cli/dotnet/Commands/Pack @dotnet/nuget-team
```

Owners come in two kinds:

- **Individual owners** — a plain `@login` (for example `@vijayrkn`, `@phil-allen-msft`). These can be assigned to the issue.
- **Team owners** — an `@dotnet/<team>` handle (for example `@dotnet/nuget-team`, `@dotnet/razor-tooling`). A team cannot be set as an assignee; route to the team instead.

If no section matches, the repository default owner is `@dotnet/dotnet-cli`.

#### Matching rules

1. Take the `Area-*` label you applied in the Labels step.
2. Find the `# Area-<Name>` comment in `CODEOWNERS` whose `<Name>` matches that label, case-insensitively (`# Area-WebSDK` matches the `Area-WebSDK` label).
3. Collect every owner on the path lines in that section, stopping at the next `# Area-` comment. De-duplicate.
4. Split them into individual owners (`@login`) and team owners (`@dotnet/<team>`).
5. If you applied more than one `Area-*` label, repeat for each and union the owners.

#### Owner routing flow

```
IF one or more individual owners were found for the area:
    - Assign one of them with `assign_to_user`.
      (Which individual — including load balancing so no one is overloaded —
      is defined in the "Round-robin and load balancing" section below.)
    - Record the individual owner(s) and any team owner(s) for the comment.

ELSE IF only team owner(s) were found (no individual owner in CODEOWNERS):
    - Do not assign (a team cannot be an issue assignee).
    - Add the `needs team triage` label so the owning team picks it up.
    - Record the team owner(s) for the comment.

ELSE (no CODEOWNERS section matched the area, or the area could not be determined):
    - Add the `needs team triage` label.
```

#### Owner routing note

Fold the routing note into the single triage comment (see the Comment requirement section) — do not post a second comment:

- When you assigned an individual, name them; the assignment already notifies them.
- When you routed to a team, name the team (for example "routing to the `@dotnet/nuget-team` team") so a human can follow up.

### Round-robin and load balancing

<!-- Copied verbatim from microsoft/pylance-release issue-assignment.md. Commit 6 makes it stateless (load-balanced by open-assigned count) and merges it with the expertise-based owner routing above. -->

You are an AI agent responsible for assigning newly opened issues to the next person in a team rotation.

#### Context

This repository uses a round-robin rotation to assign incoming issues. The rotation order is:

1. `bschnurr`
2. `heejaechang`
3. `StellaHuang95`
4. `rchiodo`

The current person in the rotation (i.e., who was last assigned) is tracked as the **assignee of issue #4462** in this repository.

#### Your Task

When a new issue is opened, follow these steps **exactly**:

##### Step 1: Check for skip condition

Read the labels on the newly opened issue (the triggering issue). If it has the label `skip-reassign`, call the `noop` safe output with the message "Issue has 'skip-reassign' label, skipping assignment" and stop. Do not proceed further.

##### Step 2: Find the current rotation owner

Read issue #4462 in this repository using the GitHub tools. Look at the first assignee of that issue — this is the person who was **last assigned** an incoming issue.

##### Step 3: Compute the next person

Using the rotation order listed above, find who comes **after** the current owner:

- `bschnurr` → next is `heejaechang`
- `heejaechang` → next is `StellaHuang95`
- `StellaHuang95` → next is `rchiodo`
- `rchiodo` → next is `bschnurr` (wraps around)

If issue #4462 has no assignees, or the assignee is not in the rotation list, default to `bschnurr`.

##### Step 4: Assign the new issue

Use the `assign_to_user` safe output to assign the newly opened (triggering) issue to the next person computed in Step 3.

##### Step 5: Label the new issue

Use the `add_labels` safe output to add the label `team needs to reproduce` to the triggering issue.

##### Step 6: Update the rotation state

First, use the `unassign_from_user` safe output to remove the current assignee from issue #4462.

Then, use the `assign_to_user` safe output to set the assignee of issue #4462 to the next person (the one you just assigned the new issue to in Step 4).

#### Important Notes

- The rotation is strictly: `bschnurr` → `heejaechang` → `StellaHuang95` → `rchiodo` → `bschnurr` (wraps around).
- Only modify the assignees of issue #4462 — do NOT change its title, body, or labels.
- Only modify the assignees and labels of the triggering issue — do NOT change its title or body.
- If anything is unclear, default to assigning `bschnurr`.

### `untriaged` handling

- If at least one `Area-*` (or type) label was applied OR at least one owner was assigned, remove `untriaged` with `remove_labels`.
- If nothing clearly matched, keep `untriaged` so a human still triages it.

### Comment requirement

Post one short triage comment with `add_comment` that:

- states which labels were applied (if any),
- states which owner(s) were assigned (if any),
- explicitly says when nothing clearly matched and `untriaged` was left in place.

Use `noop` only if the issue cannot be analyzed from the available title/body content.

### Do not falsely report missing or filtered content

- The triggering issue number is provided in the context above. Always read the issue title and body with the GitHub tools first.
- If that read returns a title or body, you HAVE the content: proceed to triage it. Do not stop.
- Never emit `missing_data`, and never claim the content is "filtered", "unreadable", "blocked", or "missing", when the issue read returned content.
- `missing_data` is reserved for a genuine tool or API failure where no title or body could be retrieved at all. If a read fails, retry once before concluding anything is missing.
