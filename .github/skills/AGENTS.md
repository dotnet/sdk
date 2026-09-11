# Agent Skills

When creating skills, follow:
- Agent skills specification: https://agentskills.io/specification.md
- Best practices: https://agentskills.io/skill-creation/best-practices.md

## Structure

```
.github/skills/skill-name/
├── SKILL.md          # Required: metadata + instructions
├── scripts/          # Optional: executable code
├── references/       # Optional: documentation
├── assets/           # Optional: templates, resources
└── ...               # Any additional files or directories
```

## Portable cores and overlays

When a portable skill also needs repository-specific paths, commands, examples, or policy,
keep the portable workflow in `SKILL.md` and put the SDK bindings in a sibling
`overlay.md`. The core declares the binding in its frontmatter:

```yaml
metadata:
  binding: optional-overlay
```

Use `required-overlay` only when the core cannot operate without repository bindings. Near
the top of `SKILL.md`, include the loader cue that `ValidateSkill.cs` enforces:

> If `overlay.md` exists beside this file, read it before acting; it contains
> repository-specific bindings.

An overlay starts with:

```yaml
---
core: skill-name
core-pin: immutable-tag-or-commit
---
```

`core` must match the skill directory. Use `core-pin: sdk-local` only while the SDK owns
and incubates both layers in the same repository. Replace it with the immutable source tag
or commit when the portable core moves to an external skills repository. Keep the portable
core directory limited to source-core files plus `overlay.md`: put SDK-specific
documentation and tooling under the repository area that owns them, then link to those
canonical locations from the overlay. Do not copy portable workflow rules into it.

## Quick Checklist

- [ ] Run `dotnet .github/skills/ValidateSkill.cs <skill-dir>` to validate format.
- [ ] For an overlay, verify `core`, `core-pin`, the core's `metadata.binding`, and its
      loader cue agree.
- [ ] `description` describes what the skill does and when to use it. Skill body does not include "When to use this skill".
- [ ] Skill does not explain things the agent already knows. Focus on what's specific to the task at hand.
- [ ] Deterministic processes use scripts (for example, to fetch and format data from an API).
- [ ] Scripts use PowerShell or .NET file-based apps, not bash.
