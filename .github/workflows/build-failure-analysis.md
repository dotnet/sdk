---
name: "Build Failure Analysis"
description: >-
  When the Azure Pipelines PR build (`dotnet-sdk-public-ci`) fails, downloads the binary
  logs that build already produced — it does NOT rebuild — and delegates to
  the `build-failure-analyst` agent, which queries the binlogs live via the
  containerized `binlog-mcp` MCP server to identify root causes, post a PR
  comment summarizing them, and attach inline `suggestion` blocks tied to the
  diff.

# This workflow is **advisory**, not gating, and it performs **no build of its
# own**. The SDK's authoritative PR build runs on Azure DevOps
# (dnceng-public/public, pipeline "dotnet-sdk-public-ci", definitionId 101) and publishes
# each build leg's binary logs inside a `<Leg>_Logs_Attempt<N>` pipeline
# artifact (e.g. `Windows_x64_Logs_Attempt1`). When
# that build's GitHub check reports failure, this workflow downloads the
# binlogs from **all** build legs (anonymously — dnceng-public/public is a
# public project) and the agent analyses whichever leg(s) actually contain
# errors. Reusing the binlogs avoids a duplicate build: the analysis pipeline
# only downloads build artifacts (data) and reads them — it does **not** build
# or execute PR code. (gh-aw's generated agent job **does** check out the
# repository — via `actions/checkout` — to load the workflow's own agent
# configuration; that checkout is for tooling only and uses the event's ref,
# **not** the PR head, so no PR code is built or executed.)

on:
  # `check_run` fires for every check on a commit, so the `fetch-binlog` job
  # below filters tightly to the rollup `dotnet-sdk-public-ci` build check
  # reporting failure. The pipeline also emits per-leg checks named
  # `dotnet-sdk-public-ci (Build <leg>)`; the exact-name match below
  # deliberately ignores those so the analysis runs once per build, not once
  # per leg.
  check_run:
    types: [completed]
  # Advisory analysis should run for **every** failing PR — including external
  # contributors' PRs, which are the most likely to break the build. Disable
  # gh-aw's default author-association gate (which would otherwise skip
  # non-write-access actors, and on `check_run` the actor is the pipeline app
  # anyway). This is safe here: the workflow only reads a public binlog and
  # posts advisory comments — it never builds or executes PR code.
  roles: all
  # Manual entry point for reruns / testing: analyse a specific Azure DevOps
  # build id and post to a specific PR.
  workflow_dispatch:
    inputs:
      ado-build-id:
        description: "Azure DevOps build id to analyze (dnceng-public/public)."
        required: true
        type: string
      pr-number:
        description: "PR number to post the analysis on."
        required: true
        type: string
  # Gate the whole AI pipeline on the fetch job so the agent only runs when a
  # binlog was actually retrieved.
  needs: [fetch-binlog]

# Activate (and run the agent) only when the fetch job retrieved at least one
# binlog. When `check_run` fires for an unrelated / passing check the
# fetch-binlog job is skipped, its output is empty, and this cascades into a
# skipped agent — no AI calls on anything but a real `dotnet-sdk-public-ci`
# failure whose PR targets an in-scope base branch.
if: needs.fetch-binlog.outputs.binlog-found == 'true'

# Least-privilege for the workflow/agent jobs. The agent runs read-only; it
# does NOT post directly. All PR writes (summary comment + inline review
# suggestions) go through gh-aw **safe-outputs**, which the compiler emits as
# a separate `safe_outputs` job granted `pull-requests: write` + `issues:
# write` in the generated lock. Keep `pull-requests: read` here so the AI
# agent job stays least-privilege — do NOT raise it to `write`, that would
# hand PR-write scope to the agent job unnecessarily.
permissions:
  contents: read
  pull-requests: read
  copilot-requests: write

