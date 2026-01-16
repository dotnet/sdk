// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.InternalReportInstallSuccess;

internal sealed class InternalReportInstallSuccessCommandDefinition : Command
{
    public readonly Argument<string> Argument = new("internal-reportinstallsuccess-arg");

    public InternalReportInstallSuccessCommandDefinition()
        : base("internal-reportinstallsuccess")
    {
        Hidden = true;
        Arguments.Add(Argument);
    }
}
