// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Usage.CSharpPreferQuotedFileBasedProgramDirective,
    Microsoft.NetCore.CSharp.Analyzers.Usage.CSharpPreferQuotedFileBasedProgramDirectiveFixer>;

namespace Microsoft.NetCore.Analyzers.Usage.UnitTests;

[TestClass]
public class PreferQuotedFileBasedProgramDirectiveTests
{
    private static DiagnosticResult Expected(string kind, int line = 1)
        => new DiagnosticResult(PreferQuotedFileBasedProgramDirective.Rule).WithLocation("Test0.cs", line, 1).WithArguments(kind);

    [TestMethod]
    public async Task PropertyUnquotedValue_WarningAndFixAsync()
    {
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        #:property Description=Hello World
                        class Program { static void Main() { } }
                        """),
                },
                ExpectedDiagnostics = { Expected("property") },
            },
            FixedState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        #:property Description="Hello World"
                        class Program { static void Main() { } }
                        """),
                },
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            SolutionTransforms = { EnableFileBasedProgramFeature },
        }.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task PropertySpacesAroundSeparator_FixPreservesWhitespaceAsync()
    {
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        #:property    Prop = Value
                        class Program { static void Main() { } }
                        """),
                },
                ExpectedDiagnostics = { Expected("property") },
            },
            FixedState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        #:property    Prop = "Value"
                        class Program { static void Main() { } }
                        """),
                },
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            SolutionTransforms = { EnableFileBasedProgramFeature },
        }.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    [DataRow("project")]
    [DataRow("ref")]
    [DataRow("include")]
    [DataRow("exclude")]
    public async Task WholeValueWithWhitespace_WarningAndFixAsync(string kind)
    {
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Test0.cs", $$"""
                        #:{{kind}} ../My Library/thing
                        class Program { static void Main() { } }
                        """),
                },
                ExpectedDiagnostics = { Expected(kind) },
            },
            FixedState =
            {
                Sources =
                {
                    ("Test0.cs", $$"""
                        #:{{kind}} "../My Library/thing"
                        class Program { static void Main() { } }
                        """),
                },
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            SolutionTransforms = { EnableFileBasedProgramFeature },
        }.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task WholeValueWithBackslash_FixEscapesAsync()
    {
        // The quoted form is a regular C# string literal, so a backslash in the value must be
        // escaped for the fix to round-trip (an unescaped '\M' would be an invalid escape sequence).
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        #:project ..\My Library
                        class Program { static void Main() { } }
                        """),
                },
                ExpectedDiagnostics = { Expected("project") },
            },
            FixedState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        #:project "..\\My Library"
                        class Program { static void Main() { } }
                        """),
                },
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            SolutionTransforms = { EnableFileBasedProgramFeature },
        }.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    [DataRow("sdk")]
    [DataRow("package")]
    public async Task SpacesAroundNameVersionSeparator_FixPreservesWhitespaceAsync(string kind)
    {
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Test0.cs", $$"""
                        #:{{kind}}    First @ 1.0
                        class Program { static void Main() { } }
                        """),
                },
                ExpectedDiagnostics = { Expected(kind) },
            },
            FixedState =
            {
                Sources =
                {
                    ("Test0.cs", $$"""
                        #:{{kind}}    First @ "1.0"
                        class Program { static void Main() { } }
                        """),
                },
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            SolutionTransforms = { EnableFileBasedProgramFeature },
        }.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task MultipleDirectives_AllFixedAsync()
    {
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        #:property Description=Hello World
                        #:project  ../My Library
                        class Program { static void Main() { } }
                        """),
                },
                ExpectedDiagnostics =
                {
                    Expected("property", line: 1),
                    Expected("project", line: 2),
                },
            },
            FixedState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        #:property Description="Hello World"
                        #:project  "../My Library"
                        class Program { static void Main() { } }
                        """),
                },
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            NumberOfFixAllIterations = 1,
            SolutionTransforms = { EnableFileBasedProgramFeature },
        }.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    [DataRow("#:property Description=\"Hello World\"")]
    [DataRow("#:property Description=Hello")]
    [DataRow("#:package Package@1.0.0")]
    [DataRow("#:package Package@1.0.0 ExcludeAssets=runtime PrivateAssets=all")]
    [DataRow("#:project ../Lib Private=false")]
    [DataRow("#:ref ../lib.cs Aliases=lib")]
    public async Task NewForm_NoDiagnosticAsync(string directive)
    {
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Test0.cs", $$"""
                        {{directive}}
                        class Program { static void Main() { } }
                        """),
                },
            },
            SolutionTransforms = { EnableFileBasedProgramFeature },
        }.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task UnfixableLegacyForm_WarningWithoutFixAsync()
    {
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        #:package Name@1.0 Property
                        class Program { static void Main() { } }
                        """),
                },
                ExpectedDiagnostics = { Expected("package") },
            },
            SolutionTransforms = { EnableFileBasedProgramFeature },
        }.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task TriviaIsPreservedAsync()
    {
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        // Before
                        #:property Description=Hello World

                        // After
                        class Program { static void Main() { } }
                        """),
                },
                ExpectedDiagnostics = { Expected("property", line: 2) },
            },
            FixedState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        // Before
                        #:property Description="Hello World"

                        // After
                        class Program { static void Main() { } }
                        """),
                },
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            SolutionTransforms = { EnableFileBasedProgramFeature },
        }.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task NoEntryPointFilePath_StillFiresAsync()
    {
        // The analyzer inspects every ignored directive trivia regardless of EntryPointFilePath.
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        #:property Description=Hello World
                        class Program { static void Main() { } }
                        """),
                },
                ExpectedDiagnostics = { Expected("property") },
            },
            FixedState =
            {
                Sources =
                {
                    ("Test0.cs", """
                        #:property Description="Hello World"
                        class Program { static void Main() { } }
                        """),
                },
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            SolutionTransforms = { EnableFileBasedProgramFeature },
        }.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task DirectiveInNonEntryPointFile_StillFiresAsync()
    {
        // A legacy directive in any file is flagged, not only the entry point.
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Test0.cs", """class Program { static void Main() { } }"""),
                    ("Other.cs", """
                        #:property Description=Hello World
                        class Other { }
                        """),
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(PreferQuotedFileBasedProgramDirective.Rule).WithLocation("Other.cs", 1, 1).WithArguments("property"),
                },
            },
            FixedState =
            {
                Sources =
                {
                    ("Test0.cs", """class Program { static void Main() { } }"""),
                    ("Other.cs", """
                        #:property Description="Hello World"
                        class Other { }
                        """),
                },
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            SolutionTransforms = { EnableFileBasedProgramFeature },
        }.RunAsync(CancellationToken.None);
    }

    private static Solution EnableFileBasedProgramFeature(Solution solution, ProjectId projectId)
    {
        var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
        return solution.WithProjectParseOptions(projectId,
            parseOptions.WithFeatures(parseOptions.Features.Concat(
                [new KeyValuePair<string, string>("FileBasedProgram", "true")])));
    }
}
