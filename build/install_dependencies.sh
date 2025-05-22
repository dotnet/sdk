#!/usr/bin/env bash
# In Helix test environments or containerized build environments, .NET SDK tests often fail due to missing system dependencies.
# This script automatically detects the operating system and installs the required dependencies to improve test stability and reduce issues caused by environment differences.

install_dependencies() {
  echo "Installing dependencies..."

  if [ -f /etc/os-release ]; then
    . /etc/os-release
    echo "Detected OS: $ID $VERSION_ID"

    case "$ID" in
      centos|rhel)
        sudo dnf install -y epel-release || { echo "Failed to install epel-release"; exit 1; }
        sudo dnf config-manager --set-enabled crb || { echo "Failed to enable CRB repository"; exit 1; }
        sudo dnf install -y zlib-devel libunwind || { echo "Failed to install dependencies"; exit 1; }
        ;;
      fedora)
        sudo dnf install -y clang || { echo "Failed to install clang"; exit 1; }
        ;;
      alpine)
        sudo apk add --no-cache clang || { echo "Failed to install clang"; exit 1; }
        ;;
      *)
        echo "Unsupported OS: $ID. Please install dependencies manually."
        exit 1
        ;;
    esac
  else
    echo "/etc/os-release not found. Cannot determine OS."
    exit 1
  fi

  echo "Dependencies installation complete."
}

install_dependencies
