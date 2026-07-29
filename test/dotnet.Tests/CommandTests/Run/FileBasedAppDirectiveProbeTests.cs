// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.FileBasedPrograms;

namespace Microsoft.DotNet.Cli.Run.Tests;

/// <summary>
/// Differentially tests the conservative directive probe against Roslyn directive parsing.
/// </summary>
[TestClass]
public class FileBasedAppDirectiveProbeTests
{
    /// <summary>Verifies that a <c>None</c> probe result implies Roslyn finds no application directive.</summary>
    [TestMethod]
    public void NoneImpliesRoslynFindsNoAppDirective()
    {
        string[] sources =
        [
            string.Empty,
            "Console.WriteLine(42);",
            "#!/usr/bin/env dotnet\nConsole.WriteLine(42);",
            "#:package Example@1.0.0\nConsole.WriteLine(42);",
            "Console.WriteLine(42);\n#:package Example@1.0.0",
            "// #:package Example@1.0.0\nConsole.WriteLine(42);",
            "/* #:package Example@1.0.0 */\nConsole.WriteLine(42);",
            "var text = \"#:package Example@1.0.0\";",
            "var text = @\"#:package Example@1.0.0\";",
            "var text = \"\"\"#:package Example@1.0.0\"\"\";",
            "#if true\nConsole.WriteLine(42);\n#endif",
            "\r\n\tConsole.WriteLine(42);\r\n",
        ];

        foreach (string source in sources)
        {
            AssertOneWaySafety(source);
        }
    }

    /// <summary>Verifies the one-way safety property across a deterministic generated corpus.</summary>
    [TestMethod]
    public void GeneratedCorpusPreservesOneWaySafety()
    {
        string[] fragments =
        [
            "#:sdk Microsoft.NET.Sdk\n",
            "#!/usr/bin/env dotnet\n",
            "// #:package Comment@1.0.0\n",
            "/* #:package Block@1.0.0 */\n",
            "var text = \"#:package String@1.0.0\";\n",
            "var raw = \"\"\"#:package Raw@1.0.0\"\"\";\n",
            "Console.WriteLine(42);\n",
            "#if true\n#endif\n",
            "\r\n",
            "\n",
        ];
        var random = new Random(1729);

        for (int inputIndex = 0; inputIndex < 500; inputIndex++)
        {
            int fragmentCount = random.Next(1, 7);
            var source = new StringBuilder();
            for (int fragmentIndex = 0; fragmentIndex < fragmentCount; fragmentIndex++)
            {
                source.Append(fragments[random.Next(fragments.Length)]);
            }

            AssertOneWaySafety(source.ToString());
        }
    }

    private static void AssertOneWaySafety(string source)
    {
        string path = Path.Join(Path.GetTempPath(), $"dotnet-directive-probe-{Guid.NewGuid():N}.cs");
        try
        {
            File.WriteAllText(path, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            FileBasedAppDirectiveProbeResult result = FileBasedAppDirectiveProbe.Probe(path);
            if (result != FileBasedAppDirectiveProbeResult.None)
            {
                return;
            }

            var sourceFile = new SourceFile(path, SourceText.From(source, Encoding.UTF8));
            var directives = FileLevelDirectiveHelpers.FindDirectives(
                sourceFile,
                reportAllErrors: false,
                errorReporter: static (_, _, _, _, _) => { });
            Assert.IsFalse(
                directives.Any(static directive => directive is not CSharpDirective.Shebang),
                $"Probe returned None for source containing an app directive:{Environment.NewLine}{source}");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
