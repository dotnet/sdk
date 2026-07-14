---
description: Deterministic, isolated CODEOWNERS routing and assignee load snapshot.
jobs:
  codeowners_pat_pool:
    name: Select CODEOWNERS PAT
    environment: ${{ github.aw.import-inputs.environment }}
    runs-on: ubuntu-slim
    outputs:
      pat_number: ${{ steps.select.outputs.pat_number }}
    steps:
      - id: select
        name: Select a configured PAT
        env:
          CODEOWNERS_PAT_0: ${{ secrets.CODEOWNERS_PAT_0 }}
          CODEOWNERS_PAT_1: ${{ secrets.CODEOWNERS_PAT_1 }}
          CODEOWNERS_PAT_2: ${{ secrets.CODEOWNERS_PAT_2 }}
        shell: bash
        run: |
          configured=()
          for index in 0 1 2; do
            variable="CODEOWNERS_PAT_${index}"
            if [ -n "${!variable}" ]; then configured+=("$index"); fi
          done
          if [ "${#configured[@]}" -eq 0 ]; then
            echo "::error::No CODEOWNERS PAT is configured"
            exit 1
          fi
          selected="${configured[$((RANDOM % ${#configured[@]}))]}"
          echo "pat_number=$selected" >> "$GITHUB_OUTPUT"
  routing_snapshot:
    name: Build isolated routing snapshot
    needs: [codeowners_pat_pool]
    environment: ${{ github.aw.import-inputs.environment }}
    permissions:
      contents: read
    runs-on: ubuntu-latest
    outputs:
      snapshot: ${{ steps.output.outputs.snapshot }}
    steps:
      - name: Check out routing sources without credentials
        uses: actions/checkout@v6
        with:
          persist-credentials: false
      - name: Parse CODEOWNERS without an environment
        shell: bash
        run: |
          mkdir -p "$RUNNER_TEMP/issue-triage/team-responses"
          node_path="$(command -v node)"
          env -i "$node_path" \
            "$GITHUB_WORKSPACE/.github/workflows/scripts/issue-triage/parse-codeowners.js" \
            "$GITHUB_WORKSPACE/CODEOWNERS" \
            "$RUNNER_TEMP/issue-triage/routing.json" \
            "$RUNNER_TEMP/issue-triage/teams.tsv"
      - name: Fetch all CODEOWNERS team members
        shell: bash
        env:
          GH_TOKEN: ${{ case(needs.codeowners_pat_pool.outputs.pat_number == '0', secrets.CODEOWNERS_PAT_0, needs.codeowners_pat_pool.outputs.pat_number == '1', secrets.CODEOWNERS_PAT_1, needs.codeowners_pat_pool.outputs.pat_number == '2', secrets.CODEOWNERS_PAT_2, 'NO CODEOWNERS PAT AVAILABLE') }}
        run: |
          while IFS=$'\t' read -r index organization slug; do
            [[ "$index" =~ ^[0-9]+$ ]]
            [[ "$organization" =~ ^[A-Za-z0-9-]+$ ]]
            [[ "$slug" =~ ^[A-Za-z0-9_.-]+$ ]]
            gh api --paginate --slurp \
              "orgs/$organization/teams/$slug/members?per_page=100" \
              > "$RUNNER_TEMP/issue-triage/team-responses/team-$index.json"
          done < "$RUNNER_TEMP/issue-triage/teams.tsv"
      - name: Validate members and generate load request without an environment
        shell: bash
        run: |
          node_path="$(command -v node)"
          env -i "$node_path" \
            "$GITHUB_WORKSPACE/.github/workflows/scripts/issue-triage/parse-team-members.js" \
            "$RUNNER_TEMP/issue-triage/routing.json" \
            "$RUNNER_TEMP/issue-triage/team-responses" \
            "$RUNNER_TEMP/issue-triage/expanded-routing.json" \
            "$RUNNER_TEMP/issue-triage/load-request.json" \
            "${{ github.repository }}"
      - name: Fetch candidate issue loads
        shell: bash
        env:
          GH_TOKEN: ${{ case(needs.codeowners_pat_pool.outputs.pat_number == '0', secrets.CODEOWNERS_PAT_0, needs.codeowners_pat_pool.outputs.pat_number == '1', secrets.CODEOWNERS_PAT_1, needs.codeowners_pat_pool.outputs.pat_number == '2', secrets.CODEOWNERS_PAT_2, 'NO CODEOWNERS PAT AVAILABLE') }}
        run: gh api graphql --input "$RUNNER_TEMP/issue-triage/load-request.json" > "$RUNNER_TEMP/issue-triage/load-response.json"
      - name: Validate API response without an environment
        shell: bash
        run: |
          node_path="$(command -v node)"
          env -i "$node_path" \
            "$GITHUB_WORKSPACE/.github/workflows/scripts/issue-triage/finalize-routing.js" \
            "$RUNNER_TEMP/issue-triage/expanded-routing.json" \
            "$RUNNER_TEMP/issue-triage/load-response.json" \
            "$RUNNER_TEMP/issue-triage/snapshot.json"
      - id: output
        name: Publish sanitized routing snapshot
        shell: bash
        run: |
          {
            echo 'snapshot<<ROUTING_SNAPSHOT'
            cat "$RUNNER_TEMP/issue-triage/snapshot.json"
            echo
            echo ROUTING_SNAPSHOT
          } >> "$GITHUB_OUTPUT"
import-schema:
  environment:
    type: string
    required: false
    default: codeowners-pat-pool
---
