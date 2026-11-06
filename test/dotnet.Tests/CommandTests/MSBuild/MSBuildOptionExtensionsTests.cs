// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.MSBuild.Tests;

[TestClass]
public class MSBuildOptionExtensionsTests : SdkTest
{
    [TestMethod]
    [DataRow("out;put", "out%3bput")]
    [DataRow("out%3Bput", "out%253Bput")]
    [DataRow("out$(Configuration)", "out%24%28Configuration%29")]
    [DataRow("out@(Items)", "out%40%28Items%29")]
    [DataRow("out'put", "out%27put")]
    [DataRow("out,put", "out%2Cput")]
    public void OutputPathEscapesLiteralValue(string directoryName, string escapedDirectoryName)
        => VerifyOutputPathEscaping(directoryName, escapedDirectoryName);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("out\"put", "out%22put")]
    [DataRow("out*put", "out%2aput")]
    [DataRow("out?put", "out%3fput")]
    public void OutputPathEscapesUnixLiteralValue(string directoryName, string escapedDirectoryName)
        => VerifyOutputPathEscaping(directoryName, escapedDirectoryName);

    private static void VerifyOutputPathEscaping(string directoryName, string escapedDirectoryName)
    {
        string basePath = TestPathUtilities.FormatAbsolutePath("base");
        CommandDirectoryContext.PerformActionWithBasePath(basePath, () =>
        {
            var outputOption = new Option<string>("--output").ForwardAsOutputPath("OutputPath");
            var command = new Command("build") { outputOption };
            command.Parse(["--output", directoryName]).OptionValuesToBeForwarded()
                .Should().BeEquivalentTo([
                    $"--property:OutputPath={Path.Combine(basePath, escapedDirectoryName)}{Path.DirectorySeparatorChar}",
                    "--property:_CommandLineDefinedOutputPath=true"
                ]);
        });
    }

    [TestMethod]
    public void AbsentOutputPathDoesNotForwardProperties()
    {
        var outputOption = new Option<string>("--output").ForwardAsOutputPath("OutputPath");
        var command = new Command("build") { outputOption };
        command.Parse([]).OptionValuesToBeForwarded().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OutputPathPreservesTrailingSeparatorAndQuoting(bool trailingSeparator)
    {
        string basePath = TestPathUtilities.FormatAbsolutePath("base");
        CommandDirectoryContext.PerformActionWithBasePath(basePath, () =>
        {
            var outputOption = new Option<string>("--output").ForwardAsOutputPath("OutputPath", surroundWithDoubleQuotes: true);
            var command = new Command("test") { outputOption };
            string directoryName = "out;put" + (trailingSeparator ? Path.DirectorySeparatorChar : "");
            string expectedPath = Path.Combine(basePath, "out%3bput") + Path.DirectorySeparatorChar;

            command.Parse(["--output", directoryName]).OptionValuesToBeForwarded()
                .Should().Contain($"--property:OutputPath={MSBuildPropertyParser.SurroundWithDoubleQuotes(expectedPath)}");
        });
    }

    [TestMethod]
    [DataRow("A=one%3Btwo", new string[] { "--property:A=one%3Btwo" })]
    [DataRow("A=$(Configuration)", new string[] { "--property:A=$(Configuration)" })]
    [DataRow("A=\"one;two\"", new string[] { "--property:A=\"one;two\"" })]
    [DataRow("A=one;B=two", new string[] { "--property:A=one", "--property:B=two" })]
    public void ExplicitMSBuildPropertiesRetainTheirSyntax(string properties, string[] expectedArguments)
    {
        var propertyOption = CommonOptions.CreatePropertyOption();
        var command = new Command("build") { propertyOption };
        command.Parse([$"-p:{properties}"]).OptionValuesToBeForwarded()
            .Should().BeEquivalentTo(expectedArguments);
    }
}