concurrency:
  # Only real `dotnet-sdk-public-ci` check_run events (and manual dispatch for
  # a PR) use a PR/head-scoped group, so a newer analysis supersedes an
  # in-progress one for the same PR. Every OTHER completed check_run on the PR
  # would otherwise land in the same group and — with cancel-in-progress —
  # abort the running real analysis, so those get a unique per-run group that
  # collides with nothing.
  group: ${{ (github.event_name == 'check_run' && github.event.check_run.name == 'dotnet-sdk-public-ci' && format('build-failure-analysis-{0}', github.event.check_run.pull_requests[0].number || github.event.check_run.head_sha)) || (github.event_name == 'workflow_dispatch' && format('build-failure-analysis-{0}', inputs['pr-number'])) || format('build-failure-analysis-run-{0}', github.run_id) }}
  cancel-in-progress: true

timeout-minutes: 30

network:
  allowed:
    - defaults
    - dotnet

# ###############################################################
# Select a PAT from the pool and override COPILOT_GITHUB_TOKEN.
# Run agentic jobs in an isolated `copilot-pat-pool` environment.
#
# When org-level billing is available, this will be removed.
# See `shared/pat_pool.README.md` for more information.
# ###############################################################
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

# Live binlog access for the agent. The build-leg binlogs are downloaded from
# Azure DevOps by the fetch-binlog job into a directory, uploaded as an
# artifact, downloaded by the agent job to `/tmp/binlogs`, and mounted
# read-only into this container at `/data/binlogs` by the gh-aw MCP gateway.
mcp-servers:
  binlog-mcp:
    container: "mcr.microsoft.com/dotnet-buildtools/prereqs:azurelinux-3.0-binlog-mcp-amd64"
    mounts:
      - "/tmp/binlogs:/data/binlogs:ro"
    allowed: ["*"]

