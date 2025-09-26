#!/usr/bin/env bash

# make NuGet network operations more robust
export NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true
export NUGET_EXPERIMENTAL_MAX_NETWORK_TRY_COUNT=6
export NUGET_EXPERIMENTAL_NETWORK_RETRY_DELAY_MILLISECONDS=1000

export MicrosoftNETBuildExtensionsTargets=$HELIX_CORRELATION_PAYLOAD/ex/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets
export DOTNET_ROOT=$HELIX_CORRELATION_PAYLOAD/d
export PATH=$DOTNET_ROOT:$PATH

export TestExecutionDirectory=$(realpath "$(mktemp -d "${TMPDIR:-/tmp}"/dotnetSdkTests.XXXXXXXX)")
export DOTNET_CLI_HOME=$TestExecutionDirectory/.dotnet
cp -a $HELIX_CORRELATION_PAYLOAD/t/TestExecutionDirectoryFiles/. $TestExecutionDirectory/

export DOTNET_SDK_TEST_EXECUTION_DIRECTORY=$TestExecutionDirectory
export DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=$HELIX_CORRELATION_PAYLOAD/r
export DOTNET_SDK_TEST_ASSETS_DIRECTORY=$TestExecutionDirectory/TestAssets

# Ensure Docker uses the containerd snapshotter so multi-arch tests can run.
if [[ "$(uname -s)" == "Linux" ]] && command -v docker >/dev/null 2>&1; then
	if ! docker info --format '{{json .DriverStatus}}' 2>/dev/null | grep -q 'io.containerd.snapshotter'; then
		echo "Enabling Docker containerd snapshotter for test run"

		if command -v sudo >/dev/null 2>&1; then
			sudo mkdir -p /etc/docker
			sudo python3 - <<'PY'
import json
import os

path = "/etc/docker/daemon.json"
data = {}

if os.path.exists(path):
		try:
				with open(path, "r", encoding="utf-8") as f:
						content = f.read().strip()
				if content:
						data = json.loads(content)
		except Exception:
				pass

if not isinstance(data.get("features"), dict):
		data["features"] = {}

changed = data["features"].get("containerd-snapshotter") is not True

if changed:
		data["features"]["containerd-snapshotter"] = True
		with open(path, "w", encoding="utf-8") as f:
				json.dump(data, f, indent=2)
				f.write("\n")
PY

			if sudo systemctl restart docker; then
				echo "Docker daemon restarted after enabling containerd snapshotter."
			else
				echo "systemctl restart docker failed; attempting service restart."
				sudo service docker restart || true
			fi

			for attempt in {1..10}; do
				if docker info >/dev/null 2>&1; then
					break
				fi
				sleep 3
			done

			if ! docker info --format '{{json .DriverStatus}}' 2>/dev/null | grep -q 'io.containerd.snapshotter'; then
				echo "Warning: Docker containerd snapshotter still not reported as enabled."
			fi
		else
			echo "Warning: sudo not available; cannot enable Docker containerd snapshotter."
		fi
	fi
fi

# call dotnet new so the first run message doesn't interfere with the first test
dotnet new --debug:ephemeral-hive

# We downloaded a special zip of files to the .nuget folder so add that as a source
dotnet nuget list source --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget add source $DOTNET_ROOT/.nuget --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget add source $TestExecutionDirectory/Testpackages --configfile $TestExecutionDirectory/NuGet.config
#Remove feeds not needed for tests
dotnet nuget remove source dotnet6-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet6-internal-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet7-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet7-internal-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source richnav --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source vs-impl --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-libraries-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-tools-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-libraries --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-eng --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget list source --configfile $TestExecutionDirectory/NuGet.config
