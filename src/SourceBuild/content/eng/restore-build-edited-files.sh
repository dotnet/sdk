#!/bin/bash

# Help message
show_help() {
    echo "Script to perform git restore for a set of commonly edited paths when building the VMR."
    echo ""
    echo "Usage: $0 [-h] [-p <path>] [-n]"
    echo ""
    echo "Options:"
    echo "  -h, --help               Show this help message and exit"
    echo "  -p, --path <path>        Specify the paths to be restored (default: src/*/eng/common/*, src/*global.json)"
    echo "  -y, --noprompt           Skip the confirmation prompt"
    echo ""
    echo "Example:"
    echo "  $0 -p \"src/*/eng/common/*\" -p \"src/*global.json\""
    echo ""
    exit 0
}

source="${BASH_SOURCE[0]}"
# resolve $source until the file is no longer a symlink
while [[ -h "$source" ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"
  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

# Default paths to restore
DefaultPathsToRestore=(
    "src/*/eng/common/*"
    "src/*global.json"
)
PathsToRestore=()
NoPrompt=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            show_help
            ;;
        -p|--path)
            shift
            PathsToRestore+=("$1")
            ;;
        -y|--noprompt)
            NoPrompt=true
            ;;
        *)
            echo "Invalid option: $1"
            show_help
            ;;
    esac
    shift
done

# Use default paths if no paths are passed
if [ ${#PathsToRestore[@]} -eq 0 ]; then
    PathsToRestore=("${DefaultPathsToRestore[@]}")
fi

# Confirmation prompt
if [ "$NoPrompt" = false ]; then
    echo "Will restore changes in the following paths:"
    for path in "${PathsToRestore[@]}"; do
        echo "  $path"
    done
    read -p "Do you want to proceed with restoring the paths? (Y/N)" choice
    if [[ ! "$choice" =~ ^[Yy]$ ]]; then
        exit 0
    fi
fi

# Perform git restore for each path
for path in "${PathsToRestore[@]}"; do
    git -C "$(dirname "$scriptroot")" restore "$path"
done
