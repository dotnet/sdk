// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Usage.CSharpMissingShebangInFileBasedProgram,
    Microsoft.NetCore.CSharp.Analyzers.Usage.CSharpMissingShebangInFileBasedProgramFixer>;

namespace Microsoft.NetCore.Analyzers.Usage.UnitTests
{
    public class MissingShebangInFileBasedProgramTests
    {
        private const string GlobalConfig = "is_global = true\r\nbuild_property.EntryPointFilePath = Test0.cs";

        [Fact]
        public async Task EntryPointWithoutShebang_MultipleFiles_WarningAsync()
        {
            // Entry point file without shebang, multiple files - warning expected.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("Test0.cs", """class Program { static void Main() { } }"""),
                        ("Util.cs", """class Util { public static string Greet() => "hello"; }"""),
                    },
                    AnalyzerConfigFiles = { ("/.globalconfig", GlobalConfig) },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult(MissingShebangInFileBasedProgram.Rule).WithLocation("Test0.cs", 1, 1),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task NoEntryPointFilePath_NoDiagnosticAsync()
        {
            // No EntryPointFilePath - not a file-based program, no diagnostic.
            await VerifyCS.VerifyAnalyzerAsync("""
                class Program
                {
                    static void Main()
                    {
                        System.Console.WriteLine("hello");
                    }
                }
                """);
        }

        [Fact]
        public async Task SingleFile_NoDiagnosticAsync()
        {
            // Single file - no need to distinguish entry point, no diagnostic.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { ("Test0.cs", """class Program { static void Main() { } }""") },
                    AnalyzerConfigFiles = { ("/.globalconfig", GlobalConfig) },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task NonEntryPointFile_MultipleFiles_NoDiagnosticAsync()
        {
            // Only the entry point gets the diagnostic, not other files.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("Test0.cs", """class Program { static void Main() { Util.Greet(); } }"""),
                        ("Util.cs", """class Util { public static string Greet() => "hello"; }"""),
                    },
                    AnalyzerConfigFiles = { ("/.globalconfig", GlobalConfig) },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult(MissingShebangInFileBasedProgram.Rule).WithLocation("Test0.cs", 1, 1),
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task EntryPointWithoutShebang_CodeFixAddsShebangAsync()
        {
            // Verify that the code fix prepends a shebang line.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("Test0.cs", """class Program { static void Main() { } }"""),
                        ("Util.cs", """class Util { public static string Greet() => "hello"; }"""),
                    },
                    AnalyzerConfigFiles = { ("/.globalconfig", GlobalConfig) },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult(MissingShebangInFileBasedProgram.Rule).WithLocation("Test0.cs", 1, 1),
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        ("Test0.cs", """
                            #!/usr/bin/env dotnet
                            class Program { static void Main() { } }
                            """),
                        ("Util.cs", """class Util { public static string Greet() => "hello"; }"""),
                    },
                },
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                SolutionTransforms =
                {
                    (solution, projectId) =>
                    {
                        // Enable #! shebang support in the parser.
                        var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
                        return solution.WithProjectParseOptions(projectId,
                            parseOptions.WithFeatures(parseOptions.Features.Concat(
                                [new KeyValuePair<string, string>("FileBasedProgram", "true")])));
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task EmptyEntryPointFilePath_NoDiagnosticAsync()
        {
            // Empty EntryPointFilePath - not a file-based program, no diagnostic.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("Test0.cs", """class Program { static void Main() { } }"""),
                        ("Util.cs", """class Util { }"""),
                    },
                    AnalyzerConfigFiles = { ("/.globalconfig", "is_global = true\r\nbuild_property.EntryPointFilePath = ") },
                },
            }.RunAsync();
        }
    }
}