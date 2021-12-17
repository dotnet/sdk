#!/usr/bin/env bash
set -euo pipefail

SCRIPT_ROOT="$(cd -P "$( dirname "$0" )" && pwd)"
TARBALL_PREFIX=dotnet-sdk-
VERSION_PREFIX=6.0
# See https://github.com/dotnet/source-build/issues/579, this version
# needs to be compatible with the runtime produced from source-build
DEV_CERTS_VERSION_DEFAULT=6.0.0-preview.6.21355.2
__ROOT_REPO=$(sed 's/\r$//' "$SCRIPT_ROOT/artifacts/obj/rootrepo.txt") # remove CR if mounted repo on Windows drive
executingUserHome=${HOME:-}

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Use uname to determine what the CPU is.
cpuName=$(uname -p)
# Some Linux platforms report unknown for platform, but the arch for machine.
if [[ "$cpuName" == "unknown" ]]; then
  cpuName=$(uname -m)
fi

case $cpuName in
  aarch64)
    buildArch=arm64
    ;;
  amd64|x86_64)
    buildArch=x64
    ;;
  armv*l)
    buildArch=arm
    ;;
  i686)
    buildArch=x86
    ;;
  s390x)
    buildArch=s390x
    ;;
  *)
    echo "Unknown CPU $cpuName detected, treating it as x64"
    buildArch=x64
    ;;
esac

projectOutput=false
keepProjects=false
dotnetDir=""
configuration="Release"
excludeNonWebTests=false
excludeWebTests=false
excludeWebNoHttpsTests=false
excludeWebHttpsTests=false
excludeLocalTests=false
excludeOnlineTests=false
excludeOmniSharpTests=${excludeOmniSharpTests:-false}
devCertsVersion="$DEV_CERTS_VERSION_DEFAULT"
testingDir="$SCRIPT_ROOT/testing-smoke"
cliDir="$testingDir/builtCli"
logsDir="$testingDir/logs"
logFile="$logsDir/smoke-test.log"
omnisharpLogFile="$logsDir/omnisharp.log"
restoredPackagesDir="$testingDir/packages"
testingHome="$testingDir/home"
archiveRestoredPackages=false
archivedPackagesDir="$testingDir/smoke-test-packages"
smokeTestPrebuilts="$SCRIPT_ROOT/packages/smoke-test-packages"
runningOnline=false
runningHttps=false

function usage() {
    echo ""
    echo "usage:"
    echo "  --dotnetDir                    the directory from which to run dotnet"
    echo "  --configuration                the configuration being tested (default=Release)"
    echo "  --targetRid                    override the target rid to use when needed (e.g. for self-contained publish tests)"
    echo "  --projectOutput                echo dotnet's output to console"
    echo "  --keepProjects                 keep projects after tests are complete"
    echo "  --minimal                      run minimal set of tests - local sources only, no web"
    echo "  --excludeNonWebTests           don't run tests for non-web projects"
    echo "  --excludeWebTests              don't run tests for web projects"
    echo "  --excludeWebNoHttpsTests       don't run web project tests with --no-https"
    echo "  --excludeWebHttpsTests         don't run web project tests with https using dotnet-dev-certs"
    echo "  --excludeLocalTests            exclude tests that use local sources for nuget packages"
    echo "  --excludeOnlineTests           exclude test that use online sources for nuget packages"
    echo "  --excludeOmniSharpTests        don't run the OmniSharp tests"
    echo "  --devCertsVersion <version>    use dotnet-dev-certs <version> instead of default $DEV_CERTS_VERSION_DEFAULT"
    echo "  --prodConBlobFeedUrl <url>     override the prodcon blob feed specified in ProdConFeed.txt, removing it if empty"
    echo "  --archiveRestoredPackages      capture all restored packages to $archivedPackagesDir"
    echo "environment:"
    echo "  prodConBlobFeedUrl    override the prodcon blob feed specified in ProdConFeed.txt, removing it if empty"
    echo ""
}

