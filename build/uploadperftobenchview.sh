#!/bin/bash

# The intent of this script is upload produced performance results to BenchView in a CI context.
#    There is no support for running this script in a dev environment.

if [ -z "$perfWorkingDirectory" ]; then
    echo EnvVar perfWorkingDirectory should be set; exiting...
    exit 1
fi
if [ -z "$configuration" ]; then
    echo EnvVar configuration should be set; exiting...
    exit 1
fi
if [ -z "$architecture" ]; then
    echo EnvVar architecture should be set; exiting...
    exit 1
fi
if [ -z "$OS" ]; then
    echo EnvVar OS should be set; exiting...
    exit 1
fi
if [ "$runType" = "private" ]; then
    if [ -z "$BenchviewCommitName" ]; then
        echo EnvVar BenchviewCommitName should be set; exiting...
        exit 1
    fi
else
    if [ "$runType" = "rolling" ]; then
        if [ -z "$GIT_COMMIT" ]; then
            echo EnvVar GIT_COMMIT should be set; exiting...
            exit 1
        fi
    else
        echo EnvVar runType should be set; exiting...
        exit 1
    fi
fi
if [ -z "$GIT_BRANCH" ]; then
    echo EnvVar GIT_BRANCH should be set; exiting...
    exit 1
fi
if [ ! -d "$perfWorkingDirectory" ]; then
    echo "$perfWorkingDirectory" does not exist; exiting...
    exit 1
fi

# Do this here to remove the origin but at the front of the branch name as this is a problem for BenchView
if [[ "$GIT_BRANCH" == "origin/"* ]]
then
    GIT_BRANCH_WITHOUT_ORIGIN=${GIT_BRANCH:7}
else
    GIT_BRANCH_WITHOUT_ORIGIN=$GIT_BRANCH
fi

benchViewName="SDK perf $OS $architecture $configuration $runType $GIT_BRANCH_WITHOUT_ORIGIN"
if [[ "$runType" == "private" ]]
then
    benchViewName="$benchViewName $BenchviewCommitName"
fi
if [[ "$runType" == "rolling" ]]
then
    benchViewName="$benchViewName $GIT_COMMIT"
fi
echo BenchViewName: "$benchViewName"

echo Creating: "$perfWorkingDirectory/submission.json"
"$HELIX_WORKITEM_ROOT/.dotnet/dotnet" build $HELIX_WORKITEM_ROOT/src/Tests/PerformanceTestsResultGenerator/PerformanceTestsResultGenerator.csproj --configuration $configuration
"$HELIX_WORKITEM_ROOT/.dotnet/dotnet" run --no-build --project $HELIX_WORKITEM_ROOT/src/Tests/PerformanceTestsResultGenerator/PerformanceTestsResultGenerator.csproj -- --output "$perfWorkingDirectory/submission.json"

echo Uploading: "$perfWorkingDirectory/submission.json"

# TODO wul upload with Azdo

exit 0
