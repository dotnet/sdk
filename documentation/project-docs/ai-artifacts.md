# AI artifacts: ownership, changes, and retirement

AI artifacts are repository files that tell AI agents and agentic automation how to work
in this repository. Agents act on them as instructions, so stale or wrong guidance
directly produces wrong changes. Treat AI artifacts like source code: they have owners,
get reviewed, are validated, and are deleted when they no longer earn their keep.

## What counts as an AI artifact

| Artifact | Location | Notes |
| --- | --- | --- |
| Repository instructions | [`AGENTS.md`](../../AGENTS.md) | Repository-wide entry point for agents. |
| Area instructions | `AGENTS.md` in an area directory, such as [`src/Cli/AGENTS.md`](../../src/Cli/AGENTS.md) or [`test/AGENTS.md`](../../test/AGENTS.md) | Area-specific rules that add to the root instructions. [`eng/common/AGENTS.md`](../../eng/common/AGENTS.md) comes from Arcade; change it in [dotnet/arcade](https://github.com/dotnet/arcade). |
| Repository memory | [`.github/memory/`](../../.github/memory/INDEX.md) | Orientation knowledge base. [`INDEX.md`](../../.github/memory/INDEX.md) is the loading map, and each file declares its scope in `coverage:` frontmatter. |
| Skills | `.github/skills/*/SKILL.md` and `.claude/skills/*/SKILL.md` | Repeatable task procedures. Authoring rules are in [`.github/skills/AGENTS.md`](../../.github/skills/AGENTS.md). |
| Custom agents | `.github/agents/*.agent.md` | Role, boundaries, and orchestration for a named agent. |
| Agentic workflows | `.github/workflows/*.md` and `.github/workflows/shared/*.md` | gh-aw workflow sources. The matching `.lock.yml` files are generated. |
| Copilot settings | [`.github/copilot/settings.json`](../../.github/copilot/settings.json) | Plugin marketplaces and announcements for Copilot CLI users. |

Each artifact type has one job. Before adding content, find the layer that already owns
the subject: the [`update-docs` skill](../../.github/skills/update-docs/SKILL.md)
describes what belongs in memory, `AGENTS.md`, skills, agents, and contributor
documentation. Link to the owning artifact instead of copying its content.

## Ownership

[`CODEOWNERS`](../../CODEOWNERS) is the source of truth. Its `# AI` section assigns
`.github/`, `.claude/`, `.agents/`, and every `AGENTS.md` and `SKILL.md` file to the
AI artifact owners. The `AGENTS.md` and `SKILL.md` patterns match at any depth and
follow the area entries, so they also take precedence for area instruction files.

The area team owns the domain knowledge in an area-specific artifact, such as an area
`AGENTS.md` or a skill for one component. When you change one, also request a review
from the CODEOWNERS of the area's source paths so that a domain expert confirms the
guidance is correct.

## Requesting or proposing a change

Anyone can propose a change to an AI artifact, including contributors who do not own it
and agents working on a task.

- **Open a pull request** when you know the fix, such as a wrong command, a stale path,
  or a missing rule. A small correction does not need an issue first.
- **Open an issue** when you found a problem but not the fix, or when you propose a new
  skill, agent, or agentic workflow or a change that spans several areas. Name the
  artifact, describe what an agent did wrong or what is missing, and link the evidence,
  such as a PR, review comment, or failed run. Apply the `Area-AIEngineering` label, or
  ask in the issue for it to be applied if you cannot set labels.
- **Fix it in the same PR** when your code change makes an artifact stale. The root
  instructions require the
  [`update-docs` checklist](../../AGENTS.md#keeping-ai-context-and-docs-in-sync)
  before completion, and the
  [`code-review` skill](../../.github/skills/code-review/SKILL.md) flags stale or
  ungrounded AI context.

Turning recurring review feedback into instructions, tests, analyzer rules, or skills
is tracked as a separate item in
[dotnet/sdk#55185](https://github.com/dotnet/sdk/issues/55185).

## Review expectations

Get approval from an owner listed in [`CODEOWNERS`](../../CODEOWNERS) for each changed
artifact, plus an area owner for area-specific guidance. Reviewers check that:

- **Claims are grounded.** Important architecture, product, and ownership claims link to
  primary sources, as required by
  [Grounding architecture and product claims](../../AGENTS.md#grounding-architecture-and-product-claims).
- **Content lives in one place.** The change puts content in the owning layer and links
  to existing guidance instead of duplicating it.
- **Guidance is actionable and scoped.** It states what is specific to this repository,
  not general knowledge an agent already has, and does not conflict with the root
  [guardrails](../../AGENTS.md#guardrails).
- **The change was validated** as described in the next section.

## Validation

- **Markdown links.** Every local link must resolve to an existing file and heading. The
  [`check-markdown-links` workflow](../../.github/workflows/check-markdown-links.yml)
  checks Markdown files on pull requests, except paths it ignores such as
  `.github/workflows` and `eng/common`. Check links in ignored paths manually.
- **Skills.** Run `dotnet .github/skills/ValidateSkill.cs <skill-dir>` and follow the
  checklist in [`.github/skills/AGENTS.md`](../../.github/skills/AGENTS.md).
- **Memory.** Keep [`INDEX.md`](../../.github/memory/INDEX.md) listing exactly the
  memory files that exist, and give each file accurate `coverage:` frontmatter.
- **Agentic workflows.** Regenerate the `.lock.yml` with `gh aw compile` and commit it
  with the source change; never hand-edit it. Compile on Linux or macOS
  ([known issue](../../.github/memory/KNOWN_ISSUES.md#gh-aw-compile-must-run-on-linux-or-macos-never-windows)).
- **Behavior.** For a new or materially changed skill, agent, or workflow, try it on at
  least one representative task and summarize the result in the PR description.

## Periodic review

Most drift is caught when a change makes an artifact stale: the
[`update-docs` checklist](../../AGENTS.md#keeping-ai-context-and-docs-in-sync), the
[`code-review` skill](../../.github/skills/code-review/SKILL.md), and the Markdown link
check. These checks do not catch artifacts that drift without a related change, or that
gradually duplicate each other.

> [!NOTE]
> The following cadence is proposed. The AI artifact owners in
> [`CODEOWNERS`](../../CODEOWNERS) have not committed to it yet.

Proposed: once per quarter, the AI artifact owners review all AI artifacts for:

- claims, commands, and paths that no longer match the repository
- content that is duplicated across artifacts instead of linked
- artifacts that meet a [retirement](#retiring-an-artifact) condition
- a memory [`INDEX.md`](../../.github/memory/INDEX.md) that does not match the memory files

Each finding becomes a fix PR or an `Area-AIEngineering` issue.

## Retiring an artifact

Retire an artifact when it:

- describes code, commands, or workflows that no longer exist
- duplicates or has been superseded by another artifact
- repeatedly leads agents to wrong results and cannot be corrected
- has no owner who can confirm that it is still accurate

To retire it:

1. Delete the artifact instead of leaving a deprecated copy. Agents can still load and
   follow a deprecated file, and Git history preserves the content.
2. In the same PR, remove or redirect every reference to it: root and area `AGENTS.md`
   files, memory files and [`INDEX.md`](../../.github/memory/INDEX.md), other skills and
   agents, workflows, and contributor documentation. Search for the artifact's name and
   path.
3. For an agentic workflow, delete the source `.md` and its generated `.lock.yml`
   together, and tell the owners about any secrets or labels that only it used.
4. In the PR description, explain why the artifact is retired and what replaces it, if
   anything.

If you are not sure whether an artifact should be retired, open an `Area-AIEngineering`
issue that proposes the retirement and let the owners decide.