while :; do
    if [ $# -le 0 ]; then
        break
    fi

    lowerI="$(echo "$1" | awk '{print tolower($0)}')"
    case $lowerI in
        '-?'|-h|--help)
            usage
            exit 0
            ;;
        --dotnetdir)
            shift
            dotnetDir="$1"
            ;;
        --configuration)
            shift
            configuration="$1"
            ;;
        --targetrid)
            shift
            targetRid="$1"
            ;;
        --projectoutput)
            projectOutput=true
            ;;
        --keepprojects)
            keepProjects=true
            ;;
        --minimal)
            excludeOnlineTests=true
            ;;
        --excludenonwebtests)
            excludeNonWebTests=true
            ;;
        --excludewebtests)
            excludeWebTests=true
            ;;
        --excludewebnohttpstests)
            excludeWebNoHttpsTests=true
            ;;
        --excludewebhttpstests)
            excludeWebHttpsTests=true
            ;;
        --excludelocaltests)
            excludeLocalTests=true
            ;;
        --excludeonlinetests)
            excludeOnlineTests=true
            ;;
        --excludeomnisharptests)
            excludeOmniSharpTests=true
            ;;
        --devcertsversion)
            shift
            devCertsVersion="$1"
            ;;
        --prodconblobfeedurl)
            shift
            prodConBlobFeedUrl="$1"
            ;;
        --archiverestoredpackages)
            archiveRestoredPackages=true
            ;;
        *)
            echo "Unrecognized argument '$1'"
            usage
            exit 1
            ;;
    esac

    shift
done

prodConBlobFeedUrl="${prodConBlobFeedUrl-}"

function doCommand() {
    lang=$1
    proj=$2
    shift; shift;

    echo "starting language $lang, type $proj" | tee -a smoke-test.log

    dotnetCmd=${dotnetDir}/dotnet

    # rename '#'' to 'Sharp' to workaround https://github.com/dotnet/roslyn/issues/51692
    projectDir="${lang//#/Sharp}_${proj}"
    mkdir "${projectDir}"
    cd "${projectDir}"

    newArgs="new $proj -lang $lang"

    while :; do
        if [ $# -le 0 ]; then
            break
        fi
        case "$1" in
            --new-arg)
                shift
                newArgs="$newArgs $1"
                ;;
            *)
                break
                ;;
        esac
        shift
    done

    while :; do
        if [ $# -le 0 ]; then
            break
        fi

        binlogOnlinePart="local"
        binlogHttpsPart="nohttps"
        if [ "$runningOnline" == "true" ]; then
            binlogOnlinePart="online"
        fi
        if [ "$runningHttps" == "true" ]; then
            binlogHttpsPart="https"
        fi

        binlogPrefix="$logsDir/${projectDir}_${binlogOnlinePart}_${binlogHttpsPart}_"
        binlog="${binlogPrefix}$1.binlog"
        echo "    running $1" | tee -a "$logFile"

        if [ "$1" == "new" ]; then
            if [ "$projectOutput" == "true" ]; then
                "${dotnetCmd}" $newArgs --no-restore | tee -a "$logFile"
            else
                "${dotnetCmd}" $newArgs --no-restore >> "$logFile" 2>&1
            fi
        elif [[ "$1" == "run" && "$proj" =~ ^(web|mvc|webapi|razor|blazorwasm|blazorserver)$ ]]; then
            # A separate log file that we will over-write all the time.
            exitLogFile="$testingDir/exitLogFile"
            echo > "$exitLogFile"
            # Run an application in the background and redirect its
            # stdout+stderr to a separate process (tee). The tee process
            # writes its input to 2 files:
            # - Either the normal log or stdout
            # - A log that's only used to find out when it's safe to kill
            #   the application.
            if [ "$projectOutput" == "true" ]; then
                "${dotnetCmd}" $1 2>&1 > >(tee -a "$exitLogFile") &
            else
                "${dotnetCmd}" $1 2>&1 > >(tee -a "$logFile" "$exitLogFile" >/dev/null) &
            fi
            webPid=$!
            killCommand="pkill -SIGTERM -P $webPid"
            echo "    waiting up to 30 seconds for web project with pid $webPid..."
            echo "    to clean up manually after an interactive cancellation, run: $killCommand"
            for seconds in $(seq 30); do
                if grep 'Application started. Press Ctrl+C to shut down.' "$exitLogFile"; then
                    echo "    app ready for shutdown after $seconds seconds"
                    break
                fi
                sleep 1
            done
            echo "    stopping $webPid" | tee -a "$logFile"
            $killCommand
            wait $!
            echo "    terminated with exit code $?" | tee -a "$logFile"
        elif [ "$1" == "multi-rid-publish" ]; then
            if [ "$lang" == "F#" ]; then
              runPublishScenarios() {
                  "${dotnetCmd}" publish --self-contained false /bl:"${binlogPrefix}publish-fx-dep.binlog"
                  "${dotnetCmd}" publish --self-contained true -r "$targetRid" /bl:"${binlogPrefix}publish-self-contained-${targetRid}.binlog"
                  "${dotnetCmd}" publish --self-contained true -r linux-x64 /bl:"${binlogPrefix}publish-self-contained-portable.binlog"
              }
            else
              runPublishScenarios() {
                  "${dotnetCmd}" publish --self-contained false /bl:"${binlogPrefix}publish-fx-dep.binlog"
                  "${dotnetCmd}" publish --self-contained true -r "$targetRid" /bl:"${binlogPrefix}publish-self-contained-${targetRid}.binlog"
                  "${dotnetCmd}" publish --self-contained true -r linux-x64 /bl:"${binlogPrefix}publish-self-contained-portable.binlog"
              }
            fi
            if [ "$projectOutput" == "true" ]; then
                runPublishScenarios | tee -a "$logFile"
            else
                runPublishScenarios >> "$logFile" 2>&1
            fi
        else
            if [ "$lang" == "F#" ]; then
              # F# tries to use a truncated version number unless we pass it this flag.  see https://github.com/dotnet/source-build/issues/2554
              if [ "$projectOutput" == "true" ]; then
                  "${dotnetCmd}" $1 /bl:"$binlog" | tee -a "$logFile"
              else
                  "${dotnetCmd}" $1 /bl:"$binlog" >> "$logFile" 2>&1
              fi
            else
              if [ "$projectOutput" == "true" ]; then
                  "${dotnetCmd}" $1 /bl:"$binlog" | tee -a "$logFile"
              else
                  "${dotnetCmd}" $1 /bl:"$binlog" >> "$logFile" 2>&1
              fi
            fi
        fi
        if [ $? -eq 0 ]; then
            echo "    $1 succeeded" >> "$logFile"
        else
            echo "    $1 failed with exit code $?" | tee -a "$logFile"
        fi

        shift
    done

    cd ..

    if [ "$keepProjects" == "false" ]; then
       rm -rf "${projectDir}"
    fi

    echo "finished language $lang, type $proj" | tee -a smoke-test.log
}

