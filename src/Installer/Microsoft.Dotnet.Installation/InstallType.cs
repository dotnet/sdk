﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation;

public enum InstallType
{
    None,
    //  Inconsistent would be when the dotnet on the path doesn't match what DOTNET_ROOT is set to
    Inconsistent,
    Admin,
    User
}
