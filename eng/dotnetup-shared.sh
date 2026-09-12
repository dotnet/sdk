#!/usr/bin/env bash

# Shared helpers for acquiring dotnetup, sourced by both eng/configure-toolset.sh
# (bootstrap SDK install) and eng/restore-toolset.sh (test runtime install).

# This file only defines functions; it has no top-level side effects so it is safe to source multiple times.

# General SDK build helpers (GetNativeMachineArchitecture, etc.).
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/sdk-tools.sh"

# Returns success (0) when an already-downloaded dotnetup binary at $1 is recent
# enough (<24h old) and architecturally compatible with the native machine, so the
# download can be skipped. Returns non-zero when dotnetup should be (re)downloaded.
function ShouldUseCachedDotnetup {
  local dotnetup_exe=$1
  [[ -f "$dotnetup_exe" ]] || return 1

  # Re-download dotnetup at most once every 24 hours to avoid unnecessary network calls.
  local current_time file_time age_seconds
  current_time=$(date +%s)
  file_time=$(stat -c %Y "$dotnetup_exe" 2>/dev/null || stat -f %m "$dotnetup_exe" 2>/dev/null || echo 0)
  age_seconds=$((current_time - file_time))
  if [[ $age_seconds -ge 86400 ]]; then
    return 1
  fi
  echo "dotnetup binary is less than 24 hours old; skipping re-download."

  # dotnetup installs runtimes for its own process architecture, so a cached
  # binary of the wrong architecture (e.g. an x64 dotnetup left on a reused
  # arm64 agent, or one downloaded under Rosetta 2) would install the wrong
  # runtimes. Verify the cached binary's actual architecture against the native
  # architecture and re-download on mismatch rather than trusting uname.
  if [[ "$(uname)" == "Darwin" ]]; then
    local native_arch cached_arch=""
    native_arch="$(GetNativeMachineArchitecture)"
    if file "$dotnetup_exe" 2>/dev/null | grep -q 'arm64'; then
      cached_arch="arm64"
    elif file "$dotnetup_exe" 2>/dev/null | grep -q 'x86_64'; then
      cached_arch="x64"
    fi
    if [[ -n "$cached_arch" && "$cached_arch" != "$native_arch" ]]; then
      echo "Cached dotnetup architecture ($cached_arch) does not match native architecture ($native_arch); re-downloading."
      return 1
    fi
  fi

  return 0
}

# Follows redirects for a mutable 'daily' shortlink and prints the concrete,
# versioned URL it currently points at (empty if it cannot be resolved). The daily
# aka.ms link is a moving pointer, so downloading the script and its .sha512 as two
# separate requests can straddle a new build publish and yield a script from one
# build with a checksum from another. Resolving the shortlink to a single concrete
# URL first lets us derive both the script and checksum URLs from the same build so
# they always match. This mirrors the standalone get-dotnetup script's own binary
# check, but is kept as a deliberately separate copy here because that script does
# not exist in this branch and must stand alone.
function ResolveDotnetupFinalUrl {
  local url="$1"
  if command -v curl > /dev/null 2>&1; then
    # --head resolves redirects without downloading the body.
    curl --silent --show-error --location --head --output /dev/null \
      --write-out '%{url_effective}' "$url" 2>/dev/null
  elif command -v wget > /dev/null 2>&1; then
    # wget lacks --write-out; --spider -S prints the redirect headers, whose final 'Location:' is the concrete build URL. tolower() keeps awk portable.
    wget --spider -S "$url" 2>&1 \
      | awk 'tolower($1) == "location:" { u = $2 } END { if (u != "") print u }'
  fi
  # An empty result falls back to the shortlink URLs used by the caller.
}

