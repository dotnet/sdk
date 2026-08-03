// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using VerifyTests.DiffPlex;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public static class ModuleInitializer
    {
        [ModuleInitializer]
        public static void Init()
        {
            // The dotnet CLI writes UTF-8 to stdout. The test framework captures child-process
            // output using Console.OutputEncoding when no explicit encoding is set (see
            // Command.StandardOutput). Under the previous xUnit/VSTest host the test process used
            // a UTF-8 console, so localized (non-ASCII) output was decoded correctly. The
            // Microsoft.Testing.Platform host does not force UTF-8, so on the .NET host German
            // output was decoded using the OEM code page and corrupted, breaking DotnetNewLocaleTests.
            // Restore UTF-8 to keep localized output assertions working.
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch (IOException)
            {
                // No console is attached (can happen with redirected output on .NET Framework); ignore.
            }

            DerivePathInfo(
                   (_, _, type, method) => new(
                       directory: BaseIntegrationTest.ApprovalsDirectory,
                       typeName: type.Name,
                       methodName: method.Name));

            // Customize diff output of verifier
            VerifyDiffPlex.Initialize(OutputType.Compact);
        }
    }
}
