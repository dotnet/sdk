#!/usr/bin/env bash
# Measures the NativeAOT binary-size impact of the current branch versus a baseline ref by
# publishing dotnet-aot on both sides and diffing the .mstat files with sizoscope-cli.
#
# Replicates .github/workflows/aot-size-analysis.yml locally:
#   1. Publishes src/Cli/dotnet-aot (Release, full ILC) for the current worktree (the "PR" side).
#   2. Adds a detached git worktree at the baseline ref and publishes the same project there.
#   3. Runs sizoscope-cli on the two dotnet-aot.mstat files to produce a per-symbol diff.
#   4. Emits a Markdown summary (raw native dll delta + sizoscope accounted difference + the full
#      diff) ready to paste into a PR description.
#
# The PowerShell sibling Measure-AotSize.ps1 is the canonical implementation; this is the bash
# port for Linux/macOS shells.
#
# Usage:
#   scripts/measure-aot-size.sh [--rid <rid>] [--configuration <cfg>] [--base-ref <ref>]
#                               [--output-path <file>] [--skip-baseline]
set -euo pipefail

rid="win-x64"
configuration="Release"
base_ref=""
output_path=""
skip_baseline=0

usage() { awk 'NR==1{next} /^#/{sub(/^# ?/,"");print;next} {exit}' "$0"; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid) rid="$2"; shift 2;;
    --configuration|-c) configuration="$2"; shift 2;;
    --base-ref) base_ref="$2"; shift 2;;
    --output-path|-o) output_path="$2"; shift 2;;
    --skip-baseline) skip_baseline=1; shift;;
    -h|--help) usage; exit 0;;
    *) echo "Unknown option: $1" >&2; usage; exit 1;;
  esac
done

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

find_repo_root() {
  local dir="$1"
  while [[ -n "$dir" && ! -e "$dir/.git" ]]; do
    local parent; parent="$(dirname "$dir")"
    [[ "$parent" == "$dir" ]] && break
    dir="$parent"
  done
  if [[ ! -e "$dir/.git" ]]; then
    echo "Could not locate the repository root from $1." >&2; exit 1
  fi
  ( cd "$dir" && pwd )
}

file_size() {
  if stat -c %s "$1" >/dev/null 2>&1; then stat -c %s "$1"; else stat -f %z "$1"; fi
}

repo_root="$(find_repo_root "$script_dir")"

dotnet_name="dotnet"
[[ "${OS:-}" == "Windows_NT" ]] && dotnet_name="dotnet.exe"
dotnet="$repo_root/.dotnet/$dotnet_name"
if [[ ! -f "$dotnet" ]]; then
  echo "Repo-local SDK not found at $dotnet. Run ./restore.sh (or ./restore.cmd) first." >&2
  exit 1
fi

if [[ -z "$base_ref" ]]; then
  base_ref="$(git -C "$repo_root" merge-base HEAD origin/main)"
fi
base_short="${base_ref:0:8}"
[[ -z "$output_path" ]] && output_path="$repo_root/artifacts/aot-size-$rid.md"
base_worktree="$(dirname "$repo_root")/_aot-size-base-$rid"

echo "Repo:      $repo_root"
echo "Base ref:  $base_ref ($base_short)"
echo "RID:       $rid    Config: $configuration"

resolve_sizoscope() {
  local tools_dir="$HOME/.dotnet/tools"
  case ":$PATH:" in
    *":$tools_dir:"*) ;;
    *) export PATH="$tools_dir:$PATH";;
  esac
  if command -v sizoscope-cli >/dev/null 2>&1; then return; fi
  echo "==> Installing sizoscope-cli (global tool)..."
  "$dotnet" tool install --global sizoscope-cli
  if ! command -v sizoscope-cli >/dev/null 2>&1; then
    echo "sizoscope-cli is not on PATH after install. Open a new shell or check ~/.dotnet/tools." >&2
    exit 1
  fi
}

publish_dotnet_aot() {
  local project_root="$1" label="$2"
  local proj="$project_root/src/Cli/dotnet-aot/dotnet-aot.csproj"
  echo "==> Publishing dotnet-aot ($label): $configuration / $rid"
  local binlog="$project_root/aot-size-$label.binlog"
  "$dotnet" publish "$proj" -c "$configuration" -r "$rid" "/bl:$binlog"
  rm -f "$binlog"
}

