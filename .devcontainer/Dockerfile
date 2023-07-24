# See here for image contents: https://github.com/microsoft/vscode-dev-containers/tree/v0.215.1/containers/dotnet/.devcontainer/base.Dockerfile

# [Choice] .NET version: 6.0, 5.0, 3.1, 6.0-bullseye, 5.0-bullseye, 3.1-bullseye, 6.0-focal, 5.0-focal, 3.1-focal
ARG VARIANT=6.0-bullseye
FROM mcr.microsoft.com/vscode/devcontainers/dotnet

# [Optional] PoshGit
ARG INSTALL_POSHGIT="true"

# Configure apt and install packages
RUN export DEBIAN_FRONTEND=noninteractive \
    # [Optional] Install the Posh-Git
    && if [ "$INSTALL_POSHGIT" = "true" ]; then \
        pwsh -command "PowerShellGet\Install-Module posh-git -Scope CurrentUser -AllowPrerelease -Force" \
        && pwsh -command "Add-PoshGitToProfile -AllHosts"; \
    fi