# Downloads the public dotnetup installer from aka.ms
# (https://aka.ms/dotnet/dotnetup/daily/get-dotnetup.sh), verifies its SHA-512
# checksum, and runs it to install dotnetup into the directory given by $1. Returns
# non-zero on failure. Callers run under `set -e`, so invoke via
# `if ! AcquireDotnetup ...; then` to handle failure.
#
# If a local get-dotnetup.sh script exists in the repo (scripts/get-dotnetup.sh),
# it is used directly instead of downloading from aka.ms. This supports branches
# (e.g. release/dnup) that carry the script locally and avoids merge conflicts
# when code flows between branches with and without the local script.
function AcquireDotnetup {
  local dotnetup_dir=$1
  local script_dir
  script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  local repo_root
  repo_root="$(cd "$script_dir/.." && pwd)"
  local local_getter="$repo_root/scripts/get-dotnetup.sh"

  # Prefer the repo-local script when available (e.g. on release/dnup).
  if [[ -f "$local_getter" ]]; then
    echo "Using local get-dotnetup.sh from '$local_getter'."
    bash "$local_getter" --install-dir "$dotnetup_dir"
    return $?
  fi

  local getter_url="https://aka.ms/dotnet/dotnetup/daily/get-dotnetup.sh"
  local checksum_url="${getter_url}.sha512"

  # Pin the mutable 'daily' shortlink to the concrete build it currently resolves
  # to, then derive both the script and checksum URLs from that single build so a
  # publish happening mid-download cannot cause a spurious checksum mismatch.
  local resolved_url
  resolved_url="$(ResolveDotnetupFinalUrl "$getter_url" || true)"
  if [[ -n "$resolved_url" && "$resolved_url" == *"/public/"* ]]; then
    echo "Resolved get-dotnetup.sh to concrete build: $resolved_url"
    getter_url="$resolved_url"
    # Checksums live under the sibling 'public-checksums' path. Use sed, not bash
    # ${var/p/r}, since bash 3.2 (macOS) keeps the escaped backslashes literally.
    checksum_url="$(printf '%s' "$resolved_url" | sed 's#/public/#/public-checksums/#').sha512"
  else
    echo "Could not resolve get-dotnetup.sh shortlink to a concrete build; using shortlink URLs directly."
  fi

  local getter_script checksum_file
  # Use an explicit template: bare `mktemp` is not portable because BSD/macOS
  # mktemp requires a template (or -t prefix) and errors without one.
  getter_script="$(mktemp "${TMPDIR:-/tmp}/get-dotnetup.XXXXXX")"
  checksum_file="${getter_script}.sha512"

  local downloader=""
  if command -v curl > /dev/null 2>&1; then
    downloader="curl"
  elif command -v wget > /dev/null 2>&1; then
    downloader="wget"
  else
    echo "Cannot download dotnetup: neither 'curl' nor 'wget' is available on PATH. Install one of them to acquire dotnetup." >&2
    rm -f "$getter_script" "$checksum_file"
    return 1
  fi

  DownloadDotnetupFile() {
    local url="$1" out="$2"
    if [[ "$downloader" == "curl" ]]; then
      curl -fsSL --retry 3 "$url" -o "$out"
    else
      wget -q --tries=3 -O "$out" "$url"
    fi
  }

  local result=0
  if ! DownloadDotnetupFile "$getter_url" "$getter_script"; then
    echo "Failed to download dotnetup installer from $getter_url" >&2
    result=1
  elif ! DownloadDotnetupFile "$checksum_url" "$checksum_file"; then
    echo "Failed to download dotnetup installer checksum from $checksum_url" >&2
    result=1
  else
    local expected actual
    expected="$(awk '{print tolower($1)}' "$checksum_file")"
    if command -v sha512sum > /dev/null 2>&1; then
      actual="$(sha512sum "$getter_script" | awk '{print tolower($1)}')"
    elif command -v shasum > /dev/null 2>&1; then
      actual="$(shasum -a 512 "$getter_script" | awk '{print tolower($1)}')"
    else
      echo "Cannot verify dotnetup installer: neither 'sha512sum' nor 'shasum' is available on PATH." >&2
      result=1
    fi

    if [[ "$result" -eq 0 && "$expected" != "$actual" ]]; then
      echo "get-dotnetup.sh checksum mismatch." >&2
      echo "  Expected: $expected" >&2
      echo "  Actual:   $actual" >&2
      result=1
    fi

    if [[ "$result" -eq 0 ]]; then
      echo "get-dotnetup.sh checksum verified."
      if ! bash "$getter_script" --install-dir "$dotnetup_dir"; then
        result=1
      fi
    fi
  fi

  rm -f "$getter_script" "$checksum_file"
  return $result
}

# Runs a command with bash 'errexit' (set -e) temporarily disabled so that a non-zero exit code does not abort the calling script
function RunWithoutErrexit {
  local restore_errexit=false
  if [[ $- == *e* ]]; then
    restore_errexit=true
    set +e
  fi
  "$@"
  _RunWithoutErrexit=$?
  if [[ "$restore_errexit" == true ]]; then
    set -e
  fi
  return 0
}
