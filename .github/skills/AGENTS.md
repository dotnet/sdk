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

## Validation

[`ValidateSkill.cs`](ValidateSkill.cs) checks `SKILL.md` frontmatter and length against the specification.
The [Validate Agent Skills](../workflows/validate-skills.yml) workflow runs it on every directory under
`.github/skills` and `.claude/skills` for pull requests that change them, and fails the check on any error.
To reproduce locally with the repo-pinned SDK, run `./.dotnet/dotnet .github/skills/ValidateSkill.cs <skill-dir>`
(install it with `./restore.sh` or `restore.cmd` if `.dotnet` is missing).

## Quick Checklist

- [ ] Run `./.dotnet/dotnet .github/skills/ValidateSkill.cs <skill-dir>` to validate format. CI enforces this for `.github/skills` and `.claude/skills`.
- [ ] `description` describes what the skill does and when to use it. Skill body does not include "When to use this skill".
- [ ] Skill does not explain things the agent already knows. Focus on what's specific to the task at hand.
- [ ] Deterministic processes use scripts (for example, to fetch and format data from an API).
- [ ] Scripts use PowerShell or .NET file-based apps, not bash.
