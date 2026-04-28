#!/usr/bin/env bash
# get-dotnetup.sh
#
# Downloads the latest dotnetup binary from the dotnetup CI pipeline and installs it locally.
#
# Uses the Azure CLI (az) with the azure-devops extension for all Azure DevOps interactions.
# You must be logged in with 'az login' and have access to the dnceng/internal project.
#
# Usage:
#   ./get-dotnetup.sh
#   ./get-dotnetup.sh --install-dir /opt/dotnetup
#   ./get-dotnetup.sh --runtime-id linux-musl-x64
#   ./get-dotnetup.sh --branch release/dnup

set -euo pipefail

# --- Defaults ---
INSTALL_DIR="$HOME/.dotnetup"
BRANCH="release/dnup"
RUNTIME_ID=""
AZDO_ORG="https://dev.azure.com/dnceng"
AZDO_PROJECT="internal"
PIPELINE_ID=1544

# --- Colors (disabled if not a terminal) ---
if [ -t 1 ]; then
    CYAN='\033[0;36m'
    GREEN='\033[0;32m'
    YELLOW='\033[1;33m'
    RED='\033[0;31m'
    GRAY='\033[0;90m'
    NC='\033[0m'
else
    CYAN='' GREEN='' YELLOW='' RED='' GRAY='' NC=''
fi

info()  { printf "${CYAN}%s${NC}\n" "$*"; }
ok()    { printf "${GREEN}%s${NC}\n" "$*"; }
warn()  { printf "${YELLOW}%s${NC}\n" "$*"; }
err()   { printf "${RED}%s${NC}\n" "$*" >&2; }
gray()  { printf "${GRAY}%s${NC}\n" "$*"; }

# --- Parse arguments ---
while [[ $# -gt 0 ]]; do
    case "$1" in
        --install-dir)  INSTALL_DIR="$2"; shift 2 ;;
        --branch)       BRANCH="$2"; shift 2 ;;
        --runtime-id)   RUNTIME_ID="$2"; shift 2 ;;
        --help|-h)
            cat <<EOF
Usage: $(basename "$0") [OPTIONS]

Downloads the latest dotnetup binary from CI and installs it locally.
Requires the Azure CLI (az) with the azure-devops extension for authentication
and artifact download. Log in with 'az login' first.

Options:
  --install-dir DIR     Installation directory (default: ~/.dotnetup)
  --branch BRANCH       Branch to get the latest build from (default: release/dnup)
  --runtime-id RID      Override OS/architecture detection (e.g. linux-musl-x64, osx-arm64)
  --help, -h            Show this help message

Prerequisites:
  Azure CLI (az)        https://aka.ms/install-az-cli
  azure-devops ext      az extension add --name azure-devops

Examples:
  az login
  ./get-dotnetup.sh
  ./get-dotnetup.sh --runtime-id linux-musl-x64
EOF
            exit 0
            ;;
        *)
            err "Unknown option: $1"
            err "Run '$(basename "$0") --help' for usage."
            exit 1
            ;;
    esac
done

# --- Check prerequisites ---
if ! command -v az &>/dev/null; then
    err "Azure CLI (az) is required but was not found on PATH."
    err "Install it from: https://aka.ms/install-az-cli"
    err ""
    err "After installing, log in with:"
    err "  az login"
    exit 1
fi

if ! az extension show --name azure-devops &>/dev/null; then
    err "The 'azure-devops' Azure CLI extension is required but is not installed."
    err "Install it with:"
    err "  az extension add --name azure-devops"
    exit 1
fi

# --- Detect runtime ID ---
detect_rid() {
    if [ -n "$RUNTIME_ID" ]; then
        echo "$RUNTIME_ID"
        return
    fi

    local os arch

    # Detect OS
    case "$(uname -s)" in
        Linux)
            # Detect musl vs glibc
            if is_musl; then
                os="linux-musl"
            else
                os="linux"
            fi
            ;;
        Darwin)
            os="osx"
            ;;
        *)
            err "Unsupported OS: $(uname -s). Use --runtime-id to specify a RID manually."
            exit 1
            ;;
    esac

    # Detect architecture
    case "$(uname -m)" in
        x86_64|amd64)   arch="x64" ;;
        aarch64|arm64)   arch="arm64" ;;
        *)
            err "Unsupported architecture: $(uname -m). Use --runtime-id to specify a RID manually."
            exit 1
            ;;
    esac

    echo "${os}-${arch}"
}

is_musl() {
    # Layer 1: Check if getconf reports glibc
    if getconf GNU_LIBC_VERSION &>/dev/null; then
        return 1  # glibc
    fi

    # Layer 2: Check ldd --version output for "musl"
    if ldd --version 2>&1 | grep -qi "musl"; then
        return 0  # musl
    fi

    # Layer 3: Check if /lib contains musl libc
    if ls /lib/ld-musl-* &>/dev/null; then
        return 0  # musl
    fi

    # Default: assume glibc
    return 1
}

