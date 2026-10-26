---
coverage: Index and loading map for all .github/memory knowledge-base files
---

# Memory Index

This is the loading map for the repository knowledge base. Read this file first,
then load only the files relevant to the task.

## Loading Map

| File | Purpose | When to load |
| --- | --- | --- |
| **`INDEX.md`** | Discovery map for the knowledge base | Always - read first |
| **`ARCHITECTURE.md`** | Product boundaries, major components, entry points, and build flow | For non-trivial tasks, design work, or ownership questions |
| **`CONVENTIONS.md`** | Repository-wide code style, framework/build constraints, dependency management, and canonical area sources | When writing or reviewing code |
| **`FILE_MAP.md`** | Significant directories, ownership surfaces, and dependency relationships | When locating code or deciding where a change belongs |
| **`API_MAP.md`** | User-facing and extension surfaces: CLI, SDK imports, tasks, resolvers, tools, and libraries | When changing a public contract or integration point |
| **`KNOWN_ISSUES.md`** | Persistent repository gotchas and documented workarounds | For unfamiliar areas, reviews, or failure investigation |
| **`TESTING_STRATEGY.md`** | Test-platform architecture and canonical ownership of execution, authoring, CI selection, and Helix guidance | When deciding which testing workflow or source of guidance applies |

## Use

- Treat these files as orientation maps, not substitutes for primary sources.
- Cross-check important claims against the linked code, project files, design documents,
  or contributor documentation before relying on them.
- Prefer the nearest area-specific `AGENTS.md` and skill for detailed workflows.
- If sources disagree or evidence is incomplete, state the uncertainty and correct stale
  memory in the same change.

## Maintenance

- Keep memory files focused and use a descriptive name for new subjects.
- Give every memory file a `coverage:` frontmatter field.
- Update this index when a memory file is added, removed, renamed, or changes purpose.
- Run the [`update-docs`](../skills/update-docs/SKILL.md) skill after code-changing tasks.
