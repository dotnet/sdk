// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli;

public class DocumentedCommand(string name, string docsLink, string description = null) : CliCommand(name, description), ICommandDocument
{
    public string DocsLink { get; } = docsLink;
}
