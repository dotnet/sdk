---
name: "Build Failure Analysis (command)"
description: >-
  Rerun the build-failure analysis on a pull request when a maintainer comments
  `/analyze-build-failure`. Same body as `build-failure-analysis.md` — it does
  NOT rebuild: it inspects the PR's **latest** Azure Pipelines `dotnet-sdk-public-ci`
  build and, **only when that latest build has failed** (it stops if the
  newest build is still running or has succeeded), downloads the binary logs
  that build already produced (all build legs) and delegates to the
  `build-failure-analyst` agent (which queries the binlogs live via the
  containerized `binlog-mcp` MCP server). Useful when a previous run was
  cancelled, the analysis comment was dismissed, or the agent needs another
  pass. Like the auto workflow it performs **no build**; the generated jobs do
  check out the repository (and, for the slash-command event, the PR branch)
  for agent tooling only — the PR's code is never built or executed.

on:
  slash_command:
    name: analyze-build-failure
    events: [pull_request_comment]
    strategy: centralized
  roles: [admin, maintainer, write]
  reaction: "eyes"
  # Gate the AI pipeline on the fetch job so the agent only runs when a binlog
  # was actually retrieved from a failed Azure DevOps build.
  needs: [fetch-binlog]

# Skip activation (and the agent) unless a binlog was retrieved — e.g. if the
# PR's latest Azure DevOps build did not fail, or the PR is out of scope.
if: needs.fetch-binlog.outputs.binlog-found == 'true'

# Least-privilege for the workflow/agent jobs. The agent runs read-only; it
# does NOT post directly. All PR writes it produces (summary comment + inline
# review suggestions) go through gh-aw **safe-outputs**, which the compiler
# emits as a separate `safe_outputs` job granted `pull-requests: write` +
# `issues: write` in the generated lock. (The slash-command trigger also adds
# an acknowledgement reaction to the command comment; gh-aw emits that in its
# own generated job with the scope it needs — it is not driven by this agent
# job.) Keep `pull-requests: read` here so the AI agent job stays
# least-privilege — do NOT raise it to `write`, that would hand PR-write scope
# to the agent job unnecessarily.
permissions:
  contents: read
  pull-requests: read
  copilot-requests: write

concurrency:
  group: build-failure-analysis-${{ github.event.issue.number || github.event.pull_request.number || fromJSON(github.event.inputs.aw_context || github.event.client_payload.aw_context || '{}').item_number || github.run_id }}
  cancel-in-progress: true

timeout-minutes: 30

network:
  allowed:
    - defaults
    - dotnet

imports:
  - uses: shared/pat_pool.md
    with:
      environment: copilot-pat-pool
  - shared/build-failure-analysis-shared.md

environment: copilot-pat-pool

engine:
  id: copilot
  env:
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_PAT_0, needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_PAT_1, needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_PAT_2, needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_PAT_3, needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_PAT_4, needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_PAT_5, needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_PAT_6, needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_PAT_7, needs.pat_pool.outputs.pat_number == '8', secrets.COPILOT_PAT_8, needs.pat_pool.outputs.pat_number == '9', secrets.COPILOT_PAT_9, 'NO COPILOT PAT AVAILABLE') }}

# Live binlog access for the agent — see build-failure-analysis.md for the
# rationale. The fetch-binlog job downloads each build leg's binlog from Azure
# DevOps into a directory and uploads it; the agent job downloads it to
# `/tmp/binlogs` and the gh-aw MCP gateway mounts it read-only at
# `/data/binlogs`.
mcp-servers:
  binlog-mcp:
    container: "mcr.microsoft.com/dotnet-buildtools/prereqs:azurelinux-3.0-binlog-mcp-amd64"
    mounts:
      - "/tmp/binlogs:/data/binlogs:ro"
    allowed: ["*"]