function setupDevCerts() {
    echo "Setting up dotnet-dev-certs $devCertsVersion to generate dev certificate" | tee -a "$logFile"
    (
        set -x
        "$dotnetDir/dotnet" tool install -g dotnet-dev-certs --version "$devCertsVersion" --add-source https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json
        export DOTNET_ROOT="$dotnetDir"
        "$testingHome/.dotnet/tools/dotnet-dev-certs" https
    ) >> "$logFile" 2>&1
}

function runAllTests() {
    # Run tests for each language and template
    if [ "$excludeNonWebTests" == "false" ]; then
        doCommand C# console new restore build run multi-rid-publish
        doCommand C# classlib new restore build multi-rid-publish
        doCommand C# xunit new restore test
        doCommand C# mstest new restore test

        doCommand VB console new restore build run multi-rid-publish
        doCommand VB classlib new restore build multi-rid-publish
        doCommand VB xunit new restore test
        doCommand VB mstest new restore test

        doCommand F# console new restore build run multi-rid-publish
        doCommand F# classlib new restore build multi-rid-publish
        doCommand F# xunit new restore test
        doCommand F# mstest new restore test
    fi

    if [ "$excludeWebTests" == "false" ]; then
        if [ "$excludeWebNoHttpsTests" == "false" ]; then
            runningHttps=false
            runWebTests --new-arg --no-https
        fi

        if [ "$excludeWebHttpsTests" == "false" ]; then
            runningHttps=true
            setupDevCerts
            runWebTests
        fi
    fi
}

