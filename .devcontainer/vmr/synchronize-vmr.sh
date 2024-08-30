#!/bin/bash

(cd /workspaces/sdk \
    && ./eng/vmr-sync.sh --vmr /workspaces/dotnet --tmp /workspaces/tmp $*)
