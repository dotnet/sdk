#!/usr/bin/env bash

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do
  ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$ScriptRoot/$SOURCE"
done
ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

arguments=(--restore --build)
target_os=""
target_arch=""
skip_crossgen=true
skip_installers=true

while [[ $# > 0 ]]; do
  opt="$(echo "${1/#--/-}" | tr "[:upper:]" "[:lower:]")"
  case "$opt" in
    -os) target_os="$2"; shift ;;
    -arch|-a) target_arch="$2"; shift ;;
    -pack) skip_crossgen=false; skip_installers=false; arguments+=(-pack) ;;
    -test|-t) arguments+=(-test) ;;
    -configuration|-c) arguments+=(-configuration "$2"); shift ;;
    *) arguments+=("$1") ;;
  esac
  shift
done

if [ -n "$target_os" ]; then
  arguments+=("/p:TargetOS=$target_os")
fi
if [ -n "$target_arch" ]; then
  arguments+=("/p:TargetArchitecture=$target_arch")
fi

arguments+=("/p:SkipUsingCrossgen=$skip_crossgen")
arguments+=("/p:SkipBuildingInstallers=$skip_installers")
arguments+=(/tlp:summary)
arguments+=(/graph)

export DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT="true"
. "$ScriptRoot/common/build.sh" "${arguments[@]}"
