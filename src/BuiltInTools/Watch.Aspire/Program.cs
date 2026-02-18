// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Locator;
using Microsoft.DotNet.Watch;

if (!DotNetWatchOptions.TryParse(args, out var options))
{
    return -1;
}

MSBuildLocator.RegisterMSBuildPath(options.SdkDirectory);

var workingDirectory = Directory.GetCurrentDirectory();
return await DotNetWatchLauncher.RunAsync(workingDirectory, options) ? 0 : 1;
