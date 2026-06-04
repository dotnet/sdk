#!/usr/bin/env bash

# Shared helpers for acquiring dotnetup, sourced by both eng/configure-toolset.sh
# (bootstrap SDK install) and eng/restore-toolset.sh (test runtime install).
# This file only defines functions; it has no top-level side effects so it is
# safe to source multiple times.

# Detect native machine architecture, handling macOS Rosetta 2
# where uname -m may report x86_64 on arm64 hardware.
function GetNativeMachineArchitecture {
  if [[ "$(uname)" == "Darwin" ]] && [[ "$(sysctl -n hw.optional.arm64 2>/dev/null)" == "1" ]]; then
    echo "arm64"
    return
  fi
  case "$(uname -m)" in
    arm64|aarch64) echo "arm64" ;;
    amd64|x86_64) echo "x64" ;;
    armv*l) echo "arm" ;;
    i[3-6]86) echo "x86" ;;
    *) echo "x64" ;;
  esac
}

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

# Downloads the public dotnetup installer from aka.ms
# (https://aka.ms/dotnetup/get-dotnetup.sh) and runs it to install dotnetup into
# the directory given by $1. Returns non-zero on failure. Callers run under
# `set -e`, so invoke via `if ! AcquireDotnetup ...; then` to handle failure.
function AcquireDotnetup {
  local dotnetup_dir=$1
  local getter_url="https://aka.ms/dotnetup/get-dotnetup.sh"
  local getter_script
  getter_script="$(mktemp)"

  local downloaded=false
  if command -v curl > /dev/null 2>&1; then
    if curl -fsSL --retry 3 "$getter_url" -o "$getter_script"; then downloaded=true; fi
  elif command -v wget > /dev/null 2>&1; then
    if wget -q -O "$getter_script" "$getter_url"; then downloaded=true; fi
  fi

  local result=0
  if [[ "$downloaded" != true ]] || ! bash "$getter_script" --install-dir "$dotnetup_dir"; then
    result=1
  fi

  rm -f "$getter_script"
  return $result
}
