// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Format;

public class FormatForwardingApp(IEnumerable<string> argsToForward)
    : ForwardingApp(
        forwardApplicationPath: GetForwardApplicationPath(),
        argsToForward: argsToForward,
        depsFile: GetDepsFilePath(),
        runtimeConfig: GetRuntimeConfigPath())
{
    private static string GetForwardApplicationPath()
        => PathResolver.Default.GetFormatPath();

    private static string GetDepsFilePath()
        => PathResolver.Default.GetFormatDepsPath();

    private static string GetRuntimeConfigPath()
        => PathResolver.Default.GetFormatRuntimeConfigPath();
}
