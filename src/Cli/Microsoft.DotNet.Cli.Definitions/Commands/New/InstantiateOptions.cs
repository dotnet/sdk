// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.New;

public sealed class InstantiateOptions
{
    public readonly Option<FileInfo> OutputOption = SharedOptionsFactory.CreateOutputOption();
    public readonly Option<string> NameOption = SharedOptionsFactory.CreateNameOption();
    public readonly Option<bool> DryRunOption = SharedOptionsFactory.CreateDryRunOption();
    public readonly Option<bool> ForceOption = SharedOptionsFactory.CreateForceOption();
    public readonly Option<bool> NoUpdateCheckOption = SharedOptionsFactory.CreateNoUpdateCheckOption();
    public readonly Option<FileInfo> ProjectOption = SharedOptionsFactory.CreateProjectOption();

    public IEnumerable<Option> AllOptions
    {
        get
        {
            yield return OutputOption;
            yield return NameOption;
            yield return DryRunOption;
            yield return ForceOption;
            yield return NoUpdateCheckOption;
            yield return ProjectOption;
        }
    }
}
