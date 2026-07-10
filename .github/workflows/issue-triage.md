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

<!-- Copied from Azure/azure-sdk-for-net issue-triage.md (Step 5 + Step 6). Commit 4 adapts this to dotnet/sdk's CODEOWNERS; commit 5-6 add the load-balanced round-robin fallback. -->

All issues reaching this step have predicted labels and proceed through ownership routing

Read the `.github/CODEOWNERS` file to look up owners for the predicted label combination

#### CODEOWNERS Matching Rules

The CODEOWNERS file contains `# ServiceLabel:` entries that associate one or more labels with owners

```
# ServiceLabel: %<Label1>
# AzureSdkOwners:                       @owner1

# ServiceLabel: %<Label1> %<Label2>
# ServiceOwners:                        @svcowner1 @svcowner2
```

**Matching uses bottom-to-top scanning with first-match-wins semantics:**

1. Start from the END of the CODEOWNERS file and scan each line upward
2. For each `# ServiceLabel:` entry, check if ALL labels listed in it (after each `%`) are present in the issue's predicted labels
3. STOP at the first entry where all its labels match — this is the matching entry
4. Use the AzureSdkOwners and/or ServiceOwners from that entry and any adjacent owner lines

**Why this matters:** The file is structured so that more specific multi-label entries appear AFTER less specific entries. In bottom-to-top scanning, entries closer to the end of the file are encountered first. Multi-label entries placed after a catch-all are encountered before it, correctly overriding the catch-all

The following simplified excerpt illustrates the structure:

```
# --- Client libraries section (earlier in file) ---

# AzureSdkOwners:                   @jsquire
# ServiceLabel: %Event Hubs
# ServiceOwners:                    @axisc @hmlam

# --- Management catch-all ---

# ServiceLabel: %Mgmt
# AzureSdkOwners:                   @ArthurMa1978

# --- Management-specific overrides (after catch-all) ---

# ServiceLabel: %ARM %Mgmt
# ServiceOwners:                    @Azure/arm-sdk-owners

# ServiceLabel: %ARM - Templates %Mgmt
# ServiceOwners:                    @armleads-azure
```

**Example 1 — Predicted labels: "ARM" + "Mgmt"**

Scan starts from end of file upward:
1. `%ARM - Templates %Mgmt` — requires "ARM - Templates" AND "Mgmt"; issue has "ARM" not "ARM - Templates" → no match, continue
2. `%ARM %Mgmt` — requires "ARM" AND "Mgmt"; issue has both → ALL labels match ✅ STOP

The `%Mgmt` catch-all is never reached because the more specific `%ARM %Mgmt` entry was encountered first (it appears after the catch-all in the file)

**Outcome:** Matches `%ARM %Mgmt`. ServiceOwners: @Azure/arm-sdk-owners, no AzureSdkOwners. Add "Service Attention" label, no assignment, no @mention. If the issue is also tagged with the "customer-reported" label, add the "needs-team-attention" label

**Example 2 — Predicted labels: "Event Hubs" + "Client"**

Scan starts from end of file upward:
1. All management-specific entries — each requires "Mgmt" or a management service; issue has "Client" not "Mgmt" → no match for any, continue
2. `%Mgmt` catch-all — requires "Mgmt"; issue has "Client" → no match, continue
3. `%Event Hubs` — requires only "Event Hubs"; issue has "Event Hubs" → ALL labels match ✅ STOP

**Outcome:** Matches `%Event Hubs`. AzureSdkOwners: @jsquire, ServiceOwners: @axisc @hmlam. Assign @jsquire, @mention @jsquire in Step 6 comment. If the issue is also tagged with the "customer-reported" label, add the "needs-team-attention" label

Note: There is no `%Client` catch-all entry in CODEOWNERS, so "Client" as a category label does not contribute to CODEOWNERS matching. The service label drives the match

#### Owner Routing Flow

```
IF a matching ServiceLabel entry is found in CODEOWNERS:

    IF AzureSdkOwners are listed for the matched entry:
        IF a single AzureSdkOwner:
            - Assign them to the issue using the `assign_to_user` tool
        ELSE (multiple AzureSdkOwners):
            - Pick one AzureSdkOwner at random and assign them using the `assign_to_user` tool

        - IF the issue has the "customer-reported" label: Add the "needs-team-attention" label
        - Record all AzureSdkOwners for Step 6

    ELSE IF only ServiceOwners are listed (no AzureSdkOwners):
        - Add the "Service Attention" label
        - IF the issue has the "customer-reported" label: Add the "needs-team-attention" label
        - Leave the issue unassigned
        - Record all ServiceOwners for Step 6

    ELSE (matched entry has neither AzureSdkOwners nor ServiceOwners):
        - Add the "needs-team-triage" label

ELSE (no ServiceLabel entry matches any of the issue's predicted labels):
    - Add the "needs-team-triage" label
```

#### Owner Routing Comment

Post a routing comment before the analysis comment. The comment type depends on who was identified in Step 5:

- For **multiple AzureSdkOwners** or **ServiceOwners**: use `mention_owners` to preserve @mentions as real pings
- For a **single AzureSdkOwner**: use `add_comment` with just the routing message (no @mentions needed — the assignment already notifies them)

**When using `mention_owners`:** Pass owner names in the `owners` field WITHOUT the @ prefix; the `mention_owners` job prepends @ on the server side to avoid safe-outputs sanitization. Never include @ symbols in any `mention_owners` tool parameter

This comment should be concise: a brief routing message only; no analysis or debugging detail

```
IF a single AzureSdkOwner was identified in Step 5:
    - Use `add_comment` with body: "Thank you for your feedback. Tagging and routing to the team member(s) best able to assist."

ELSE IF multiple AzureSdkOwners were identified in Step 5:
    - Use `mention_owners` with:
        message: "Thank you for your feedback. Tagging and routing to the team member(s) best able to assist."
        owners: "owner1, owner2"

ELSE IF ServiceOwners were identified in Step 5 (Service Attention path):
    - Use `mention_owners` with:
        message: "Thank you for your feedback. Tagging and routing to the team member(s) best able to assist."
        owners: "owner1, owner2"

ELSE:
    - Skip this step
```

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
