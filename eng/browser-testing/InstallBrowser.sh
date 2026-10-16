#!/usr/bin/env bash
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

set -euo pipefail

if [[ "${TF_BUILD:-}" != "True" && "${TF_BUILD:-}" != "true" ]]; then
  echo "Browser provisioning is restricted to disposable Azure Pipelines workers." >&2
  exit 1
fi

df -h
if command -v google-chrome >/dev/null; then
  google-chrome --version
  exit 0
fi

echo "Installing the missing Google Chrome prerequisite from its signed APT repository."
if ! command -v gpg >/dev/null; then
  sudo -n apt-get update -o APT::Update::Error-Mode=any
  sudo -n apt-get install --yes --no-install-recommends gnupg
fi
temp="$(mktemp -d "${AGENT_TEMPDIRECTORY:?}/browser-setup.XXXXXXXX")"
trap 'rm -rf -- "$temp"' EXIT

# Primary signing authority published at https://www.google.com/linuxrepositories/.
fingerprint="EB4C1BFD4F042F6DDDCCEC917721F63BD38B4796"
curl --fail --silent --show-error --location \
  https://dl.google.com/linux/linux_signing_key.pub --output "$temp/key.pub"
gpg --batch --no-autostart --homedir "$temp" --import "$temp/key.pub"
gpg --batch --no-autostart --homedir "$temp" --with-colons --fingerprint "$fingerprint" |
  awk -F: '$1 == "fpr" { print $10 }' | grep --fixed-strings --line-regexp "$fingerprint"
gpg --batch --no-autostart --homedir "$temp" --output "$temp/google-chrome.gpg" --export "$fingerprint"

sudo -n install -m 0644 "$temp/google-chrome.gpg" /usr/share/keyrings/dotnet-browser-google.gpg
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/dotnet-browser-google.gpg] https://dl.google.com/linux/chrome/deb/ stable main" > "$temp/google-chrome.list"
sudo -n install -m 0644 "$temp/google-chrome.list" /etc/apt/sources.list.d/google-chrome.list
sudo -n apt-get update -o APT::Update::Error-Mode=any
sudo -n apt-get install --yes --no-install-recommends google-chrome-stable
google-chrome --version
