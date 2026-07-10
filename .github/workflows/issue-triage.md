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
    allowed:
      - OS-Linux
      - OS-MacOS
      - OS-Windows
      - Container-Process
      - Container-MicroVM
      - Container-VM
      - Container-WSLC
      - Container-Session
      - Container-Hyperlight
      - Area-SDK-Configuration
      - Area-SDK-Policy
      - Area-Executor-Schema
      - Area-SDK-Api
      - Area-Executor-LXC
      - Area-Executor-WXC
      - Area-Build-Rust
      - Area-Build-TypeScript
      - Area-Test-SDK
      - Area-Test-Executor
    max: 6
  remove-labels:
    allowed: [Needs-Triage]
  assign-to-user:
    allowed:
      - jsidewhite
      - mgudgin
      - SohamDas2021
      - bbonaby
      - huzaifa-d
      - adpa-ms
      - richiemsft
    max: 2
  add-comment:
    max: 1
---

# Issue Triage

## Task

Read the triggering issue title and body and triage by meaning, not keyword matching.

### Labels to apply

- Apply only labels from the configured `add-labels.allowed` list.
- Apply the OS-*, Container-*, and Area-* labels the issue is genuinely about.
- Ignore terms mentioned only in passing (for example in file paths, build flags, or examples).
- Understand synonyms and short forms. Treat "Mac", "Mac x64", "Mac ARM", "Mac aarch64", "macOS", "darwin", and "Seatbelt" as the macOS backend.
- Do not invent labels.

### Owner assignment map

Assign matching owner(s) with `assign_to_user` using this map:

| Owner (login) | Area |
|---|---|
| @jsidewhite | AppContainer / BaseContainer / process isolation/container |
| @mgudgin | AppContainer / BaseContainer / process isolation/container |
| @bbonaby | AppContainer / BaseContainer / process isolation/container / networking / firewall / DNS / proxy / iptables |
| @SohamDas2021 | Linux / LXC / WSLC / Bubblewrap (bwrap) / proxy on Linux / iptables |
| @huzaifa-d | MicroVM / NanVix / Hyperlight / Windows Sandbox |
| @adpa-ms | IsolationSession / session isolation |
| @richiemsft | macOS / Seatbelt |

Rules:

- Assign more than one owner only if the issue genuinely spans multiple areas.
- If nothing fits, assign no one.

### Needs-Triage handling

- If at least one OS/Container/Area label was applied OR at least one owner was assigned, remove `Needs-Triage` with `remove_labels`.
- If nothing clearly matched, keep `Needs-Triage`.

### Comment requirement

Post one short triage comment with `add_comment` that:

- states which labels were applied (if any),
- states which owner(s) were assigned (if any),
- explicitly says when nothing clearly matched and `Needs-Triage` was left in place.

Use `noop` only if the issue cannot be analyzed from the available title/body content.

### Do not falsely report missing or filtered content

- The triggering issue number is provided in the context above. Always read the issue title and body with the GitHub tools first.
- If that read returns a title or body, you HAVE the content: proceed to triage it. Do not stop.
- Never emit `missing_data`, and never claim the content is "filtered", "unreadable", "blocked", or "missing", when the issue read returned content.
- `missing_data` is reserved for a genuine tool or API failure where no title or body could be retrieved at all. If a read fails, retry once before concluding anything is missing.