# Custom job that reuses the binlogs from the failed Azure DevOps build instead
# of rebuilding. It resolves the ADO build id (from the check details URL or
# the dispatch input), verifies the PR targets an in-scope base branch,
# downloads every `<Leg>_Logs_Attempt<N>` artifact, extracts each leg's
# `*.binlog`, and uploads them for the agent job.
jobs:
  fetch-binlog:
    name: Fetch binlogs (Azure Pipelines)
    runs-on: ubuntu-latest
    timeout-minutes: 15
    # `check_run` fires for every check; only act on the rollup SDK PR build
    # check reporting failure (or a manual dispatch).
    if: >
      github.event_name == 'workflow_dispatch' ||
      (github.event.check_run.name == 'dotnet-sdk-public-ci' && github.event.check_run.conclusion == 'failure')
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
      missing-legs: ${{ steps.fetch.outputs.missing-legs }}
    steps:
      - name: Download binlogs from the failed Azure Pipelines build
        id: fetch
        env:
          GH_TOKEN: ${{ github.token }}
          GH_AW_REPO: ${{ github.repository }}
          ADO_API: "https://dev.azure.com/dnceng-public/public/_apis"
          ADO_BUILD_UI: "https://dev.azure.com/dnceng-public/public/_build/results"
          # dotnet-sdk-public-ci pipeline definition id in dnceng-public/public (used to
          # validate a dispatched build id belongs to the right pipeline).
          ADO_BUILD_DEFINITION_ID: "101"
          EVENT_NAME: ${{ github.event_name }}
          CHECK_DETAILS_URL: ${{ github.event.check_run.details_url }}
          CHECK_HEAD_SHA: ${{ github.event.check_run.head_sha }}
          CHECK_PR_NUMBER: ${{ github.event.check_run.pull_requests[0].number }}
          DISPATCH_BUILD_ID: ${{ inputs['ado-build-id'] }}
          DISPATCH_PR_NUMBER: ${{ inputs['pr-number'] }}
        run: |
          # Advisory + best-effort: on any gap emit binlog-found=false and the
          # agent pipeline stays inert.
          set +e
          set +o pipefail
          emit_none() { echo "binlog-found=false" >> "$GITHUB_OUTPUT"; exit 0; }

          # --- 1. Resolve the Azure DevOps build id ---
          if [ "${EVENT_NAME}" = "workflow_dispatch" ]; then
            BUILD_ID="${DISPATCH_BUILD_ID}"
          else
            # details_url looks like: .../_build/results?buildId=NNN&view=...
            BUILD_ID=$(printf '%s' "${CHECK_DETAILS_URL}" | grep -oE 'buildId=[0-9]+' | head -1 | cut -d= -f2)
          fi
          echo "Azure DevOps build id: '${BUILD_ID}'"
          [ -z "${BUILD_ID}" ] && { echo "::warning::Could not resolve an ADO build id."; emit_none; }
          # The build id feeds directly into ADO API URLs below; require it to
          # be purely numeric (esp. on workflow_dispatch, where it is free-form
          # input) so a malformed value can't alter the request path/query.
          if ! printf '%s' "${BUILD_ID}" | grep -qE '^[0-9]+$'; then
            echo "::warning::Resolved ADO build id '${BUILD_ID}' is not numeric; refusing."; emit_none
          fi

          # Fetch the build metadata once, up front: it is the authoritative
          # source both for the PR number (via sourceBranch) and for the
          # definition/result/revision validated in step 4.
          build_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}?api-version=7.1")
          RESULT=$(printf '%s' "${build_json}" | jq -r '.result // empty')
          DEF_ID=$(printf '%s' "${build_json}" | jq -r '.definition.id // empty')
          SRC_BRANCH=$(printf '%s' "${build_json}" | jq -r '.sourceBranch // empty')
          # A PR build's sourceBranch is exactly `refs/pull/<n>/merge`, so it
          # identifies the PR unambiguously — unlike the commit->PRs API, which
          # can return several PRs in an unspecified order.
          BUILD_PR_NUM=$(printf '%s' "${SRC_BRANCH}" | sed -n 's#^refs/pull/\([0-9]\{1,\}\)/merge$#\1#p')

          # --- 2. Resolve the PR number + head SHA ---
          if [ "${EVENT_NAME}" = "workflow_dispatch" ]; then
            PR_NUMBER="${DISPATCH_PR_NUMBER}"
            HEAD_SHA=""
          else
            # Prefer the PR named by the build's own sourceBranch (authoritative:
            # `refs/pull/<n>/merge`) over check_run.pull_requests[0], whose order
            # isn't guaranteed and can name a different PR that shares the commit.
            PR_NUMBER="${BUILD_PR_NUM:-${CHECK_PR_NUMBER}}"
            HEAD_SHA="${CHECK_HEAD_SHA}"
          fi
          [ -z "${PR_NUMBER}" ] && { echo "::warning::Could not resolve a PR number."; emit_none; }
          # PR_NUMBER feeds `gh api .../pulls/<n>` and the `refs/pull/<n>/merge`
          # comparison; require it numeric so a malformed value can't reach the
          # GitHub API path (traversal-like input) or skew the branch match.
          if ! printf '%s' "${PR_NUMBER}" | grep -qE '^[0-9]+$'; then
            echo "::warning::Resolved PR number '${PR_NUMBER}' is not numeric; refusing."; emit_none
          fi

          # --- 3. Scope check: only analyse PRs targeting main / release/* ---
          PR_JSON=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" 2>/dev/null)
          BASE_REF=$(printf '%s' "${PR_JSON}" | jq -r '.base.ref // empty')
          [ -z "${HEAD_SHA}" ] && HEAD_SHA=$(printf '%s' "${PR_JSON}" | jq -r '.head.sha // empty')
          case "${BASE_REF}" in
            main|release/*) echo "PR #${PR_NUMBER} base '${BASE_REF}' is in scope." ;;
            *) echo "::warning::PR #${PR_NUMBER} base '${BASE_REF}' is out of scope (main, release/*); skipping."; emit_none ;;
          esac

          # --- 4. Validate the build for EVERY trigger (not just dispatch):
          #        it must be the dotnet-sdk-public-ci definition (101), have failed, and
          #        belong to this PR (sourceBranch == refs/pull/<PR>/merge).
          #        For `check_run` the build id is parsed from a check payload
          #        we don't fully trust; for dispatch the build id and PR
          #        number are independent inputs. Validating on both paths
          #        prevents downloading an unrelated build or posting its
          #        analysis to the wrong PR.
          echo "ADO build ${BUILD_ID}: result='${RESULT}' definition='${DEF_ID}' sourceBranch='${SRC_BRANCH}'"
          if [ "${DEF_ID}" != "${ADO_BUILD_DEFINITION_ID}" ]; then
            echo "::warning::ADO build ${BUILD_ID} is definition '${DEF_ID}', not dotnet-sdk-public-ci (${ADO_BUILD_DEFINITION_ID}); refusing."; emit_none
          fi
          if [ "${RESULT}" != "failed" ]; then
            echo "::warning::ADO build ${BUILD_ID} did not fail (result='${RESULT}'); nothing to analyze."; emit_none
          fi
          if [ "${SRC_BRANCH}" != "refs/pull/${PR_NUMBER}/merge" ]; then
            echo "::warning::ADO build ${BUILD_ID} sourceBranch '${SRC_BRANCH}' does not match PR #${PR_NUMBER} (refs/pull/${PR_NUMBER}/merge); refusing to avoid posting to the wrong PR."; emit_none
          fi

          # Require the build's analyzed revision to equal the PR's CURRENT
          # head. gh-aw safe-output review comments carry no `commit_id` — they
          # target the current PR diff — so analyzing a stale revision would
          # produce inline suggestions that get rejected or land on the wrong
          # lines. If the PR has advanced since this build ran, skip: a newer
          # build/check for the current head will cover it.
          BUILD_PR_SHA=$(printf '%s' "${build_json}" | jq -r '.triggerInfo["pr.sourceSha"] // empty')
          CURRENT_HEAD=$(printf '%s' "${PR_JSON}" | jq -r '.head.sha // empty')
          # ADO builds GitHub's `refs/pull/<n>/merge` ref, so build_json.sourceVersion
          # is the merge commit GitHub produced at build time and equals the PR's
          # `merge_commit_sha` then. If the base branch advances (even with the PR
          # head unchanged) GitHub recomputes that merge and merge_commit_sha
          # changes, so this catches base-advance staleness the head check misses.
          BUILD_MERGE_SHA=$(printf '%s' "${build_json}" | jq -r '.sourceVersion // empty')
          CURRENT_MERGE=$(printf '%s' "${PR_JSON}" | jq -r '.merge_commit_sha // empty')
          # Fail CLOSED: if either the build's analyzed revision or the current
          # PR head can't be resolved, skip — we must not analyze a possibly
          # stale binlog against the current diff (inline comments have no
          # commit_id and target the current PR diff).
          if [ -z "${BUILD_PR_SHA}" ] || [ -z "${CURRENT_HEAD}" ]; then
            echo "::warning::Could not resolve build revision ('${BUILD_PR_SHA}') and/or current PR head ('${CURRENT_HEAD}'); skipping to avoid analyzing a stale binlog against the current diff."
            emit_none
          fi
          if [ "${BUILD_PR_SHA}" != "${CURRENT_HEAD}" ]; then
            echo "::warning::Build ${BUILD_ID} analyzed revision '${BUILD_PR_SHA}' but PR #${PR_NUMBER} head is now '${CURRENT_HEAD}'; skipping stale build (a newer build/check will cover the current revision)."
            emit_none
          fi
          # When both merge revisions are known and differ, the base branch moved
          # since the build — the binlog reflects an obsolete merge. Skip.
          if [ -n "${BUILD_MERGE_SHA}" ] && [ -n "${CURRENT_MERGE}" ] && [ "${BUILD_MERGE_SHA}" != "${CURRENT_MERGE}" ]; then
            echo "::warning::Build ${BUILD_ID} merge revision '${BUILD_MERGE_SHA}' but PR #${PR_NUMBER} current merge is '${CURRENT_MERGE}' (base branch advanced); skipping stale merge."
            emit_none
          fi
          # Consistent now: build revision == current PR head. Use it for
          # permalinks so they line up with the inline comments' diff target.
          HEAD_SHA="${CURRENT_HEAD}"
          echo "Analyzing build ${BUILD_ID} at PR head revision '${HEAD_SHA}'."

          # --- 5. Download every <Leg>_Logs_Attempt<N> artifact and extract binlogs ---
          # The SDK pipeline publishes one logs artifact per build leg, named
          # `<Leg>_Logs_Attempt<N>` (e.g. `Windows_x64_Logs_Attempt1`,
          # `Linux_arm64_AOT_Logs_Attempt1`), each containing that leg's
          # `log/<Configuration>/*.binlog`. A retried leg adds an `Attempt2`
          # artifact, which is matched too.
          artifacts_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}/artifacts?api-version=7.1")
          mapfile -t names < <(printf '%s' "${artifacts_json}" | jq -r '.value // [] | map(select(.name | test("_Logs_Attempt[0-9]+$"))) | .[].name')
          [ "${#names[@]}" -eq 0 ] && { echo "::warning::No *_Logs_Attempt* artifacts on build ${BUILD_ID}."; emit_none; }

          # --- 5a. Which failed legs never published logs at all? ---
          # The fail-closed check further down compares staged legs against the
          # artifacts ADO *returned*, so it cannot see a leg that died before
          # publishing its logs artifact — that leg is simply absent from
          # `names`. Ask the timeline which jobs failed and record any whose
          # logs never appeared. This is advisory rather than fail-closed: a
          # failed non-build job (e.g. `Monitor Helix Jobs`) legitimately
          # publishes no logs artifact, and skipping on that would suppress
          # analysis of real compile breaks in the same build. The agent is told
          # about the gap so it cannot conclude "no build failure" from the legs
          # that happened to upload.
          timeline_json=$(curl -sSL --retry 3 --max-time 60 "${ADO_API}/build/builds/${BUILD_ID}/timeline?api-version=7.1" 2>/dev/null || true)
          MISSING_LEGS=""
          if [ -n "${timeline_json}" ]; then
            while IFS= read -r jobname; do
              [ -z "${jobname}" ] && continue
              # Timeline job names use spaces where artifact names use
              # underscores: "Windows x64 AOT" -> "Windows_x64_AOT_Logs_Attempt1".
              prefix=$(printf '%s' "${jobname}" | tr ' ' '_')
              found=0
              for n in "${names[@]}"; do
                case "${n}" in
                  "${prefix}_Logs_Attempt"[0-9]*) found=1; break ;;
                esac
              done
              [ "${found}" -eq 0 ] && MISSING_LEGS="${MISSING_LEGS:+${MISSING_LEGS}, }${jobname}"
            done < <(printf '%s' "${timeline_json}" | jq -r '.records // [] | map(select(.type=="Job" and .result=="failed")) | .[].name' 2>/dev/null)
          fi
          if [ -n "${MISSING_LEGS}" ]; then
            echo "::warning::Failed leg(s) with no published logs artifact: ${MISSING_LEGS}"
          fi

          # Guards for untrusted PR-produced archives: cap the compressed
          # download and the reported uncompressed size per artifact, bound
          # extraction time, AND enforce a cumulative uncompressed budget across
          # all legs so many individually-small artifacts can't collectively
          # exhaust the runner's disk.
          MAX_ZIP_BYTES=524288000       # 500 MB compressed per artifact
          MAX_UNZIP_BYTES=2147483648    # 2 GB uncompressed per artifact
          MAX_TOTAL_BYTES=4294967296    # 4 GB uncompressed across all artifacts
          MAX_TOTAL_ZIP_BYTES=3221225472 # 3 GB compressed downloaded in total
          MAX_ARTIFACTS=40              # legs to process (SDK currently has 10)
          TOTAL_BYTES=0
          TOTAL_ZIP_BYTES=0
          # Bound the work before starting: a pipeline change (or repeated leg
          # retries adding Attempt<N> artifacts) could grow the matched set well
          # past today's 10. Refuse rather than process a prefix of the list,
          # because a partial view is exactly what the fail-closed check below
          # exists to prevent.
          if [ "${#names[@]}" -gt "${MAX_ARTIFACTS}" ]; then
            echo "::warning::Build ${BUILD_ID} matched ${#names[@]} *_Logs_Attempt* artifacts, above the ${MAX_ARTIFACTS} cap; skipping."
            emit_none
          fi
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
            # stream through `head -c` (cap + 1) and bound total time. This
            # closes the gap where `curl --max-filesize` alone would let a
            # length-less response write unbounded data before any post-check.
            curl -sSL --retry 3 --max-time 300 "${url}" 2>/dev/null | head -c $((MAX_ZIP_BYTES + 1)) > /tmp/a.zip || true
            ZIP_BYTES=$(stat -c%s /tmp/a.zip 2>/dev/null || echo 0)
            if [ "${ZIP_BYTES}" -eq 0 ]; then
              echo "::warning::Skipping ${name}: empty or failed download."; continue
            fi
            if [ "${ZIP_BYTES}" -gt "${MAX_ZIP_BYTES}" ]; then
              echo "::warning::Skipping ${name}: download exceeded ${MAX_ZIP_BYTES} bytes."; continue
            fi
            # Bound cumulative *compressed* bytes too. The per-artifact and
            # cumulative-uncompressed caps still allow many mid-sized archives
            # to be pulled over the network before any of them is inspected.
            if [ $((TOTAL_ZIP_BYTES + ZIP_BYTES)) -gt "${MAX_TOTAL_ZIP_BYTES}" ]; then
              echo "::warning::Cumulative compressed download budget ${MAX_TOTAL_ZIP_BYTES} reached at ${name}; stopping."; break
            fi
            TOTAL_ZIP_BYTES=$((TOTAL_ZIP_BYTES + ZIP_BYTES))
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
            echo "missing-legs=${MISSING_LEGS}"
          } >> "$GITHUB_OUTPUT"

      - name: Upload analysis artifact
        if: steps.fetch.outputs.binlog-found == 'true'
        uses: actions/upload-artifact@v7.0.1
        with:
          name: build-failure-analysis-data
          path: /tmp/binlogs
          if-no-files-found: warn
          retention-days: 1

# Steps that run in the agent job. Because the top-level `if:` gates activation
# on `needs.fetch-binlog.outputs.binlog-found == 'true'`, these only run once
# binlogs have been retrieved from the failed Azure DevOps build.
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
      GH_AW_MISSING_LEGS_VALUE: ${{ needs.fetch-binlog.outputs.missing-legs }}
      GH_AW_GITHUB_WORKSPACE: ${{ github.workspace }}
    run: |
      # The binlogs are mounted into the binlog-mcp container at
      # `/data/binlogs`. Build the list of in-container binlog paths (one per
      # build leg) that the agent should query. `GH_AW_BINLOG_PATH` is the
      # first entry for tools/prompts that expect a single path.
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
        echo "GH_AW_MISSING_LEGS=${GH_AW_MISSING_LEGS_VALUE}"
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
  # `check_run` carries no native issue/PR context for gh-aw, so the agent must
  # target the resolved PR explicitly (`target: "*"`) using `GH_AW_PR_NUMBER`.
  # NOTE: `target` only accepts "triggering", "*", or a literal number -- it is
  # resolved at compile time, so it cannot be pinned to the PR that
  # `fetch-binlog` resolves at run time (`safe_outputs` does not depend on that
  # job, so the expression would evaluate to an empty string and drop the
  # target). The containment for a prompt-injected agent is therefore the
  # read-only agent job, the separate `safe_outputs` job, and the max limits --
  # not the target. See the PR description for the residual-risk note.
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

  All build-failure analysis expertise (binlog parsing, error grouping,
  suggestion authoring) lives in the reusable agent at
  .github/agents/build-failure-analyst.agent.md.
-->
