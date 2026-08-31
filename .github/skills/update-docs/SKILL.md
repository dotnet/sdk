---
name: update-docs
description: >-
  Keep dotnet/sdk contributor guidance and agent memory synchronized after code changes.
  Run at the end of tasks that modify code, move files, change public contracts, alter
  tests or workflows, establish conventions, or reveal durable repository gotchas.
license: MIT
---

# Update docs

Run a documentation pass after the implementation and validation steps of a code-changing
task. Update only artifacts affected by the change; an internal change with no
contributor-, user-, or agent-visible impact needs no documentation edit.

## Check the knowledge base

**Architecture or ownership changed?** Update
`.github/memory/ARCHITECTURE.md` for component boundaries, process entry points, major
abstractions, or build/data flow.

**Files or responsibilities moved?** Update `.github/memory/FILE_MAP.md` when a
significant path is added, removed, renamed, repurposed, or changes dependency direction.

**The public-surface map changed?** Update `.github/memory/API_MAP.md` when a surface
category, ownership boundary, authoritative registry, or extension-point location changes.
Individual commands, options, properties, targets, diagnostics, templates, analyzers, or
library members normally update their owning code/help/tests, not this non-exhaustive map,
unless the map's summary becomes stale.

**A repository-wide rule or pattern changed?** Update
`.github/memory/CONVENTIONS.md`. Keep area-only guidance in the nearest `AGENTS.md` or
skill and link to it from memory only when it is important for orientation.

**Agent guidance changed?** Keep `.github/agents/*.agent.md` focused on role, boundaries,
and orchestration. Link to repository memory, the nearest `AGENTS.md`, and owning skills
instead of copying their maps, invariants, commands, or procedures.

**Test strategy changed?** Update `.github/memory/TESTING_STRATEGY.md` for test
platform architecture or canonical documentation ownership. Test selection, execution,
and product-layout freshness belong in `run-tests`; authoring and Helix-safe test
conventions belong in `test/AGENTS.md`; conditional-filtering details belong in
`test/ConditionalTests.props` and its design documentation.

**A durable gotcha or workaround was found or removed?** Update
`.github/memory/KNOWN_ISSUES.md`. Do not use it as a list of transient failures or open
bugs.

**Memory files changed?** Update `.github/memory/INDEX.md` when a file is added, removed,
renamed, or changes purpose. Its loading map must list exactly the files that exist.

## Check other documentation

Review the affected area for:

- `.github/copilot-instructions.md` and the nearest `AGENTS.md`
- `.github/skills/*/SKILL.md`, `.claude/skills/*/SKILL.md`, and `.github/agents/*.agent.md`
- contributor and area guidance under `documentation/`
- command help, usage text, completion and Verify snapshots

## Create a memory file only when warranted

Create a focused memory file when the knowledge:

- is durable enough to remain useful after the current task
- is likely to be reused across tasks or repository areas
- forms a coherent subject that does not fit an existing memory file
- is scoped narrowly enough for `INDEX.md` to say when agents should load it without
  pulling unrelated context
- can be grounded in stable primary sources

Do not create a memory file for task status, investigation notes, transient failures,
open-issue tracking, or a single implementation detail. Put area-specific rules in the
nearest `AGENTS.md`, repeatable procedures in a skill, and contributor-facing guidance
under `documentation/`.

## Ground and validate updates

1. Link important claims to the nearest primary repository source.
2. Cross-check memory against current code; do not copy a stale statement forward.
3. Mark inference or uncertainty instead of presenting it as established behavior.
4. Keep files focused and remove placeholders.
5. Give new memory files only `coverage:` frontmatter that states their precise scope,
   plus a descriptive filename.
6. Confirm every local Markdown link in each changed Markdown file resolves to an
   existing file and, when present, an existing heading anchor. When memory changes,
   validate all `.github/memory/*.md` files because their cross-references form one
   knowledge base.
