# Issue triage routing credentials

The issue triage workflow expands CODEOWNERS teams and measures candidate issue loads before the agent runs. GitHub's repository `GITHUB_TOKEN` cannot enumerate private organization teams, so this preprocessing uses a dedicated PAT pool.

## Configuration

Create the `codeowners-pat-pool` GitHub Actions environment and add one or more environment secrets:

- `CODEOWNERS_PAT_0`
- `CODEOWNERS_PAT_1`
- `CODEOWNERS_PAT_2`

Use fine-grained PATs owned by members of the `dotnet` organization. Each PAT needs organization **Members: read** and repository **Issues: read** access to `dotnet/sdk`. A classic PAT requires `read:org` and repository access.

The workflow randomly selects one configured PAT for each run. This distributes use and permits manual replacement without downtime; it does not renew or rotate expired PATs automatically.

## Security boundary

PAT values are available only to the pool selector and the two `gh api` fetch steps. Checkout uses `persist-credentials: false`. CODEOWNERS, paginated team responses, and issue-load responses are parsed by standalone Node.js processes invoked through `env -i`. The parsers reject credential-like environment variables and emit a validated, size-bounded JSON snapshot for the agent.

The workflow fails closed when no PAT is configured, a team is not visible to the selected PAT, an API response is malformed, or routing data exceeds its bounds.
