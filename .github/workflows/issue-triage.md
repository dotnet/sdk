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

### Owner assignment

<!-- TODO(commit 3-4): replace with CODEOWNERS-based owner lookup for dotnet/sdk. -->
<!-- TODO(commit 5-6): add load-balanced round-robin fallback. -->

Rules:

- Assign more than one owner only if the issue genuinely spans multiple areas.
- If nothing fits, assign no one.

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
