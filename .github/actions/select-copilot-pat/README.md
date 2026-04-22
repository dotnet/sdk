# Select Copilot PAT

Selects a random Copilot PAT from a numbered pool of secrets. This addresses
limitations that arise from having a single PAT shared across all workflows
that call the Copilot API, such as rate-limiting.

> **This is a stop-gap workaround.** As soon as organization/enterprise billing
> is offered for agentic workflows, this approach will be removed.

Based on the pattern established in [dotnet/runtime#126057](https://github.com/dotnet/runtime/pull/126057).

## Repository Onboarding

1. Copy this `select-copilot-pat` folder into the repository under
   `.github/actions/select-copilot-pat`, including both the `README.md`
   and `action.yml`.
2. Add repository secrets named `COPILOT_PAT_0` through `COPILOT_PAT_9`
   (you only need as many as you have team members contributing PATs).
3. Reference the action in your workflow (see Usage below).

## PAT Creation

[Use this link to prefill the PAT creation form with the required settings][create-pat]:

1. **Resource owner** is your **user account**, not an organization.
2. **Copilot Requests (Read)** must be the only permission granted.
3. **8-day expiration** must be used, which enforces a weekly renewal.
4. **Repository access** set to **Public repositories** only.

Team members providing PATs should set weekly recurring reminders to
regenerate and update their PATs in the repository secrets.

PATs are added through **Settings > Secrets and variables > Actions**,
saved as **Repository secrets** matching the `COPILOT_PAT_<0-9>` naming
convention. This can also be done using the GitHub CLI:

```sh
gh secret set "COPILOT_PAT_0" --body "<your-github-pat>" --repo dotnet/sdk
```

## Usage

Add a job that selects a PAT, then consume it in downstream jobs:

```yaml
jobs:
  select-pat:
    name: Select Copilot PAT
    runs-on: ubuntu-latest
    outputs:
      copilot_pat_number: ${{ steps.select-copilot-pat.outputs.copilot_pat_number }}
    steps:
      - name: Checkout select-copilot-pat action
        uses: actions/checkout@v6
        with:
          persist-credentials: false
          sparse-checkout: .github/actions/select-copilot-pat
          sparse-checkout-cone-mode: true
          fetch-depth: 1

      - id: select-copilot-pat
        name: Select Copilot token from pool
        uses: ./.github/actions/select-copilot-pat
        env:
          SECRET_0: ${{ secrets.COPILOT_PAT_0 }}
          SECRET_1: ${{ secrets.COPILOT_PAT_1 }}
          # ... up to SECRET_9

  my-ai-job:
    needs: [select-pat]
    steps:
      - name: Call Copilot API
        env:
          COPILOT_TOKEN: ${{ needs.select-pat.outputs.copilot_pat_number == '0' && secrets.COPILOT_PAT_0 || needs.select-pat.outputs.copilot_pat_number == '1' && secrets.COPILOT_PAT_1 || '' }}
        run: |
          curl -X POST https://api.githubcopilot.com/chat/completions \
            -H "Authorization: Bearer $COPILOT_TOKEN" \
            -H "Content-Type: application/json" \
            -d '{"model":"gpt-4o-mini","messages":[...]}'
```

## Output Attribution

Team members' PATs are _only_ used for Copilot API requests. All other
workflow outputs (issues, comments, labels) use the `github-actions[bot]`
token and are attributed accordingly.

## References

- [dotnet/runtime#126057 — Set up GitHub Agentic Workflows](https://github.com/dotnet/runtime/pull/126057)
- [PAT creation link][create-pat]

[create-pat]: https://github.com/settings/personal-access-tokens/new?name=dotnet%20org%20agentic%20workflows&description=GitHub+Agentic+Workflows+-+Copilot+API+authentication.++Used+for+dotnet+org+workflows.+MUST+be+configured+with+only+Copilot+Requests+permissions+and+user+account+as+resource+owner.+Weekly+expiration+and+required+renewal.&user_copilot_requests=read&expires_in=8
