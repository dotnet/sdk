// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier.Commands;

/// <summary>
/// The result information of a template instantiation action.
/// </summary>
public interface IInstantiationResult
{
    /// <summary>
    /// Exit code of the action (e.g. exit code of the dotnet new command).
    /// 0 indicates successful action. Nonzero otherwise.
    /// </summary>
    int ExitCode { get; }

    /// <summary>
    /// Standard output stream content for the instantiation action.
    /// </summary>
    string StdOut { get; }

    /// <summary>
    /// Standard error stream content for the instantiation action.
    /// </summary>
    string StdErr { get; }

    /// <summary>
    /// Path to directory containing the output of the instantiation.
    /// </summary>
    string InstantiatedContentDirectory { get; }
}
