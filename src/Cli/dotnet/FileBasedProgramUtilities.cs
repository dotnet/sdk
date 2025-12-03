// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.FileBasedPrograms;

internal static partial class ErrorReporters
{
    public static readonly ErrorReporter ThrowingReporter =
        static (sourceFile, textSpan, message) => throw new GracefulException($"{sourceFile.GetLocationString(textSpan)}: {FileBasedProgramsResources.DirectiveError}: {message}");

    public static readonly ErrorReporter IgnoringReporter =
        static (_, _, _) => { };
}
