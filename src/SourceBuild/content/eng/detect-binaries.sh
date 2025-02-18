#!/usr/bin/env bash

### Usage: $0
###
###   Prepares and runs the binary tooling to detect binaries in the VMR. Default behavior is to report any binaries
###   not found in the allowed-binaries file. To remove binaries not specified in the allowed-binaries file, pass --clean.
###
### Options:
###   --clean                           Clean the VMR of binaries not in the specified allowed-binaries file.
###   --allowed-binaries-file <file>    Path to the file containing the list of binaries to be
###                                     ignored for either cleaning or validating.
###                                     Defaults to eng/allowed-vmr-binaries.txt.
###   --log-level <level>               Set the log level for the binary tooling. Defaults to Debug.

set -euo pipefail
IFS=$'\n\t'

source="${BASH_SOURCE[0]}"
REPO_ROOT="$( cd -P "$( dirname "$0" )/../" && pwd )"
BINARY_TOOL="BinaryToolKit"
VMR_TOOLS="$REPO_ROOT/eng/vmr-tools.sh"

# Set default values
allowedBinariesFile="$REPO_ROOT/eng/allowed-vmr-binaries.txt"
mode='validate'
logLevel='Debug'
targetDir="$REPO_ROOT"
outputDir="$REPO_ROOT/artifacts/log/binary-report"

function print_help
{
  sed -n '/^### /,/^$/p' "$source" | cut -b 5-
  "$VMR_TOOLS" --help advanced
}

positional_args=()
while :; do
  if [ $# -le 0 ]; then
    break
  fi
  lowerI="$(echo "$1" | awk '{print tolower($0)}')"
  case $lowerI in
    "-?"|-h|--help)
      print_help
      exit 0
      ;;
    --clean)
      mode="clean"
      ;;
    --allowed-binaries-file)
      allowedBinariesFile="$2"
      if [ ! -f "$allowedBinariesFile" ]; then
        echo "ERROR: The specified allowed-binaries file does not exist."
        exit 1
      fi
      shift
      ;;
    --log-level)
      logLevel=$2
      shift
      ;;
    *)
      if [ -n "$1" ]; then
        positional_args+=("$1")
      fi
      ;;
  esac

  shift
done

"$VMR_TOOLS" --tool "$BINARY_TOOL" "${positional_args[@]}" "$mode" "$targetDir" -o "$outputDir" -ab "$allowedBinariesFile" -l "$logLevel"
