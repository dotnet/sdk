#!/bin/sh
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

first_run() {
    /usr/share/dotnet/dotnet exec /usr/share/dotnet/sdk/%SDK_VERSION%/dotnet.dll internal-reportinstallsuccess "rpmpackage" > /dev/null 2>&1 || true
}

INSTALL_TEMP_HOME=/tmp/dotnet-installer
[ -d $INSTALL_TEMP_HOME ] || mkdir $INSTALL_TEMP_HOME
HOME=$INSTALL_TEMP_HOME first_run