function runWebTests() {
    doCommand C# web "$@" new restore build run multi-rid-publish
    doCommand C# mvc "$@" new restore build run multi-rid-publish
    doCommand C# webapi "$@" new restore build multi-rid-publish
    doCommand C# razor "$@" new restore build run multi-rid-publish
    doCommand C# blazorwasm "$@" new restore build run publish
    doCommand C# blazorserver "$@" new restore build run publish

    doCommand F# web "$@" new restore build run multi-rid-publish
    doCommand F# mvc "$@" new restore build run multi-rid-publish
    doCommand F# webapi "$@" new restore build run multi-rid-publish
}

function runXmlDocTests() {
    targetingPacksDir="$dotnetDir/packs/"
    echo "Looking for xml docs in targeting packs in $targetingPacksDir"

    netstandardIgnoreList=(
        Microsoft.Win32.Primitives.xml
        mscorlib.xml
        System.AppContext.xml
        System.Buffers.xml
        System.Collections.Concurrent.xml
        System.Collections.NonGeneric.xml
        System.Collections.Specialized.xml
        System.Collections.xml
        System.ComponentModel.Composition.xml
        System.ComponentModel.EventBasedAsync.xml
        System.ComponentModel.Primitives.xml
        System.ComponentModel.TypeConverter.xml
        System.ComponentModel.xml
        System.Console.xml
        System.Core.xml
        System.Data.Common.xml
        System.Data.xml
        System.Diagnostics.Contracts.xml
        System.Diagnostics.Debug.xml
        System.Diagnostics.FileVersionInfo.xml
        System.Diagnostics.Process.xml
        System.Diagnostics.StackTrace.xml
        System.Diagnostics.TextWriterTraceListener.xml
        System.Diagnostics.Tools.xml
        System.Diagnostics.TraceSource.xml
        System.Diagnostics.Tracing.xml
        System.Drawing.Primitives.xml
        System.Drawing.xml
        System.Dynamic.Runtime.xml
        System.Globalization.Calendars.xml
        System.Globalization.Extensions.xml
        System.Globalization.xml
        System.IO.Compression.FileSystem.xml
        System.IO.Compression.xml
        System.IO.Compression.ZipFile.xml
        System.IO.FileSystem.DriveInfo.xml
        System.IO.FileSystem.Primitives.xml
        System.IO.FileSystem.Watcher.xml
        System.IO.FileSystem.xml
        System.IO.IsolatedStorage.xml
        System.IO.MemoryMappedFiles.xml
        System.IO.Pipes.xml
        System.IO.UnmanagedMemoryStream.xml
        System.IO.xml
        System.Linq.Expressions.xml
        System.Linq.Parallel.xml
        System.Linq.Queryable.xml
        System.Linq.xml
        System.Memory.xml
        System.Net.Http.xml
        System.Net.NameResolution.xml
        System.Net.NetworkInformation.xml
        System.Net.Ping.xml
        System.Net.Primitives.xml
        System.Net.Requests.xml
        System.Net.Security.xml
        System.Net.Sockets.xml
        System.Net.WebHeaderCollection.xml
        System.Net.WebSockets.Client.xml
        System.Net.WebSockets.xml
        System.Net.xml
        System.Numerics.Vectors.xml
        System.Numerics.xml
        System.ObjectModel.xml
        System.Reflection.DispatchProxy.xml
        System.Reflection.Emit.ILGeneration.xml
        System.Reflection.Emit.Lightweight.xml
        System.Reflection.Emit.xml
        System.Reflection.Extensions.xml
        System.Reflection.Primitives.xml
        System.Reflection.xml
        System.Resources.Reader.xml
        System.Resources.ResourceManager.xml
        System.Resources.Writer.xml
        System.Runtime.CompilerServices.VisualC.xml
        System.Runtime.Extensions.xml
        System.Runtime.Handles.xml
        System.Runtime.InteropServices.RuntimeInformation.xml
        System.Runtime.InteropServices.xml
        System.Runtime.Numerics.xml
        System.Runtime.Serialization.Formatters.xml
        System.Runtime.Serialization.Json.xml
        System.Runtime.Serialization.Primitives.xml
        System.Runtime.Serialization.xml
        System.Runtime.Serialization.Xml.xml
        System.Runtime.xml
        System.Security.Claims.xml
        System.Security.Cryptography.Algorithms.xml
        System.Security.Cryptography.Csp.xml
        System.Security.Cryptography.Encoding.xml
        System.Security.Cryptography.Primitives.xml
        System.Security.Cryptography.X509Certificates.xml
        System.Security.Principal.xml
        System.Security.SecureString.xml
        System.ServiceModel.Web.xml
        System.Text.Encoding.Extensions.xml
        System.Text.Encoding.xml
        System.Text.RegularExpressions.xml
        System.Threading.Overlapped.xml
        System.Threading.Tasks.Extensions.xml
        System.Threading.Tasks.Parallel.xml
        System.Threading.Tasks.xml
        System.Threading.ThreadPool.xml
        System.Threading.Thread.xml
        System.Threading.Timer.xml
        System.Threading.xml
        System.Transactions.xml
        System.ValueTuple.xml
        System.Web.xml
        System.Windows.xml
        System.xml
        System.Xml.Linq.xml
        System.Xml.ReaderWriter.xml
        System.Xml.Serialization.xml
        System.Xml.XDocument.xml
        System.Xml.xml
        System.Xml.XmlDocument.xml
        System.Xml.XmlSerializer.xml
        System.Xml.XPath.XDocument.xml
        System.Xml.XPath.xml
    )

    netcoreappIgnoreList=(
        mscorlib.xml
        Microsoft.VisualBasic.xml
        System.AppContext.xml
        System.Buffers.xml
        System.ComponentModel.DataAnnotations.xml
        System.Configuration.xml
        System.Core.xml
        System.Data.DataSetExtensions.xml
        System.Data.xml
        System.Diagnostics.Debug.xml
        System.Diagnostics.Tools.xml
        System.Drawing.xml
        System.Dynamic.Runtime.xml
        System.Globalization.Calendars.xml
        System.Globalization.Extensions.xml
        System.Globalization.xml
        System.IO.Compression.Brotli.xml
        System.IO.Compression.FileSystem.xml
        System.IO.FileSystem.xml
        System.IO.FileSystem.Primitives.xml
        System.IO.UnmanagedMemoryStream.xml
        System.IO.xml
        System.Net.xml
        System.Numerics.xml
        System.Numerics.Vectors.xml
        System.Reflection.Extensions.xml
        System.Reflection.xml
        System.Resources.Reader.xml
        System.Resources.ResourceManager.xml
        System.Runtime.Extensions.xml
        System.Runtime.Handles.xml
        System.Runtime.Serialization.xml
        System.Security.Principal.xml
        System.Security.SecureString.xml
        System.Security.xml
        System.ServiceModel.Web.xml
        System.ServiceProcess.xml
        System.Text.Encoding.xml
        System.Text.Json.SourceGeneration.resources.xml
        System.Text.Json.SourceGeneration.xml
        System.Threading.Tasks.Extensions.xml
        System.Threading.Tasks.xml
        System.Threading.Timer.xml
        System.Transactions.xml
        System.ValueTuple.xml
        System.Web.xml
        System.Windows.xml
        System.xml
        System.Xml.Linq.xml
        System.Xml.Serialization.xml
        System.Xml.xml
        System.Xml.XmlDocument.xml
        WindowsBase.xml
    )

    aspnetcoreappIgnoreList=(
        Microsoft.AspNetCore.App.Analyzers.xml
        Microsoft.AspNetCore.App.CodeFixes.xml
        Microsoft.Extensions.Logging.Generators.resources.xml
        Microsoft.Extensions.Logging.Generators.xml
    )

    error=0
    while IFS= read -r -d '' dllFile; do
        xmlDocFile=${dllFile%.*}.xml
        skip=0
        if [[ "$xmlDocFile" == *"/packs/Microsoft.NETCore.App.Ref"* ]]; then
            xmlFileBasename=$(basename "$xmlDocFile")
            for ignoreItem in "${netcoreappIgnoreList[@]}"; do
                if [[ "$ignoreItem" == "$xmlFileBasename" ]]; then
                    skip=1;
                    break
                fi
            done
        fi
        if [[ "$xmlDocFile" == *"/packs/NETStandard.Library.Ref"* ]]; then
            xmlFileBasename=$(basename "$xmlDocFile")
            for ignoreItem in "${netstandardIgnoreList[@]}"; do
                if [[ "$ignoreItem" == "$xmlFileBasename" ]]; then
                    skip=1;
                    break
                fi
            done
        fi
        if [[ "$xmlDocFile" == *"/packs/Microsoft.AspNetCore.App.Ref"* ]]; then
            xmlFileBasename=$(basename "$xmlDocFile")
            for ignoreItem in "${aspnetcoreappIgnoreList[@]}"; do
                if [[ "$ignoreItem" == "$xmlFileBasename" ]]; then
                    skip=1;
                    break
                fi
            done
        fi
        if [[ $skip == 0 ]] && [[ ! -f "$xmlDocFile" ]]; then
            error=1
            echo "error: missing $xmlDocFile"
        fi

    if [[ $skip == 1 ]] && [[ -f "$xmlDocFile" ]]; then
        error=1
        echo "error: Ignored xml doc was found: $xmlDocFile"
    fi

    done < <(find "$targetingPacksDir" -name '*.dll' -print0)

    if [[ $error != 0 ]]; then
        echo "error: Missing or unexpected xml documents"
        exit 1
    else
        echo "All expected xml docs are present"
    fi
}

