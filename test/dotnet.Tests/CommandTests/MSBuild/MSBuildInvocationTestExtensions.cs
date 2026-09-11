// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.MSBuild.Tests;

internal static class MSBuildInvocationTestExtensions
{
    public static IEnumerable<string> WithoutLLMSpecificArguments(this IEnumerable<string> arguments) =>
        arguments.Where(argument => argument != Constants.TerminalLogger_DisableNodeDisplay);
}
