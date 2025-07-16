#!/usr/bin/env bash

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$ScriptRoot/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

if [[ "$@" != *"-pack"* ]]; then
  # skip crossgen for inner-loop builds to save a ton of time
  skipFlags="/p:SkipUsingCrossgen=true /p:SkipBuildingInstallers=true"
fi

export DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT="true"
. "$ScriptRoot/eng/common/build.sh" --build --restore $skipFlags "$@"
