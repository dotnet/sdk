#!/bin/bash

(cd /workspaces/dotnet/src/installer \
    && ./eng/vmr-sync.sh --vmr /workspaces/dotnet --tmp /workspaces/tmp --no-vmr-prepare $*)
