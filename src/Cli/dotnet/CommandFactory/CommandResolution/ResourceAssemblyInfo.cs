// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

internal class ResourceAssemblyInfo(string culture, string relativePath)
{
    public string Culture { get; } = culture;
    public string RelativePath { get; } = relativePath;
}