find_native() {
  local root="$1" leaf="$2" base
  if [[ "$leaf" == "dotnet-aot.dll" ]]; then
    base="$root/artifacts/bin/dotnet-aot/$configuration"
  else
    base="$root/artifacts/obj/dotnet-aot/$configuration"
  fi
  local hit
  hit="$(find "$base" -type f -name "$leaf" -path '*native*' 2>/dev/null | head -n 1)"
  if [[ -z "$hit" ]]; then
    echo "Could not find native/$leaf under $base." >&2; exit 1
  fi
  printf '%s\n' "$hit"
}

resolve_sizoscope

# --- PR side (current worktree) ---
publish_dotnet_aot "$repo_root" "pr"
pr_mstat="$(find_native "$repo_root" "dotnet-aot.mstat")"
pr_dll="$(find_native "$repo_root" "dotnet-aot.dll")"

# --- Baseline side (temporary detached worktree) ---
created_worktree=0
cleanup() {
  if [[ "$created_worktree" -eq 1 && "$skip_baseline" -eq 0 ]]; then
    echo "==> Removing baseline worktree..."
    git -C "$repo_root" worktree remove --force "$base_worktree" 2>/dev/null || true
  fi
}
trap cleanup EXIT

if ! { [[ "$skip_baseline" -eq 1 ]] && [[ -d "$base_worktree" ]]; }; then
  if [[ -d "$base_worktree" ]]; then
    git -C "$repo_root" worktree remove --force "$base_worktree" 2>/dev/null || true
  fi
  echo "==> Adding baseline worktree at $base_short"
  git -C "$repo_root" worktree add --detach "$base_worktree" "$base_ref"
  created_worktree=1
  publish_dotnet_aot "$base_worktree" "base"
fi
base_mstat="$(find_native "$base_worktree" "dotnet-aot.mstat")"
base_dll="$(find_native "$base_worktree" "dotnet-aot.dll")"

# --- Diff ---
mkdir -p "$repo_root/artifacts"
diff_file="$repo_root/artifacts/aot-size-$rid.sizoscope.txt"
echo "==> Running sizoscope-cli diff..."
sizoscope-cli "$base_mstat" "$pr_mstat" --output "$diff_file"
[[ -f "$diff_file" ]] || { echo "sizoscope-cli did not produce an output file." >&2; exit 1; }

total_line="$(head -n 1 "$diff_file")"
base_len="$(file_size "$base_dll")"
pr_len="$(file_size "$pr_dll")"
delta=$(( pr_len - base_len ))
accounted="${total_line#Total accounted size difference: }"

base_mb="$(awk "BEGIN{printf \"%.3f\", $base_len/1048576}")"
pr_mb="$(awk "BEGIN{printf \"%.3f\", $pr_len/1048576}")"
delta_kb="$(awk "BEGIN{printf \"%+.0f\", $delta/1024}")"
pct="$(awk "BEGIN{ if ($base_len>0) printf \"%+.2f\", $delta/$base_len*100; else printf \"%+.2f\", 0}")"

{
  echo "## AOT size impact ($rid)"
  echo ""
  echo "Published \`dotnet-aot\` ($configuration, \`$rid\`, full ILC) on this branch vs. base (\`$base_short\`); \`.mstat\` files diffed with \`sizoscope-cli\`."
  echo ""
  echo "| Native \`dotnet-aot.dll\` | Size |"
  echo "| --- | --- |"
  echo "| Base (\`$base_short\`) | ${base_mb} MB (${base_len} B) |"
  echo "| This branch | ${pr_mb} MB (${pr_len} B) |"
  echo "| **Delta** | **${delta_kb} KB (${pct}%)** |"
  echo ""
  echo "\`sizoscope-cli\` accounted difference: **${accounted}**"
  echo ""
  echo "<details><summary>sizoscope-cli diff</summary>"
  echo ""
  echo '```'
  cat "$diff_file"
  echo '```'
  echo ""
  echo "</details>"
} > "$output_path"

echo ""
echo "Summary written to: $output_path"
echo "Raw native dll delta: ${delta_kb} KB (${pct}%)"
echo "Accounted: $total_line"
