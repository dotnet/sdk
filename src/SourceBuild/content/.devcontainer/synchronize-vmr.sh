#!/bin/bash

(cd /workspaces/dotnet/src/sdk \
    && ./eng/vmr-sync.sh --vmr /workspaces/dotnet --tmp /workspaces/tmp $*)
