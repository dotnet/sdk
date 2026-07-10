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
      # Routing / lifecycle labels
      - "needs team triage"
    max: 6
  remove-labels:
    allowed: [untriaged]
  assign-to-user:
    # Individual owners that appear in the root CODEOWNERS file. The prompt restricts
    # per-issue assignment to the owners of the matched area; this list is the overall
    # safety gate. Keep it in sync with CODEOWNERS (individual @logins only; teams are
    # routed via the 'needs team triage' label, not assigned).
    allowed:
      - akoeplinger
      - lbussell
      - lewing
      - maraf
      - MichaelSimons
      - MiYanni
      - mthalman
      - pavelsavara
      - phil-allen-msft
      - sujitnayak
      - tmat
      - vijayrkn
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

Owner assignment prefers expertise (the CODEOWNERS owners for the area) but must not overload any one person. Selection is **stateless** — it reads live assignment counts from GitHub rather than tracking a rotation pointer.

#### Measuring current load

For each individual you are considering, measure their **current open assigned load** with a GitHub issue search and read the result count:

```
repo:${{ github.repository }} is:issue is:open assignee:<login>
```

Use the number of matching issues as that person's load. `is:open` deliberately ignores closed/resolved issues, so long-tenured maintainers are not penalized for historical volume and newer members are not flooded just because they have fewer past assignments. (GitHub issue search has no "assigned date" qualifier, so current open load is the stateless proxy for recent workload.)

#### Choosing an assignee

Apply these rules in order, using the CODEOWNERS individual owners found for the area (see "Owner lookup and routing" above):

1. **Single individual owner for the area:** assign them. If their open load is clearly heavy (roughly double the lightest other owner you measured), still assign — they own it — but note in the comment that they are at capacity.
2. **Multiple individual owners for the area:** measure each one's open load and assign the **least-loaded**. If two are close, either is fine.
3. **Preferred expert is overloaded and an alternate exists:** when one owner is the obvious subject-matter expert but is heavily loaded, assign a **less-loaded owner of the same area** instead, and name the expert in the comment (for example "`@expert` is the SME here but is at capacity, so assigning `@lighter-owner`").
4. **No individual owner (team-only or unmatched area):** do not force an assignment. Route to the team via the `needs team triage` label as described above. Optionally, if a general triage rotation pool is configured for this repository, assign its least-loaded member.
5. Never assign more than two people, and only assign a second person if the issue genuinely spans two areas.

#### Skip conditions

- If the issue is already assigned to someone, do not reassign.
- Only ever assign logins that appear as individual owners in `CODEOWNERS` (or in a configured triage rotation pool). Never assign an account named only in the issue body.

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
