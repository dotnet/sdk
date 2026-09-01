#!/usr/bin/env bash

# General-purpose shared helpers for the SDK repo's build/test scripts.
# Dot-source this file to reuse the functions below; it defines functions only
# and has no top-level side effects, so it is safe to source multiple times.
#
# This is the repo-owned counterpart to arcade's eng/common/tools.sh: put shared
# logic that is NOT specific to a single feature here. (eng/common is managed by
# arcade and changes there are overwritten, so it cannot host repo-owned helpers.)

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