# Custom job that reuses the binlogs from the PR's most recent failed Azure
# DevOps `dotnet-sdk-public-ci` build instead of rebuilding. Mirrors the fetch-binlog job
# in build-failure-analysis.md; it locates the build by the PR's merge branch
# (no `check_run` payload is available on a slash command).
jobs:
  fetch-binlog:
    name: Fetch binlogs (Azure Pipelines)
    runs-on: ubuntu-latest
    timeout-minutes: 15
    permissions:
      contents: read
      pull-requests: read
    outputs:
      binlog-found: ${{ steps.fetch.outputs.binlog-found }}
      pr-number: ${{ steps.fetch.outputs.pr-number }}
      pr-head-sha: ${{ steps.fetch.outputs.pr-head-sha }}
      pr-merge-sha: ${{ steps.fetch.outputs.pr-merge-sha }}
      ado-build-id: ${{ steps.fetch.outputs.ado-build-id }}
      ado-build-url: ${{ steps.fetch.outputs.ado-build-url }}
    steps:
      - name: Download binlogs from the PR's latest failed Azure Pipelines build
        id: fetch
        env:
          GH_TOKEN: ${{ github.token }}
          GH_AW_REPO: ${{ github.repository }}
          ADO_API: "https://dev.azure.com/dnceng-public/public/_apis"
          ADO_BUILD_UI: "https://dev.azure.com/dnceng-public/public/_build/results"
          # dotnet-sdk-public-ci pipeline definition id in dnceng-public/public.
          ADO_BUILD_DEFINITION_ID: "101"
          PR_NUMBER: ${{ github.event.issue.number || fromJSON(github.event.inputs.aw_context || github.event.client_payload.aw_context || '{}').item_number }}
        run: |
          # Advisory + best-effort. On any gap emit binlog-found=false so the
          # agent pipeline stays inert.
          set +e
          set +o pipefail
          emit_none() { echo "binlog-found=false" >> "$GITHUB_OUTPUT"; exit 0; }

          [ -z "${PR_NUMBER}" ] && { echo "::warning::No PR number resolved from the slash-command event / aw_context."; emit_none; }
          # PR_NUMBER feeds GitHub API paths and the `refs/pull/<n>/merge`
          # branch query; require it numeric so a malformed event/aw_context
          # payload can't reach those URLs with unexpected content.
          if ! printf '%s' "${PR_NUMBER}" | grep -qE '^[0-9]+$'; then
            echo "::warning::Resolved PR number '${PR_NUMBER}' is not numeric; refusing."; emit_none
          fi

          # --- Scope check: only analyse PRs targeting main / release/* ---
          PR_JSON=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" 2>/dev/null)
          BASE_REF=$(printf '%s' "${PR_JSON}" | jq -r '.base.ref // empty')
          HEAD_SHA=$(printf '%s' "${PR_JSON}" | jq -r '.head.sha // empty')
          case "${BASE_REF}" in
            main|release/*) echo "PR #${PR_NUMBER} base '${BASE_REF}' is in scope." ;;
            *) echo "::warning::PR #${PR_NUMBER} base '${BASE_REF}' is out of scope (main, release/*); skipping."; emit_none ;;
          esac

          # --- Find the PR's most recent dotnet-sdk-public-ci build (merge ref) ---
          # Query the newest build REGARDLESS of status (queue-time desc). If
          # the newest build is still queued/running — e.g. right after a
          # force-push — skip: analysing an older completed failure now would
          # pair a stale binlog with the PR's current head. Only proceed when
          # the newest build is completed AND failed. The head SHA is then
          # anchored to that build's own revision (below), so links/suggestions
          # always match the analysed binlog.
          builds_json=$(curl -sSL --retry 3 \
            "${ADO_API}/build/builds?definitions=${ADO_BUILD_DEFINITION_ID}&branchName=refs/pull/${PR_NUMBER}/merge&queryOrder=queueTimeDescending&\$top=1&api-version=7.1")
          BUILD_ID=$(printf '%s' "${builds_json}" | jq -r '.value // [] | .[0].id // empty')
          BUILD_STATUS=$(printf '%s' "${builds_json}" | jq -r '.value // [] | .[0].status // empty')
          BUILD_RESULT=$(printf '%s' "${builds_json}" | jq -r '.value // [] | .[0].result // empty')
          echo "Newest dotnet-sdk-public-ci build for PR #${PR_NUMBER}: id='${BUILD_ID}' status='${BUILD_STATUS}' result='${BUILD_RESULT}'"
          [ -z "${BUILD_ID}" ] && { echo "::warning::No dotnet-sdk-public-ci build found for PR #${PR_NUMBER}."; emit_none; }
          # Require a numeric build id before it feeds subsequent ADO API URLs,
          # so a malformed query response can't inject unexpected path/query.
          if ! printf '%s' "${BUILD_ID}" | grep -qE '^[0-9]+$'; then
            echo "::warning::ADO build id '${BUILD_ID}' is not numeric; refusing."; emit_none
          fi
          if [ "${BUILD_STATUS}" != "completed" ]; then
            echo "::warning::PR #${PR_NUMBER}'s newest dotnet-sdk-public-ci build (${BUILD_ID}) is still '${BUILD_STATUS}'; wait for it to finish before analysing."
            emit_none
          fi
          if [ "${BUILD_RESULT}" != "failed" ]; then
            echo "::warning::PR #${PR_NUMBER}'s newest dotnet-sdk-public-ci build (${BUILD_ID}) result is '${BUILD_RESULT}', not failed — the failure looks resolved; nothing to analyse."
            emit_none
          fi

          # Require the build's analyzed revision to equal the PR's CURRENT
          # head. gh-aw safe-output review comments carry no `commit_id` (they
          # target the current PR diff), so analyzing a stale revision would
          # misplace/reject inline suggestions. The PR can advance between
          # selecting the build and downloading artifacts, and right after a
          # force-push this query can still return the previous failed build —
          # so re-read the head here and skip if it moved.
          build_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}?api-version=7.1")
          BUILD_PR_SHA=$(printf '%s' "${build_json}" | jq -r '.triggerInfo["pr.sourceSha"] // empty')
          BUILD_MERGE_SHA=$(printf '%s' "${build_json}" | jq -r '.sourceVersion // empty')
          PR_JSON2=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" 2>/dev/null)
          CURRENT_HEAD=$(printf '%s' "${PR_JSON2}" | jq -r '.head.sha // empty')
          CURRENT_MERGE=$(printf '%s' "${PR_JSON2}" | jq -r '.merge_commit_sha // empty')
          # Fail CLOSED: if either SHA can't be resolved (transient API failure
          # or missing Azure triggerInfo), skip rather than risk analyzing a
          # stale binlog against the current diff.
          if [ -z "${BUILD_PR_SHA}" ] || [ -z "${CURRENT_HEAD}" ]; then
            echo "::warning::Could not resolve build revision ('${BUILD_PR_SHA}') and/or current PR head ('${CURRENT_HEAD}'); skipping."
            emit_none
          fi
          if [ "${BUILD_PR_SHA}" != "${CURRENT_HEAD}" ]; then
            echo "::warning::Build ${BUILD_ID} analyzed revision '${BUILD_PR_SHA}' but PR #${PR_NUMBER} head is now '${CURRENT_HEAD}'; skipping stale build (a newer build will cover the current revision)."
            emit_none
          fi
          # ADO builds GitHub's `refs/pull/<n>/merge` ref, so build_json.sourceVersion
          # is that merge commit; if the base branch advanced it differs from the
          # PR's current merge_commit_sha even with the head unchanged. Skip stale merges.
          if [ -n "${BUILD_MERGE_SHA}" ] && [ -n "${CURRENT_MERGE}" ] && [ "${BUILD_MERGE_SHA}" != "${CURRENT_MERGE}" ]; then
            echo "::warning::Build ${BUILD_ID} merge revision '${BUILD_MERGE_SHA}' but PR #${PR_NUMBER} current merge is '${CURRENT_MERGE}' (base branch advanced); skipping stale merge."
            emit_none
          fi
          HEAD_SHA="${CURRENT_HEAD}"
          echo "Analyzing build ${BUILD_ID} at PR head revision '${HEAD_SHA}'."

          # --- Download every <Leg>_Logs_Attempt<N> artifact and extract binlogs ---
          artifacts_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}/artifacts?api-version=7.1")
          mapfile -t names < <(printf '%s' "${artifacts_json}" | jq -r '.value // [] | map(select(.name | test("_Logs_Attempt[0-9]+$"))) | .[].name')
          [ "${#names[@]}" -eq 0 ] && { echo "::warning::No *_Logs_Attempt* artifacts on build ${BUILD_ID}."; emit_none; }

          # Guards for untrusted PR-produced archives: cap the compressed
          # download and the reported uncompressed size per artifact, bound
          # extraction time, AND enforce a cumulative uncompressed budget across
          # all legs so many individually-small artifacts can't collectively
          # exhaust the runner's disk.
          MAX_ZIP_BYTES=524288000       # 500 MB compressed per artifact
          MAX_UNZIP_BYTES=2147483648    # 2 GB uncompressed per artifact
          MAX_TOTAL_BYTES=4294967296    # 4 GB uncompressed across all artifacts
          TOTAL_BYTES=0
          mkdir -p /tmp/binlogs
          count=0
          staged_legs=0
          ai=0
          for name in "${names[@]}"; do
            # `name` is PR-controlled ADO artifact metadata and the
            # `_Logs_Attempt<N>` filter only anchors the suffix, so sanitize it
            # before using it in any on-disk path (guards against `/` or `..`
            # traversal); keep the original `name` for the artifacts_json lookup.
            safe_name=$(printf '%s' "${name}" | tr -c 'A-Za-z0-9._-' '_')
            ai=$((ai + 1))
            url=$(printf '%s' "${artifacts_json}" | jq -r --arg n "${name}" '.value[] | select(.name==$n) | .resource.downloadUrl // empty')
            [ -z "${url}" ] && continue
            rm -rf /tmp/ax /tmp/a.zip
            mkdir -p /tmp/ax
            # Hard-cap the bytes written to disk regardless of Content-Length:
            # stream through `head -c` (cap + 1) and bound total time.
            curl -sSL --retry 3 --max-time 300 "${url}" 2>/dev/null | head -c $((MAX_ZIP_BYTES + 1)) > /tmp/a.zip || true
            ZIP_BYTES=$(stat -c%s /tmp/a.zip 2>/dev/null || echo 0)
            if [ "${ZIP_BYTES}" -eq 0 ]; then
              echo "::warning::Skipping ${name}: empty or failed download."; continue
            fi
            if [ "${ZIP_BYTES}" -gt "${MAX_ZIP_BYTES}" ]; then
              echo "::warning::Skipping ${name}: download exceeded ${MAX_ZIP_BYTES} bytes."; continue
            fi
            UNCOMP=$(unzip -l /tmp/a.zip 2>/dev/null | tail -1 | awk '{print $1}')
            # Fail safe: if the uncompressed size isn't a plain integer (corrupt
            # zip / unexpected `unzip -l` output), we can't verify it — skip the
            # artifact rather than let a non-numeric value bypass the `-gt` guard.
            if ! printf '%s' "${UNCOMP}" | grep -qE '^[0-9]+$'; then
              echo "::warning::Skipping ${name}: could not determine uncompressed size (unparseable unzip output)."; continue
            fi
            # ZIP64 uncompressed sizes can reach ~20 digits — beyond Bash's
            # signed 64-bit range, where `-gt` (and the cumulative `$((...))`
            # below) error out and, under `set +e`, would let an oversized
            # archive slip past the guard. Any value with more digits than the
            # limit is unambiguously larger, so reject on decimal length first;
            # after this, UNCOMP fits safely in the integer range used below.
            if [ "${#UNCOMP}" -gt "${#MAX_UNZIP_BYTES}" ]; then
              echo "::warning::Skipping ${name}: uncompressed size has ${#UNCOMP} digits, exceeding the ${MAX_UNZIP_BYTES} guard (possible zip bomb)."; continue
            fi
            if [ "${UNCOMP}" -gt "${MAX_UNZIP_BYTES}" ]; then
              echo "::warning::Skipping ${name}: uncompressed size ${UNCOMP} exceeds ${MAX_UNZIP_BYTES} guard (possible zip bomb)."; continue
            fi
            if [ $((TOTAL_BYTES + UNCOMP)) -gt "${MAX_TOTAL_BYTES}" ]; then
              echo "::warning::Cumulative uncompressed budget ${MAX_TOTAL_BYTES} reached at ${name}; stopping extraction."; break
            fi
            # Refuse the archive if any entry path is absolute or has a `..`
            # component (defense-in-depth over unzip's own traversal guard),
            # then extract `*.binlog` entries *preserving* their in-archive
            # paths (no `-j`) under a fresh dir + timeout, so two binlogs that
            # share a basename in different folders don't overwrite each other.
            if unzip -Z1 /tmp/a.zip 2>/dev/null | grep -qE '(^/|(^|/)\.\.(/|$))'; then
              echo "::warning::Skipping ${name}: archive has a suspicious (absolute or ..) entry path."; continue
            fi
            timeout 120 unzip -o /tmp/a.zip '*.binlog' -d /tmp/ax >/dev/null 2>&1 \
              || { echo "::warning::Skipping ${name}: extraction failed or timed out."; continue; }
            # Consume the cumulative budget only once the archive actually
            # extracted — not on a suspicious-path or extraction-failure skip
            # above — so a skipped leg can't wrongly exhaust the budget and
            # force later legs to be dropped as "incomplete".
            TOTAL_BYTES=$((TOTAL_BYTES + UNCOMP))
            i=0
            leg_staged=0
            while IFS= read -r bl; do
              [ -f "${bl}" ] || continue
              # Every destination is uniquely prefixed with the artifact index
              # (`ai`) and a per-file counter (`i`), so neither a cross-artifact
              # sanitize collision nor same-basename entries within one archive
              # can overwrite a previously staged leg's binlog. `safe_name` is
              # kept only for readability.
              dest="/tmp/binlogs/${ai}_${i}_${safe_name}.binlog"
              # Only count a staged binlog when the copy actually succeeds —
              # `set +e` is on, so a failed `cp` must not inflate the counts.
              if cp "${bl}" "${dest}"; then
                count=$((count + 1))
                i=$((i + 1))
                leg_staged=1
              else
                echo "::warning::Failed to stage ${bl}; skipping."
              fi
            done < <(find /tmp/ax -type f -name '*.binlog')
            # This leg produced at least one usable binlog.
            [ "${leg_staged}" -eq 1 ] && staged_legs=$((staged_legs + 1))
          done
          echo "Extracted ${count} binlog(s) from ${staged_legs}/${#names[@]} legs into /tmp/binlogs:"
          ls -la /tmp/binlogs || true
          [ "${count}" -eq 0 ] && { echo "::warning::No *.binlog found in any *_Logs_Attempt* artifact of build ${BUILD_ID}."; emit_none; }
          # Fail CLOSED on a partial set: if any *_Logs_Attempt* leg did not yield
          # a usable binlog (download/extract failure, size-guard skip, or no
          # binlog inside), we cannot see every leg. Activating anyway would let
          # the agent treat the retrieved legs as the whole build and possibly
          # mis-classify a real build break in a missing leg as a clean compile /
          # non-build failure. A later build/check will re-trigger the analysis.
          if [ "${staged_legs}" -ne "${#names[@]}" ]; then
            echo "::warning::Only ${staged_legs} of ${#names[@]} *_Logs_Attempt* legs produced a usable binlog; skipping to avoid analyzing an incomplete build (a missing leg could be the one that failed)."
            emit_none
          fi

          # The download/extract loop above can take minutes. Re-read the PR
          # head right before activating and fail CLOSED if it moved or can't
          # be resolved: a force-push during that window would otherwise leave
          # the analyzed binlog stale relative to the current diff (inline
          # comments carry no commit_id and target the current diff).
          LATEST_PR=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" 2>/dev/null)
          LATEST_HEAD=$(printf '%s' "${LATEST_PR}" | jq -r '.head.sha // empty')
          LATEST_MERGE=$(printf '%s' "${LATEST_PR}" | jq -r '.merge_commit_sha // empty')
          if [ -z "${LATEST_HEAD}" ] || [ "${LATEST_HEAD}" != "${HEAD_SHA}" ]; then
            echo "::warning::PR #${PR_NUMBER} head changed during artifact download ('${HEAD_SHA}' -> '${LATEST_HEAD}') or could not be re-resolved; skipping to avoid posting stale-build suggestions against the new diff."
            emit_none
          fi
          # The base branch may also have advanced during the download; if the
          # merge revision moved from what the build analyzed, skip (stale merge).
          if [ -n "${BUILD_MERGE_SHA}" ] && [ -n "${LATEST_MERGE}" ] && [ "${LATEST_MERGE}" != "${BUILD_MERGE_SHA}" ]; then
            echo "::warning::PR #${PR_NUMBER} merge revision changed during artifact download ('${BUILD_MERGE_SHA}' -> '${LATEST_MERGE}'); skipping stale merge."
            emit_none
          fi

          {
            echo "binlog-found=true"
            echo "pr-number=${PR_NUMBER}"
            echo "pr-head-sha=${HEAD_SHA}"
            echo "pr-merge-sha=${BUILD_MERGE_SHA}"
            echo "ado-build-id=${BUILD_ID}"
            echo "ado-build-url=${ADO_BUILD_UI}?buildId=${BUILD_ID}"
          } >> "$GITHUB_OUTPUT"

      - name: Upload analysis artifact
        if: steps.fetch.outputs.binlog-found == 'true'
        uses: actions/upload-artifact@v7.0.1
        with:
          name: build-failure-analysis-data
          path: /tmp/binlogs
          if-no-files-found: warn
          retention-days: 1

# Steps that run in the agent job. The top-level `if:` gates these on binlogs
# having been retrieved, so the agent never runs without something to analyse.
steps:
  - name: Download analysis artifact
    uses: actions/download-artifact@v8.0.1
    with:
      name: build-failure-analysis-data
      path: /tmp/binlogs

  - name: Export agent context
    env:
      GH_AW_BINLOG_FOUND_VALUE: ${{ needs.fetch-binlog.outputs.binlog-found }}
      GH_AW_PR_NUMBER_VALUE: ${{ needs.fetch-binlog.outputs.pr-number }}
      GH_AW_PR_HEAD_SHA_VALUE: ${{ needs.fetch-binlog.outputs.pr-head-sha }}
      GH_AW_PR_MERGE_SHA_VALUE: ${{ needs.fetch-binlog.outputs.pr-merge-sha }}
      GH_AW_ADO_BUILD_URL_VALUE: ${{ needs.fetch-binlog.outputs.ado-build-url }}
      GH_AW_GITHUB_WORKSPACE: ${{ github.workspace }}
    run: |
      # See build-failure-analysis.md for the binlog path conventions. The
      # per-leg binlogs are read through the binlog-mcp MCP server (mounted at
      # `/data/binlogs`); GH_AW_BINLOG_HOST_PATH points at the Azure DevOps
      # build for human-facing references.
      BINLOG_DIR="/data/binlogs"
      LIST=""
      if [ "${GH_AW_BINLOG_FOUND_VALUE:-false}" = "true" ] && [ -d /tmp/binlogs ]; then
        for f in /tmp/binlogs/*.binlog; do
          [ -f "$f" ] || continue
          LIST="${LIST}${BINLOG_DIR}/$(basename "$f")"$'\n'
        done
      fi
      FIRST=$(printf '%s' "$LIST" | head -1)
      {
        echo "GH_AW_BUILD_OUTCOME=failure"
        echo "GH_AW_BINLOG_DIR=${BINLOG_DIR}"
        echo "GH_AW_BINLOG_PATH=${FIRST}"
        echo "GH_AW_BINLOG_HOST_PATH=${GH_AW_ADO_BUILD_URL_VALUE}"
        echo "GH_AW_PR_NUMBER=${GH_AW_PR_NUMBER_VALUE}"
        echo "GH_AW_PR_HEAD_SHA=${GH_AW_PR_HEAD_SHA_VALUE}"
        echo "GH_AW_PR_MERGE_SHA=${GH_AW_PR_MERGE_SHA_VALUE}"
        echo "GH_AW_WORKSPACE=${GH_AW_GITHUB_WORKSPACE}"
        echo "GH_AW_BINLOG_LIST<<GH_AW_EOF"
        printf '%s' "$LIST"
        echo "GH_AW_EOF"
      } >> "$GITHUB_ENV"

tools:
  # cli-proxy + github.mode: gh-proxy route GitHub tools and Safe Outputs through the
  # generated CLI proxy instead of the native HTTP MCP endpoint on the internal awmg-mcpg
  # gateway, avoiding the firewall TCP_DENIED/403 on that single-label host.
  # See github/gh-aw#45915.
  cli-proxy: true
  github:
    mode: gh-proxy
    toolsets: [pull_requests, repos]
    allowed-repos:
      - "${{ github.repository }}"
    # This workflow exists to analyse failing PRs — including unapproved ones
    # from external contributors — so it must be able to read PR content that
    # has not been approved or merged.
    min-integrity: none
  bash:
    - "cat"
    - "head"
    - "tail"
    - "grep"
    - "wc"
    - "sort"
    - "uniq"
    - "ls"
    - "find"

safe-outputs:
  messages:
    footer: "> 🤖 **Automated content by GitHub Copilot.** Generated by the [{workflow_name}]({agentic_workflow_url}) workflow.{ai_credits_suffix} · [◷]({history_link})"
  # The agent targets the resolved PR via `GH_AW_PR_NUMBER` (`target: "*"`),
  # matching the auto-trigger workflow.
  report-failure-as-issue: false
  add-comment:
    max: 5
    target: "*"
    hide-older-comments: true
  create-pull-request-review-comment:
    max: 25
    target: "*"
  noop:
    max: 5
    report-as-issue: false
---

<!--
  Body provided by shared/build-failure-analysis-shared.md.
-->