function runOmniSharpTests() {
    dotnetCmd=${dotnetDir}/dotnet

    rm -rf workdir
    mkdir workdir
    pushd workdir

    curl -sSLO "https://github.com/OmniSharp/omnisharp-roslyn/releases/latest/download/omnisharp-linux-x64.tar.gz"

    mkdir omnisharp
    pushd omnisharp
    tar xf "../omnisharp-linux-x64.tar.gz"
    popd

    # 'blazorwasm' requires prereqs (non-source-built packages) - re-enable with https://github.com/dotnet/source-build/issues/2550
    for project in blazorserver classlib console mstest mvc nunit web webapp webapi worker xunit ; do

        mkdir hello-$project
        pushd hello-$project

        "${dotnetCmd}" new $project
        popd

        ./omnisharp/run -s "$(readlink -f hello-$project)" > "$omnisharpLogFile" &

        sleep 5

        pkill -P $$

        # Omnisharp spawns off a number of processes. They all include the
        # current directory as a process argument, so use that to identify and
        # kill them.
        pgrep -f "$(pwd)"

        kill "$(pgrep -f "$(pwd)")"

        cat "$omnisharpLogFile"

        if grep ERROR "$omnisharpLogFile"; then
            echo "test failed"
            exit 1
        else
            echo "OK"
        fi

    done

    popd
}

