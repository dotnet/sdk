// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if CLI_AOT
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Contains a parsed launch profile and diagnostics buffered until the Native AOT path commits.
/// </summary>
/// <param name="Profile">The selected launch profile, or <see langword="null"/>.</param>
/// <param name="Messages">The buffered message text and error-channel selection.</param>
internal sealed record LaunchProfileReadResult(
    LaunchProfile? Profile,
    IReadOnlyList<(string Message, bool IsError)> Messages)
{
    /// <summary>
    /// Writes the buffered launch-profile messages to their selected reporters.
    /// </summary>
    internal void WriteMessages()
    {
        foreach ((string message, bool isError) in Messages)
        {
            (isError ? Reporter.Error : Reporter.Output).WriteLine(message);
        }
    }
}
#endif
