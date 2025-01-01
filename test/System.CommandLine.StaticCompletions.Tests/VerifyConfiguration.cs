// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Tests;

using System.Runtime.CompilerServices;

public static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifyDiffPlex.Initialize(VerifyTests.DiffPlex.OutputType.Compact);
        // prevent CI systems from trying to make a snapshot directory, maybe?
        DerivePathInfo((_, _, _, _) => new("snapshots"));
        EmptyFiles.FileExtensions.AddTextExtension("ps1");
        EmptyFiles.FileExtensions.AddTextExtension("nu");
    }
}
