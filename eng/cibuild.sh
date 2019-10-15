#!/usr/bin/env bash
cd "$(dirname "${BASH_SOURCE[0]}")";

case $(uname) in
  Linux)
    sudo -n ulimit -n 8192
    ;;
esac
exec common/cibuild.sh

