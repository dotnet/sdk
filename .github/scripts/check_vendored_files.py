#!/usr/bin/env python3
"""
Detect drift between locally-vendored source files and their upstream sources.

Reads eng/vendored-files.json and, for each tracked file, compares the recorded
baseline blob SHA against the current blob SHA on the tracked branch via the
GitHub Contents API. When drift is detected, opens (or updates) a tracking
issue containing a unified diff of the upstream changes.

Modes:
    validate    - Structural validation only. No network calls. Safe for PRs.
    check       - Full drift detection. Requires GH_TOKEN. Creates/edits issues.

The check mode is idempotent: existing issues are matched by label
`area-vendored-sync` plus a hidden HTML marker in the body of the form
`<!-- vendored-sync:id=<entry-id>:<source-index> -->`.

See eng/vendored-files.md for the manifest schema and reconciliation workflow.
"""

from __future__ import annotations

import argparse
import base64
import difflib
import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[2]
MANIFEST_PATH = REPO_ROOT / "eng" / "vendored-files.json"
ISSUE_LABEL = "area-vendored-sync"
ISSUE_REPO = os.environ.get("VENDORED_SYNC_REPO", "dotnet/sdk")
MAX_DIFF_LINES = 300
SHA_RE = re.compile(r"^[0-9a-f]{40}$")
API_BASE = "https://api.github.com"
RAW_BASE = "https://raw.githubusercontent.com"


@dataclass
class Source:
    repo: str
    ref: str
    path: str
    baseline_ref_sha: str
    baseline_blob_sha: str
    scope: str

    @classmethod
    def from_dict(cls, d: dict[str, Any]) -> "Source":
        return cls(
            repo=d["repo"],
            ref=d["ref"],
            path=d["path"],
            baseline_ref_sha=d["baseline_ref_sha"],
            baseline_blob_sha=d["baseline_blob_sha"],
            scope=d.get("scope", ""),
        )


@dataclass
class Entry:
    id: str
    local_path: str
    notes: str
    sources: list[Source]

    @classmethod
    def from_dict(cls, d: dict[str, Any]) -> "Entry":
        return cls(
            id=d["id"],
            local_path=d["local_path"],
            notes=d.get("notes", ""),
            sources=[Source.from_dict(s) for s in d["sources"]],
        )


def load_manifest() -> list[Entry]:
    with MANIFEST_PATH.open("r", encoding="utf-8") as f:
        data = json.load(f)
    return [Entry.from_dict(e) for e in data["entries"]]


# ---------- validation ----------

