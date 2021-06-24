#!/usr/bin/env bash
set -euo pipefail
IFS=$'\n\t'

DOCKER_FILE=""
DOCKER_IMAGE=""
SCRIPT_ROOT="$(cd -P "$( dirname "$0" )" && pwd)"
REPO_ROOT="$(cd -P "$SCRIPT_ROOT/../../" && pwd)"

case $(echo $1 | awk '{print tolower($0)}') in
    -d | --dockerfile)
        DOCKER_FILE=$2
        ;;
    -i | --image)
        DOCKER_IMAGE=$2
        ;;
    *)
        echo "usage: $0 [[-d | --dockerfile] <path-to-dockerfile>] | [-i | --image] <image-id>]] cmd-to-run"
        exit 1
        ;;
esac

shift
shift

if [ $DOCKER_FILE ]; then
    if [ -d $DOCKER_FILE ]; then
        DOCKER_FILE="$DOCKER_FILE/Dockerfile"
    fi

    DOCKER_FILE_DIR=$(dirname $DOCKER_FILE)
    DOCKER_IMAGE=$(set -x ; docker build -q -f $DOCKER_FILE $DOCKER_FILE_DIR)
fi

DOCKER_USERADD_AND_SWITCH_CMD=""

if [ ! $(id -u) = 0 ]; then
    DOCKER_USERADD_AND_SWITCH_CMD="useradd -m -u $(id -u) $(id -n -u) && su $(id -n -u) -c "
fi

ARGS=$(IFS=' ' ; echo $@)
(set -x ; docker run --rm --init -v $REPO_ROOT:/code -t $DOCKER_IMAGE /bin/sh -c "cd /code ; $DOCKER_USERADD_AND_SWITCH_CMD\"$ARGS\"")
