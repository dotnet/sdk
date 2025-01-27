// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Completions.Tests;

using System.Runtime.CompilerServices;

public static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifyDiffPlex.Initialize(VerifyTests.DiffPlex.OutputType.Compact);
        if (IsCI())
        {
            Verifier.DerivePathInfo((sourceFile, projectDirectory, type, method) => new(
                directory: Path.Combine(Environment.CurrentDirectory, "snapshots"),
                typeName: type.Name,
                methodName: method.Name)
            );
        }
        EmptyFiles.FileExtensions.AddTextExtension("ps1");
        EmptyFiles.FileExtensions.AddTextExtension("nu");
    }

    private static bool IsCI() => Environment.GetEnvironmentVariable("CI") is not null;
}