def validate(entries: list[Entry]) -> int:
    errors: list[str] = []
    seen_ids: set[str] = set()
    for entry in entries:
        if not entry.id:
            errors.append("Entry missing id.")
            continue
        if entry.id in seen_ids:
            errors.append(f"Duplicate entry id: {entry.id}")
        seen_ids.add(entry.id)

        local = REPO_ROOT / entry.local_path
        if not local.is_file():
            errors.append(f"{entry.id}: local_path does not exist: {entry.local_path}")

        if not entry.sources:
            errors.append(f"{entry.id}: must declare at least one source.")

        for index, source in enumerate(entry.sources):
            tag = f"{entry.id}#sources[{index}]"
            if not re.match(r"^[\w.-]+/[\w.-]+$", source.repo):
                errors.append(f"{tag}: invalid repo '{source.repo}' (expected owner/name).")
            if not source.ref:
                errors.append(f"{tag}: missing ref.")
            if not source.path or source.path.startswith("/"):
                errors.append(f"{tag}: invalid path '{source.path}'.")
            if not SHA_RE.match(source.baseline_ref_sha):
                errors.append(f"{tag}: baseline_ref_sha must be a 40-char hex SHA.")
            if not SHA_RE.match(source.baseline_blob_sha):
                errors.append(f"{tag}: baseline_blob_sha must be a 40-char hex SHA.")

    if errors:
        print("Manifest validation FAILED:", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    total_sources = sum(len(entry.sources) for entry in entries)
    print(f"Manifest OK: {len(entries)} entries, {total_sources} sources.")
    return 0


# ---------- HTTP helpers ----------

def _request(url: str, accept: str = "application/vnd.github+json") -> tuple[int, bytes, dict[str, str]]:
    token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
    req = urllib.request.Request(url)
    req.add_header("Accept", accept)
    req.add_header("X-GitHub-Api-Version", "2022-11-28")
    req.add_header("User-Agent", "dotnet-sdk-vendored-sync")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return resp.status, resp.read(), dict(resp.headers)
    except urllib.error.HTTPError as e:
        return e.code, e.read(), dict(e.headers or {})


def api_json(path: str) -> tuple[int, Any]:
    url = f"{API_BASE}{path}"
    status, body, _ = _request(url)
    if not body:
        return status, None
    try:
        return status, json.loads(body)
    except json.JSONDecodeError:
        return status, None


def fetch_blob_content(repo: str, blob_sha: str) -> str | None:
    status, data = api_json(f"/repos/{repo}/git/blobs/{blob_sha}")
    if status != 200 or not isinstance(data, dict):
        return None
    if data.get("encoding") != "base64":
        return None
    try:
        return base64.b64decode(data["content"]).decode("utf-8", errors="replace")
    except (KeyError, ValueError):
        return None


def fetch_raw(repo: str, ref: str, path: str) -> str | None:
    url = f"{RAW_BASE}/{repo}/{ref}/{urllib.parse.quote(path)}"
    status, body, _ = _request(url, accept="text/plain")
    if status != 200:
        return None
    return body.decode("utf-8", errors="replace")


# ---------- drift detection ----------

@dataclass
class DriftResult:
    entry: Entry
    source_index: int
    source: Source
    status: str            # "drift", "missing", "ref_missing", "ok", "error"
    current_blob_sha: str | None
    current_ref_sha: str | None
    detail: str
    diff_text: str


def _resolve_ref_sha(repo: str, ref: str) -> str | None:
    status, data = api_json(f"/repos/{repo}/commits/{urllib.parse.quote(ref, safe='/')}")
    if status == 200 and isinstance(data, dict):
        return data.get("sha")
    return None


def _get_blob_sha(repo: str, ref: str, path: str) -> tuple[int, str | None]:
    status, data = api_json(
        f"/repos/{repo}/contents/{urllib.parse.quote(path)}?ref={urllib.parse.quote(ref, safe='/')}"
    )
    if status == 200 and isinstance(data, dict) and data.get("type") == "file":
        return status, data.get("sha")
    return status, None


def _truncate_diff(diff_lines: list[str], max_lines: int) -> str:
    if len(diff_lines) <= max_lines:
        return "".join(diff_lines)
    head = "".join(diff_lines[:max_lines])
    return f"{head}\n... (diff truncated; {len(diff_lines) - max_lines} more lines)"


def check_source(entry: Entry, source_index: int, source: Source) -> DriftResult:
    status, current_blob_sha = _get_blob_sha(source.repo, source.ref, source.path)

    if status == 404:
        return DriftResult(
            entry=entry, source_index=source_index, source=source,
            status="missing", current_blob_sha=None, current_ref_sha=None,
            detail=(
                f"Upstream path `{source.path}` no longer exists on "
                f"`{source.repo}@{source.ref}`. The file may have been renamed, "
                f"deleted, or the tracked ref was renamed."
            ),
            diff_text="",
        )

    if current_blob_sha is None:
        return DriftResult(
            entry=entry, source_index=source_index, source=source,
            status="error", current_blob_sha=None, current_ref_sha=None,
            detail=f"Unexpected response from contents API (HTTP {status}).",
            diff_text="",
        )

    if current_blob_sha == source.baseline_blob_sha:
        return DriftResult(
            entry=entry, source_index=source_index, source=source,
            status="ok", current_blob_sha=current_blob_sha, current_ref_sha=None,
            detail="", diff_text="",
        )

    current_ref_sha = _resolve_ref_sha(source.repo, source.ref)
    baseline_content = fetch_blob_content(source.repo, source.baseline_blob_sha)
    current_content = fetch_raw(source.repo, source.ref, source.path)

    if baseline_content is None or current_content is None:
        diff_text = "(unable to fetch one or both file revisions for diff)"
    else:
        diff_iter = difflib.unified_diff(
            baseline_content.splitlines(keepends=True),
            current_content.splitlines(keepends=True),
            fromfile=f"{source.path}@{source.baseline_blob_sha[:7]}",
            tofile=f"{source.path}@{(current_blob_sha or 'HEAD')[:7]}",
            lineterm="\n",
        )
        diff_text = _truncate_diff(list(diff_iter), MAX_DIFF_LINES)

    return DriftResult(
        entry=entry, source_index=source_index, source=source,
        status="drift", current_blob_sha=current_blob_sha, current_ref_sha=current_ref_sha,
        detail="Upstream blob SHA changed since the recorded baseline.",
        diff_text=diff_text,
    )


# ---------- issue rendering / updating ----------

def _marker(entry_id: str, source_index: int) -> str:
    return f"<!-- vendored-sync:id={entry_id}:{source_index} -->"


def _render_issue_body(result: DriftResult) -> str:
    entry = result.entry
    source = result.source
    marker = _marker(entry.id, result.source_index)
    cur_ref = result.current_ref_sha or source.ref
    base_ref = source.baseline_ref_sha
    repo = source.repo

    lines = [
        marker,
        "",
        f"Automated drift report for vendored file **`{entry.local_path}`**.",
        "",
        "## Source",
        f"- Upstream repo: [`{repo}`](https://github.com/{repo})",
        f"- Upstream path: [`{source.path}`](https://github.com/{repo}/blob/{cur_ref}/{source.path})",
        f"- Tracked ref: `{source.ref}`",
        f"- Scope: {source.scope or '_unspecified_'}",
        "",
        "## Drift",
    ]

    if result.status == "drift":
        lines += [
            f"- Baseline blob SHA: `{source.baseline_blob_sha}`",
            f"- Current  blob SHA: `{result.current_blob_sha}`",
            f"- Baseline ref SHA : `{base_ref}`",
            f"- Current  ref SHA : `{result.current_ref_sha or '(unknown)'}`",
            "",
            "### Links",
            f"- [File history]({_history_url(repo, source.ref, source.path)})",
            f"- [Baseline blob]({_blob_url(repo, base_ref, source.path)})",
            f"- [Current blob]({_blob_url(repo, cur_ref, source.path)})",
        ]
        if result.current_ref_sha:
            lines.append(f"- [Upstream ref compare]({_compare_url(repo, base_ref, result.current_ref_sha)}) (whole-repo diff, may be noisy)")
        lines += [
            "",
            "### Upstream-only diff",
            "```diff",
            result.diff_text.rstrip(),
            "```",
        ]
    else:
        lines += [
            f"- Status: **{result.status}**",
            f"- Detail: {result.detail}",
        ]

    if entry.notes:
        lines += ["", "## Local adaptation notes", entry.notes]

    lines += [
        "",
        "## How to reconcile",
        "1. Review the upstream changes and decide whether they should be ported.",
        f"2. Port the relevant changes to `{entry.local_path}`.",
        f"3. Update `eng/vendored-files.json` entry `{entry.id}` (source index {result.source_index}):",
        "   - bump `baseline_ref_sha` to the new upstream ref SHA,",
        "   - bump `baseline_blob_sha` to the new upstream blob SHA.",
        "4. Close this issue once the reconciliation PR is merged.",
        "",
        "If no port is needed (e.g. whitespace-only upstream change), still bump the baseline SHAs to silence future runs.",
    ]

    return "\n".join(lines)


def _history_url(repo: str, ref: str, path: str) -> str:
    return f"https://github.com/{repo}/commits/{ref}/{path}"


def _blob_url(repo: str, ref: str, path: str) -> str:
    return f"https://github.com/{repo}/blob/{ref}/{path}"


def _compare_url(repo: str, old_sha: str, new_sha: str) -> str:
    return f"https://github.com/{repo}/compare/{old_sha}...{new_sha}"


def _gh(args: list[str], input_data: str | None = None) -> tuple[int, str, str]:
    proc = subprocess.run(
        ["gh", *args], input=input_data, capture_output=True, text=True, check=False,
    )
    return proc.returncode, proc.stdout, proc.stderr


_issue_cache: list[dict[str, Any]] | None = None


def _list_open_sync_issues() -> list[dict[str, Any]]:
    """List all open issues with the sync label once and cache the result.

    Issue search with HTML-comment markers is unreliable, so we fetch the full
    label-filtered set (capped at 200, which is far more than we expect) and
    filter in Python by exact marker match.
    """
    global _issue_cache
    if _issue_cache is not None:
        return _issue_cache
    rc, out, err = _gh([
        "issue", "list",
        "--repo", ISSUE_REPO,
        "--label", ISSUE_LABEL,
        "--state", "open",
        "--limit", "200",
        "--json", "number,title,body",
    ])
    if rc != 0:
        print(f"gh issue list failed: {err}", file=sys.stderr)
        _issue_cache = []
        return _issue_cache
    try:
        _issue_cache = json.loads(out)
    except json.JSONDecodeError:
        _issue_cache = []
    return _issue_cache


def find_existing_issue(marker: str) -> dict[str, Any] | None:
    for item in _list_open_sync_issues():
        if marker in (item.get("body") or ""):
            return item
    return None


def ensure_label() -> None:
    """Ensure the sync label exists. `gh label create --force` is idempotent."""
    _gh([
        "label", "create", ISSUE_LABEL,
        "--repo", ISSUE_REPO,
        "--description", "Drift detected between a vendored source file and its upstream copy",
        "--color", "fbca04",
        "--force",
    ])


_STATUS_TITLES = {
    "drift": "drift detected",
    "missing": "upstream path missing",
    "error": "drift check error",
}


def upsert_issue(result: DriftResult, dry_run: bool) -> None:
    marker = _marker(result.entry.id, result.source_index)
    body = _render_issue_body(result)
    status_text = _STATUS_TITLES.get(result.status, result.status)
    title = f"[vendored-sync] {result.entry.id}: {status_text} (#{result.source_index})"

    if dry_run:
        print(f"\n--- would create/update issue ({result.entry.id}#{result.source_index}) ---")
        print(f"title: {title}")
        print(body[:2000])
        print("---")
        return

    existing = find_existing_issue(marker)
    if existing is None:
        rc, out, err = _gh([
            "issue", "create",
            "--repo", ISSUE_REPO,
            "--title", title,
            "--label", ISSUE_LABEL,
            "--body-file", "-",
        ], input_data=body)
        if rc != 0:
            print(f"Failed to create issue for {result.entry.id}: {err}", file=sys.stderr)
        else:
            print(f"Created issue for {result.entry.id}#{result.source_index}: {out.strip()}")
        return

    if (existing.get("body") or "").strip() == body.strip():
        print(f"Issue #{existing['number']} already up to date for {result.entry.id}#{result.source_index}.")
        return

    rc, _, err = _gh([
        "issue", "edit", str(existing["number"]),
        "--repo", ISSUE_REPO,
        "--body-file", "-",
    ], input_data=body)
    if rc != 0:
        print(f"Failed to update issue #{existing['number']}: {err}", file=sys.stderr)
    else:
        print(f"Updated issue #{existing['number']} for {result.entry.id}#{result.source_index}.")


# ---------- entry points ----------

def cmd_validate(_args: argparse.Namespace) -> int:
    return validate(load_manifest())


def cmd_check(args: argparse.Namespace) -> int:
    if not (os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")):
        print("GH_TOKEN or GITHUB_TOKEN must be set for `check` mode.", file=sys.stderr)
        return 2

    entries = load_manifest()
    if validate(entries) != 0:
        return 1

    if not args.dry_run:
        ensure_label()

    drift_count = 0
    error_count = 0
    for entry in entries:
        for index, source in enumerate(entry.sources):
            result = check_source(entry, index, source)
            short_id = f"{entry.id}#{index}"
            if result.status == "ok":
                print(f"[ok]      {short_id} ({source.repo}:{source.path})")
                continue
            if result.status == "error":
                error_count += 1
                print(f"[ERROR]   {short_id}: {result.detail}", file=sys.stderr)
                continue
            drift_count += 1
            print(f"[{result.status:<7}] {short_id}: {result.detail}")
            upsert_issue(result, args.dry_run)

    print(f"\nSummary: {drift_count} drifted, {error_count} errors.")
    return 1 if error_count else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    p_validate = sub.add_parser("validate", help="Validate manifest structure (no network).")
    p_validate.set_defaults(func=cmd_validate)

    p_check = sub.add_parser("check", help="Run drift detection against upstream repos.")
    p_check.add_argument("--dry-run", action="store_true", help="Skip creating/updating issues; print what would happen.")
    p_check.set_defaults(func=cmd_check)

    args = parser.parse_args()
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
