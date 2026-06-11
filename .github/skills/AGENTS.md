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

## Quick Checklist

- [ ] Run `dotnet .github/skills/ValidateSkill.cs <skill-dir>` to validate format.
- [ ] `description` describes what the skill does and when to use it. Skill body does not include "When to use this skill".
- [ ] Skill does not explain things the agent already knows. Focus on what's specific to the task at hand.
- [ ] Deterministic processes use scripts (for example, to fetch and format data from an API).
- [ ] Scripts use PowerShell or .NET file-based apps, not bash.
