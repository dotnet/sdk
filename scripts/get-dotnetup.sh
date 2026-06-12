#!/usr/bin/env bash
# get-dotnetup.sh
#
# Downloads the latest dotnetup daily build and installs it locally.
#
# Downloads dotnetup from the public aka.ms shortlinks (e.g.
# https://aka.ms/dotnet/dotnetup/daily/dotnetup-linux-x64), verifies the
# SHA-512 checksum, and installs the binary to a local directory.
#
# Usage:
#   ./get-dotnetup.sh
#   ./get-dotnetup.sh --install-dir /opt/dotnetup
#   ./get-dotnetup.sh --runtime-id linux-musl-x64
#   ./get-dotnetup.sh --quality daily

set -euo pipefail

# --- Defaults ---
INSTALL_DIR="$HOME/.dotnetup"
QUALITY="daily"
RUNTIME_ID=""

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
        --quality)      QUALITY="$2"; shift 2 ;;
        --runtime-id)   RUNTIME_ID="$2"; shift 2 ;;
        --help|-h)
            cat <<EOF
Usage: $(basename "$0") [OPTIONS]

Downloads the latest dotnetup daily build from aka.ms and installs it locally.

Options:
  --install-dir DIR     Installation directory (default: ~/.dotnetup)
  --quality QUALITY     Build quality (default: daily)
  --runtime-id RID      Override OS/architecture detection (e.g. linux-musl-x64, osx-arm64)
  --help, -h            Show this help message

Examples:
  ./get-dotnetup.sh
  ./get-dotnetup.sh --runtime-id linux-musl-x64
  ./get-dotnetup.sh --install-dir /opt/dotnetup
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

BASE_URL="https://aka.ms/dotnet/dotnetup/${QUALITY}"

# --- Check prerequisites ---
if command -v curl &>/dev/null; then
    DOWNLOADER="curl"
elif command -v wget &>/dev/null; then
    DOWNLOADER="wget"
else
    err "Neither 'curl' nor 'wget' was found on PATH. One of them is required to download dotnetup."
    exit 1
fi

download() {
    local url="$1" out="$2"
    if [ "$DOWNLOADER" = "curl" ]; then
        curl --fail --silent --show-error --location --retry 3 --output "$out" "$url"
    else
        wget --quiet --tries=3 --output-document="$out" "$url"
    fi
}

# --- Detect runtime ID ---
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

detect_rid() {
    if [ -n "$RUNTIME_ID" ]; then
        echo "$RUNTIME_ID"
        return
    fi

    local os arch

    # Detect OS
    case "$(uname -s)" in
        Linux)
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

    # Detect architecture. On macOS, `uname -m` reports x86_64 when the shell is
    # running under Rosetta 2, so prefer the native hardware capability signal.
    local machine_arch
    if [ "$os" = "osx" ] && [ "$(sysctl -n hw.optional.arm64 2>/dev/null || echo 0)" = "1" ]; then
        machine_arch="arm64"
    else
        machine_arch="$(uname -m)"
    fi

    case "$machine_arch" in
        x86_64|amd64)    arch="x64" ;;
        aarch64|arm64)   arch="arm64" ;;
        *)
            err "Unsupported architecture: $machine_arch. Use --runtime-id to specify a RID manually."
            exit 1
            ;;
    esac

    echo "${os}-${arch}"
}

# --- Main ---

RID=$(detect_rid)
info "Detected runtime: $RID"

FILE_NAME="dotnetup-${RID}"
DOWNLOAD_URL="${BASE_URL}/${FILE_NAME}"
CHECKSUM_URL="${DOWNLOAD_URL}.sha512"

INSTALLED_BINARY="${INSTALL_DIR}/dotnetup"

TEMP_DIR=$(mktemp -d)

cleanup() {
    rm -rf "$TEMP_DIR"
}
trap cleanup EXIT

TEMP_BINARY="${TEMP_DIR}/${FILE_NAME}"

info "Downloading ${DOWNLOAD_URL}"
if ! download "$DOWNLOAD_URL" "$TEMP_BINARY"; then
    err "Failed to download dotnetup from $DOWNLOAD_URL"
    err "Available RIDs: win-x64, win-arm64, linux-x64, linux-arm64, linux-musl-x64, linux-musl-arm64, osx-x64, osx-arm64"
    err "Use --runtime-id to specify the correct RID, or --quality to select a different build quality."
    exit 1
fi

info "Verifying SHA-512 checksum..."
TEMP_CHECKSUM="${TEMP_DIR}/${FILE_NAME}.sha512"
if ! download "$CHECKSUM_URL" "$TEMP_CHECKSUM"; then
    err "Failed to download checksum from $CHECKSUM_URL"
    exit 1
fi

EXPECTED=$(awk '{print tolower($1)}' "$TEMP_CHECKSUM")
if command -v sha512sum &>/dev/null; then
    ACTUAL=$(sha512sum "$TEMP_BINARY" | awk '{print tolower($1)}')
elif command -v shasum &>/dev/null; then
    ACTUAL=$(shasum -a 512 "$TEMP_BINARY" | awk '{print tolower($1)}')
else
    err "Neither 'sha512sum' nor 'shasum' was found on PATH."
    exit 1
fi

if [ "$EXPECTED" != "$ACTUAL" ]; then
    err "Checksum mismatch."
    err "  Expected: $EXPECTED"
    err "  Actual:   $ACTUAL"
    exit 1
fi
ok "Checksum verified."

# Install the binary (renamed to plain 'dotnetup')
info "Installing to $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"
cp "$TEMP_BINARY" "$INSTALL_DIR/dotnetup"
chmod +x "$INSTALL_DIR/dotnetup"

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
