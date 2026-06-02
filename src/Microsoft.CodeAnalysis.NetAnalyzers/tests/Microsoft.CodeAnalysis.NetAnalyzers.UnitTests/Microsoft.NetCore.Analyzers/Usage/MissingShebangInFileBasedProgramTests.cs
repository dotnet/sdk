// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
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
            // Entry point file without shebang and a #:include file - warning expected.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("Test0.cs", """
                            #:include Util.cs
                            class Program { static void Main() { } }
                            """),
                        ("Util.cs", """class Util { public static string Greet() => "hello"; }"""),
                    },
                    AnalyzerConfigFiles = { ("/.globalconfig", GlobalConfig) },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult(MissingShebangInFileBasedProgram.Rule).WithLocation("Test0.cs", 1, 1),
                    },
                },
                SolutionTransforms = { EnableFileBasedProgramFeature },
            }.RunAsync();
        }

        [Fact]
        public async Task ExtraCompileFileNotFromIncludeDirective_NoDiagnosticAsync()
        {
            // A second Compile item from other MSBuild code does not require a shebang.
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
                },
            }.RunAsync(TestContext.Current.CancellationToken);
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
            }.RunAsync(TestContext.Current.CancellationToken);
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
                        ("Test0.cs", """
                            #:include Util.cs
                            class Program { static void Main() { } }
                            """),
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
                            #:include Util.cs
                            class Program { static void Main() { } }
                            """),
                        ("Util.cs", """class Util { public static string Greet() => "hello"; }"""),
                    },
                },
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                SolutionTransforms = { EnableFileBasedProgramFeature },
            }.RunAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task EntryPointWithShebang_MultipleFiles_NoDiagnosticAsync()
        {
            // Entry point already has shebang, multiple files - no diagnostic.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("Test0.cs", """
                            #!/usr/bin/env dotnet
                            #:include Util.cs
                            class Program { static void Main() { } }
                            """),
                        ("Util.cs", """class Util { public static string Greet() => "hello"; }"""),
                    },
                    AnalyzerConfigFiles = { ("/.globalconfig", GlobalConfig) },
                },
                SolutionTransforms = { EnableFileBasedProgramFeature },
            }.RunAsync(TestContext.Current.CancellationToken);
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
            }.RunAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task GeneratedCodeFile_NoDiagnosticAsync()
        {
            // Entry point file without shebang, but no #:include directive - no diagnostic.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("Test0.cs", """class Program { static void Main() { } }"""),
                        ("Test1.g.cs", """class Generated { }"""),
                    },
                    AnalyzerConfigFiles = { ("/.globalconfig", GlobalConfig) },
                },
            }.RunAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task AutoGeneratedComment_NoDiagnosticAsync()
        {
            // Entry point file without shebang, but no #:include directive - no diagnostic.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("Test0.cs", """class Program { static void Main() { } }"""),
                        ("AssemblyInfo.cs", """
                            // <auto-generated/>
                            using System;
                            [assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
                            """),
                    },
                    AnalyzerConfigFiles = { ("/.globalconfig", GlobalConfig) },
                },
            }.RunAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task GeneratedCodePlusRealFile_WarningAsync()
        {
            // Entry point file without shebang and a #:include directive - warning expected.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("Test0.cs", """
                            #:include Util.cs
                            class Program { static void Main() { } }
                            """),
                        ("Util.cs", """class Util { }"""),
                        ("Test1.g.cs", """class Generated { }"""),
                    },
                    AnalyzerConfigFiles = { ("/.globalconfig", GlobalConfig) },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult(MissingShebangInFileBasedProgram.Rule).WithLocation("Test0.cs", 1, 1),
                    },
                },
                SolutionTransforms = { EnableFileBasedProgramFeature },
            }.RunAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task ShebangNotAtPositionZero_WarningAsync()
        {
            // A class declaration before #! prevents the parser from treating it as ShebangDirectiveTrivia,
            // so the analyzer warns about the missing shebang.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("Test0.cs", """
                            #:include Util.cs
                            class Foo { }
                            #!/usr/bin/env dotnet
                            class Program { static void Main() { } }
                            """),
                        ("Util.cs", """class Util { }"""),
                    },
                    AnalyzerConfigFiles = { ("/.globalconfig", GlobalConfig) },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult(MissingShebangInFileBasedProgram.Rule).WithLocation("Test0.cs", 1, 1),
                        // Test0.cs(2,1): error CS9378: '#!' must be the first characters on the first line of the file
                        DiagnosticResult.CompilerError("CS9378").WithSpan("Test0.cs", 3, 1, 3, 2),
                    },
                },
                SolutionTransforms = { EnableFileBasedProgramFeature },
            }.RunAsync(TestContext.Current.CancellationToken);
        }

        private static Solution EnableFileBasedProgramFeature(Solution solution, ProjectId projectId)
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId,
                parseOptions.WithFeatures(parseOptions.Features.Concat(
                    [new KeyValuePair<string, string>("FileBasedProgram", "true")])));
        }
    }
}