# --- Main ---

RID=$(detect_rid)
info "Detected runtime: $RID"

# Get latest successful build for the specified branch
info "Querying for latest successful build on $BRANCH..."

RUN_ID=$(az pipelines runs list \
    --pipeline-ids "$PIPELINE_ID" \
    --branch "$BRANCH" \
    --status completed \
    --query-order FinishTimeDesc \
    --top 10 \
    --org "$AZDO_ORG" \
    --project "$AZDO_PROJECT" \
    --query "[?result=='succeeded' || result=='partiallySucceeded'] | [0].id" \
    --output tsv) || {
    err "Failed to query pipeline runs. Ensure you are logged in and have access to dnceng/internal:"
    err ""
    err "  az login"
    err "  az extension add --name azure-devops   # if not already installed"
    err ""
    err "Error: $RUN_ID"
    exit 1
}

if [ -z "$RUN_ID" ] || [ "$RUN_ID" = "None" ]; then
    err "No successful builds found for pipeline $PIPELINE_ID on branch $BRANCH."
    exit 1
fi

ok "Found build $RUN_ID"
gray "  ${AZDO_ORG}/${AZDO_PROJECT}/_build/results?buildId=${RUN_ID}"

# Download the artifact
ARTIFACT_NAME="dotnetup-standalone-${RID}"
info "Downloading artifact '$ARTIFACT_NAME'..."

TEMP_DIR=$(mktemp -d)

cleanup() {
    rm -rf "$TEMP_DIR"
}
trap cleanup EXIT

az pipelines runs artifact download \
    --artifact-name "$ARTIFACT_NAME" \
    --path "$TEMP_DIR" \
    --run-id "$RUN_ID" \
    --org "$AZDO_ORG" \
    --project "$AZDO_PROJECT" 2>&1 || {
    err "Failed to download artifact '$ARTIFACT_NAME' for run $RUN_ID."
    err "Available RIDs: win-x64, win-arm64, linux-x64, linux-arm64, linux-musl-x64, linux-musl-arm64, osx-x64, osx-arm64"
    err "Use --runtime-id to specify the correct RID."
    exit 1
}

# Find the binary in the downloaded contents
BINARY_PATH=$(find "$TEMP_DIR" -name "dotnetup" -type f | head -1)
if [ -z "$BINARY_PATH" ]; then
    err "Could not find 'dotnetup' binary in the downloaded artifact."
    err "Contents:"
    find "$TEMP_DIR" -type f | sed 's/^/  /' >&2
    exit 1
fi

# Install just the binary
info "Installing to $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"
cp "$BINARY_PATH" "$INSTALL_DIR/dotnetup"

# Ensure the binary is executable
chmod +x "$INSTALL_DIR/dotnetup"

# Verify
if [ ! -x "$INSTALL_DIR/dotnetup" ]; then
    err "Installation failed: '$INSTALL_DIR/dotnetup' not found or not executable after copy."
    exit 1
fi

echo ""
ok "dotnetup installed successfully to $INSTALL_DIR"
echo ""

# Check if install dir is on PATH (resolve to absolute path, handle trailing slashes)
RESOLVED_INSTALL_DIR=$(cd "$INSTALL_DIR" 2>/dev/null && pwd -P || echo "$INSTALL_DIR")
on_path() {
    local dir
    while IFS= read -r -d ':' dir || [ -n "$dir" ]; do
        dir="${dir%/}"
        [ -z "$dir" ] && continue
        local resolved
        resolved=$(cd "$dir" 2>/dev/null && pwd -P || echo "$dir")
        if [ "$resolved" = "$RESOLVED_INSTALL_DIR" ]; then
            return 0
        fi
    done <<< "$PATH"
    return 1
}

if on_path; then
    ok "dotnetup is already on your PATH. Run 'dotnetup --help' to get started."
else
    warn "To add dotnetup to your PATH:"
    echo ""
    gray "  # Current session:"
    echo "  export PATH=\"$INSTALL_DIR:\$PATH\""
    echo ""
    gray "  # Permanently (add to your shell profile):"

    # Detect current shell for profile recommendation
    SHELL_NAME=$(basename "${SHELL:-/bin/bash}")
    case "$SHELL_NAME" in
        zsh)   PROFILE="~/.zshrc" ;;
        fish)  PROFILE="~/.config/fish/config.fish" ;;
        *)     PROFILE="~/.bashrc" ;;
    esac

    if [ "$SHELL_NAME" = "fish" ]; then
        echo "  fish_add_path \"$INSTALL_DIR\""
    else
        echo "  echo 'export PATH=\"$INSTALL_DIR:\$PATH\"' >> $PROFILE"
    fi
    echo ""
fi
