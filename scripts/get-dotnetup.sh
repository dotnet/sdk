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

# Returns the ICU package name and install command for the current Linux distro.
# Package names and install commands are derived from:
# https://github.com/dotnet/core/blob/main/release-notes/8.0/os-packages.json
# Sets DISTRO_ICU_PKG and DISTRO_ICU_INSTALL_CMD (empty = unknown distro).
detect_icu_install_info() {
    DISTRO_ICU_PKG=""
    DISTRO_ICU_INSTALL_CMD=""
    local distro_id="" distro_version="" distro_id_like=""
    if [ -f /etc/os-release ]; then
        distro_id=$(grep -E '^ID=' /etc/os-release | sed 's/^ID=//;s/"//g')
        distro_version=$(grep -E '^VERSION_ID=' /etc/os-release | sed 's/^VERSION_ID=//;s/"//g')
        distro_id_like=$(grep -E '^ID_LIKE=' /etc/os-release | sed 's/^ID_LIKE=//;s/"//g')
    fi

    case "$distro_id" in
        ubuntu)
            case "$distro_version" in
                26.*) DISTRO_ICU_PKG="libicu78" ;;
                25.*) DISTRO_ICU_PKG="libicu76" ;;
                24.*) DISTRO_ICU_PKG="libicu74" ;;
                22.*) DISTRO_ICU_PKG="libicu70" ;;
                20.*) DISTRO_ICU_PKG="libicu66" ;;
                *)    DISTRO_ICU_PKG="libicu-dev" ;;
            esac
            DISTRO_ICU_INSTALL_CMD="sudo apt-get update && sudo apt-get install -y ${DISTRO_ICU_PKG}"
            ;;
        debian)
            case "$distro_version" in
                13|sid) DISTRO_ICU_PKG="libicu76" ;;
                12)     DISTRO_ICU_PKG="libicu72" ;;
                11)     DISTRO_ICU_PKG="libicu67" ;;
                *)      DISTRO_ICU_PKG="libicu-dev" ;;
            esac
            DISTRO_ICU_INSTALL_CMD="sudo apt-get update && sudo apt-get install -y ${DISTRO_ICU_PKG}"
            ;;
        fedora|rhel|centos|centos-stream|almalinux|rocky|ol)
            DISTRO_ICU_PKG="libicu"
            DISTRO_ICU_INSTALL_CMD="sudo dnf install -y libicu"
            ;;
        alpine)
            DISTRO_ICU_PKG="icu-libs"
            DISTRO_ICU_INSTALL_CMD="sudo apk add icu-libs"
            ;;
        opensuse-leap|opensuse-tumbleweed|sles)
            DISTRO_ICU_PKG="libicu"
            DISTRO_ICU_INSTALL_CMD="sudo zypper install -y libicu"
            ;;
        mariner|azurelinux)
            DISTRO_ICU_PKG="icu"
            DISTRO_ICU_INSTALL_CMD="sudo tdnf install -y icu"
            ;;
        *)
            case " ${distro_id_like} " in
                *" ubuntu "*|*" debian "*)
                    DISTRO_ICU_PKG="libicu-dev"
                    DISTRO_ICU_INSTALL_CMD="sudo apt-get update && sudo apt-get install -y libicu-dev"
                    ;;
                *" rhel "*|*" fedora "*|*" centos "*)
                    DISTRO_ICU_PKG="libicu"
                    DISTRO_ICU_INSTALL_CMD="sudo dnf install -y libicu"
                    ;;
                *" suse "*)
                    DISTRO_ICU_PKG="libicu"
                    DISTRO_ICU_INSTALL_CMD="sudo zypper install -y libicu"
                    ;;
            esac
            ;;
    esac
}

# Checks whether the ICU package is installed on the current distro.
# Uses the native package-manager query for known distros; falls back to
# ldconfig and a filesystem scan for unknown distributions.
# Package names derived from:
# https://github.com/dotnet/core/blob/main/release-notes/8.0/os-packages.json
check_icu_present() {
    local distro_id="" distro_id_like=""
    if [ -f /etc/os-release ]; then
        distro_id=$(grep -E '^ID=' /etc/os-release | sed 's/^ID=//;s/"//g')
        distro_id_like=$(grep -E '^ID_LIKE=' /etc/os-release | sed 's/^ID_LIKE=//;s/"//g')
    fi

    case "$distro_id" in
        ubuntu|debian)
            dpkg -l 'libicu[0-9]*' 2>/dev/null | grep -q '^ii' && return 0
            return 1
            ;;
        fedora|rhel|centos|centos-stream|almalinux|rocky|ol)
            rpm -q libicu &>/dev/null && return 0
            return 1
            ;;
        alpine)
            apk info -e icu-libs &>/dev/null && return 0
            return 1
            ;;
        opensuse-leap|opensuse-tumbleweed|sles)
            rpm -q libicu &>/dev/null && return 0
            return 1
            ;;
        mariner|azurelinux)
            rpm -q icu &>/dev/null && return 0
            return 1
            ;;
        *)
            # Try ID_LIKE for derivative distros
            case " ${distro_id_like} " in
                *" ubuntu "*|*" debian "*)
                    dpkg -l 'libicu[0-9]*' 2>/dev/null | grep -q '^ii' && return 0
                    return 1
                    ;;
                *" rhel "*|*" fedora "*|*" centos "*)
                    rpm -q libicu &>/dev/null && return 0
                    return 1
                    ;;
                *" suse "*)
                    rpm -q libicu &>/dev/null && return 0
                    return 1
                    ;;
            esac
            # Fallback: ldconfig cache
            if command -v ldconfig &>/dev/null && ldconfig -p 2>/dev/null | grep -q "libicuuc\.so"; then
                return 0
            fi
            # Fallback: filesystem search
            for f in /usr/lib/libicuuc.so.* /usr/lib64/libicuuc.so.* /usr/local/lib/libicuuc.so.* /usr/local/lib64/libicuuc.so.* /lib/libicuuc.so.* /lib64/libicuuc.so.*; do
                [ -e "$f" ] && return 0
            done
            return 1
            ;;
    esac
}

# --- Main ---

RID=$(detect_rid)
info "Detected runtime: $RID"

# --- Check ICU libraries (Linux only) ---
# The .NET runtime requires ICU for globalization support. Check that the libraries
# are present before downloading dotnetup to give a clear, actionable error message.
if [[ "$RID" == linux* ]]; then
    detect_icu_install_info
    if ! check_icu_present; then
        err "ICU libraries are required to run dotnetup but were not found on this system."
        err ""
        err "Please install ICU using your package manager and re-run this script:"
        if [ -n "$DISTRO_ICU_INSTALL_CMD" ]; then
            err "  $DISTRO_ICU_INSTALL_CMD"
        else
            err "  Debian/Ubuntu:  sudo apt-get update && sudo apt-get install -y libicu-dev"
            err "  Fedora/RHEL:    sudo dnf install -y libicu"
            err "  Alpine Linux:   sudo apk add icu-libs"
        fi
        err ""
        err "For more information, see: https://aka.ms/dotnet-missing-libicu"
        exit 1
    fi
fi

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
