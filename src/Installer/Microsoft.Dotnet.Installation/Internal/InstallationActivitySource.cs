// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Dotnet.Installation.Internal;

internal static class InstallationActivitySource
{
    private static readonly ActivitySource s_activitySource = new("Microsoft.Dotnet.Installer", "1.0.0");

    public static ActivitySource ActivitySource => s_activitySource;
}
