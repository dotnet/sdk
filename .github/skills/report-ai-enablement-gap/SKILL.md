---
name: report-ai-enablement-gap
description: >-
  Report missing test or diagnostic support that blocks verification or failure
  investigation. Use when encountering a gap during any task, including missing test
  hooks, inaccessible runtime state, missing build or CI artifacts, or output that
  cannot explain a failure.
license: MIT
---

# Report an AI-enablement gap

For PR build or CI failures, first follow the
[PR validation failure guidance](../../../AGENTS.md#investigating-pr-validation-failures).

1. Unless the task requires it, do not expand the change to build the missing infrastructure.
2. Use temporary logging or other local workarounds when needed for investigation. Unless
   the workaround is the intended fix, exclude it from the final change.
3. Search for an existing issue covering the gap.
4. In the final handoff, describe what you needed to observe or control, what was missing,
   any workaround used, and the supporting file, command, or log. Link a matching issue if
   one exists; otherwise, provide a ready-to-file issue title and body for the
   `Area-AIEngineering` label.
5. File a new issue only when the user asks.
