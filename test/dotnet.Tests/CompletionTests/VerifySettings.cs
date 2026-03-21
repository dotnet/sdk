// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Cli.Completions.Tests;

public static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifyDiffPlex.Initialize(VerifyTests.DiffPlex.OutputType.Compact);
        DerivePathInfo((sourceFile, projectDirectory, type, method) => new(
            directory: Path.Combine(AppContext.BaseDirectory, "snapshots"),
            typeName: type.Name,
            methodName: method.Name)
        );
        EmptyFiles.FileExtensions.AddTextExtension("ps1");
        EmptyFiles.FileExtensions.AddTextExtension("nu");
    }
}