function resetCaches() {
    rm -rf "$testingHome"
    mkdir "$testingHome"

    HOME="$testingHome"

    # clean restore path
    rm -rf "$restoredPackagesDir"

    # Copy NuGet plugins if running user has HOME and we have auth. In particular, the auth plugin.
    if [ "${internalPackageFeedPat:-}" ] && [ "${executingUserHome:-}" ]; then
        cp -r "$executingUserHome/.nuget/" "$HOME/.nuget/" || :
    fi
}

function setupProdConFeed() {
    if [ "$prodConBlobFeedUrl" ]; then
        sed -i.bakProdCon "s|PRODUCT_CONTRUCTION_PACKAGES|$prodConBlobFeedUrl|g" "$testingDir/NuGet.Config"
    else
        sed -i.bakProdCon "/PRODUCT_CONTRUCTION_PACKAGES/d" "$testingDir/NuGet.Config"
    fi
}

function setupSmokeTestFeed() {
    # Setup smoke-test-packages if they exist
    if [ -e "$smokeTestPrebuilts" ]; then
        sed -i.bakSmokeTestFeed "s|SMOKE_TEST_PACKAGE_FEED|$smokeTestPrebuilts|g" "$testingDir/NuGet.Config"
    else
        sed -i.bakSmokeTestFeed "/SMOKE_TEST_PACKAGE_FEED/d" "$testingDir/NuGet.Config"
    fi
}

