// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands;

/// <summary>
/// If a <see cref="CliCommand"/> implements this interface, it can open
/// its documentation page online.
/// </summary>
public interface ICommandDocument
{
    /// <summary>
    /// The URL to the documentation page for this command.
    /// </summary>
    string DocsLink { get; }
}
