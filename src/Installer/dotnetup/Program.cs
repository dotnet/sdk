// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// NativeAOT entry point for dotnetup. Forwards immediately to
/// <see cref="DotnetupProgram.Main"/> in the referenced dotnetup.Library project.
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
        => DotnetupProgram.Main(args);
}