function copyRestoredPackages() {
    if [ "$archiveRestoredPackages" == "true" ]; then
        mkdir -p "$archivedPackagesDir"
        cp -rf "$restoredPackagesDir"/* "$archivedPackagesDir"
    fi
}

echo "RID to test: ${targetRid?not specified. Use ./build.sh --run-smoke-test to detect RID, or specify manually.}"

if [ "$__ROOT_REPO" != "known-good" ]; then
    echo "Skipping smoke-tests since cli was not built";
    exit
fi

# Clean up and create directory
if [ -e "$testingDir"  ]; then
    read -p "testing-smoke directory exists, remove it? [Y]es / [n]o" -n 1 -r
    echo
    if [[ $REPLY == "" || $REPLY == " " || $REPLY =~ ^[Yy]$ ]]; then
        rm -rf "$testingDir"
    fi
fi

mkdir -p "$testingDir"
mkdir -p "$logsDir"
cd "$testingDir"

# Create blank Directory.Build files to avoid traversing to source-build infra.
echo "<Project />" | tee Directory.Build.props > Directory.Build.targets

# Unzip dotnet if the dotnetDir is not specified
if [ "$dotnetDir" == "" ]; then
    OUTPUT_DIR="$SCRIPT_ROOT/artifacts/$buildArch/$configuration/"
    DOTNET_TARBALL="$(ls "${OUTPUT_DIR}${TARBALL_PREFIX}${VERSION_PREFIX}"*)"

    mkdir -p "$cliDir"
    tar xzf "$DOTNET_TARBALL" -C "$cliDir"
    dotnetDir="$cliDir"
else
    if ! [[ "$dotnetDir" = /* ]]; then
       dotnetDir="$SCRIPT_ROOT/$dotnetDir"
    fi
fi

echo SDK under test is:
"$dotnetDir/dotnet" --info

# setup restore path
export NUGET_PACKAGES="$restoredPackagesDir"
SOURCE_BUILT_PKGS_PATH="$SCRIPT_ROOT/artifacts/obj/$buildArch/$configuration/blob-feed/packages/"
export DOTNET_ROOT="$dotnetDir"
export PATH="$dotnetDir:$PATH"

# Run all tests, local restore sources first, online restore sources second
if [ "$excludeLocalTests" == "false" ]; then
    resetCaches
    runningOnline=false
    # Setup NuGet.Config with local restore source
    if [ -e "$SCRIPT_ROOT/smoke-testNuGet.Config" ]; then
        cp "$SCRIPT_ROOT/smoke-testNuGet.Config" "$testingDir/NuGet.Config"
        sed -i.bak "s|SOURCE_BUILT_PACKAGES|$SOURCE_BUILT_PKGS_PATH|g" "$testingDir/NuGet.Config"
        setupProdConFeed
        setupSmokeTestFeed
        echo "$testingDir/NuGet.Config Contents:"
        cat "$testingDir/NuGet.Config"
    fi
    echo "RUN ALL TESTS - LOCAL RESTORE SOURCE"
    runAllTests
    copyRestoredPackages
    echo "LOCAL RESTORE SOURCE - ALL TESTS PASSED!"
fi

if [ "$excludeOnlineTests" == "false" ]; then
    resetCaches
    runningOnline=true
    # Setup NuGet.Config to use online restore sources
    if [ -e "$SCRIPT_ROOT/smoke-testNuGet.Config" ]; then
        cp "$SCRIPT_ROOT/smoke-testNuGet.Config" "$testingDir/NuGet.Config"
        sed -i.bak "/SOURCE_BUILT_PACKAGES/d" "$testingDir/NuGet.Config"
        setupProdConFeed
        setupSmokeTestFeed
        echo "$testingDir/NuGet.Config Contents:"
        cat "$testingDir/NuGet.Config"
    fi
    echo "RUN ALL TESTS - ONLINE RESTORE SOURCE"
    runAllTests
    copyRestoredPackages
    echo "ONLINE RESTORE SOURCE - ALL TESTS PASSED!"
fi

runXmlDocTests

if [ "$excludeOmniSharpTests" == "false" ]; then
    runOmniSharpTests
fi

echo "ALL TESTS PASSED!"
