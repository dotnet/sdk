// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Basic.CompilerLog.Util;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.NET.Build.Tests;

public sealed class RoslynBuildTaskTests(ITestOutputHelper log) : SdkTest(log)
{
    private static string CompilerFileNameWithoutExtension(Language language) => language switch
    {
        Language.CSharp => "csc",
        Language.VisualBasic => "vbc",
        _ => throw new ArgumentOutOfRangeException(paramName: nameof(language)),
    };

    private static string CoreCompilerFileName(Language language) => CompilerFileNameWithoutExtension(language) + ".dll";

    private static string FxCompilerFileName(Language language) => CompilerFileNameWithoutExtension(language) + ".exe";

    [FullMSBuildOnlyTheory, CombinatorialData]
    public void FullMSBuild_SdkStyle(bool useSharedCompilation, Language language)
    {
        var testAsset = CreateProject(useSharedCompilation, language);
        var buildCommand = BuildAndRunUsingMSBuild(testAsset);
        VerifyCompiler(buildCommand, CoreCompilerFileName(language), useSharedCompilation);
    }

    [FullMSBuildOnlyTheory, CombinatorialData]
    public void FullMSBuild_SdkStyle_OptOut(bool useSharedCompilation, Language language)
    {
        var testAsset = CreateProject(useSharedCompilation, language).WithProjectChanges(static doc =>
        {
            doc.Root!.Element("PropertyGroup")!.Add(new XElement("RoslynCompilerType", "Framework"));
        });
        var buildCommand = BuildAndRunUsingMSBuild(testAsset);
        VerifyCompiler(buildCommand, FxCompilerFileName(language), useSharedCompilation);
    }

    [FullMSBuildOnlyTheory, CombinatorialData]
    public void FullMSBuild_NonSdkStyle(bool useSharedCompilation, Language language)
    {
        var testAsset = CreateProject(useSharedCompilation, language, static project =>
        {
            project.IsSdkProject = false;
            project.TargetFrameworkVersion = "v4.7.2";
        });
        var buildCommand = BuildAndRunUsingMSBuild(testAsset);
        VerifyCompiler(buildCommand, FxCompilerFileName(language), useSharedCompilation);
    }

    [Theory, CombinatorialData]
    public void DotNet(bool useSharedCompilation, Language language)
    {
        var testAsset = CreateProject(useSharedCompilation, language);
        var buildCommand = BuildAndRunUsingDotNet(testAsset);
        VerifyCompiler(buildCommand, CoreCompilerFileName(language), useSharedCompilation);
    }

    private TestAsset CreateProject(bool useSharedCompilation, Language language, Action<TestProject>? configure = null, [CallerMemberName] string callingMethod = "")
    {
        var (projExtension, sourceName, sourceText) = language switch
        {
            Language.CSharp => (".csproj", "Program.cs", """
                class Program
                {
                    static void Main()
                    {
                        System.Console.WriteLine(40 + 2);
                    }
                }
                """),
            Language.VisualBasic => (".vbproj", "Program.vb", """
                Module Program
                    Sub Main()
                        System.Console.WriteLine(40 + 2)
                    End Sub
                End Module
                """),
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(language)),
        };

        var project = new TestProject
        {
            Name = "App1",
            IsExe = true,
            SourceFiles =
            {
                [sourceName] = sourceText,
            },
        };

        // UseSharedCompilation should be the default, so set it only if false.
        if (!useSharedCompilation)
        {
            project.AdditionalProperties["UseSharedCompilation"] = "false";
        }

        configure?.Invoke(project);
        return _testAssetsManager.CreateTestProject(project, callingMethod: callingMethod, targetExtension: projExtension);
    }

    private TestCommand BuildAndRunUsingMSBuild(TestAsset testAsset)
    {
        var buildCommand = new MSBuildCommand(testAsset, "Build");
        buildCommand.WithWorkingDirectory(testAsset.Path)
            .Execute("-bl").Should().Pass();

        Run(buildCommand.GetOutputDirectory().File(testAsset.TestProject!.GetOutputFileName()));

        return buildCommand;
    }

    private TestCommand BuildAndRunUsingDotNet(TestAsset testAsset)
    {
        var buildCommand = new DotnetBuildCommand(testAsset);
        buildCommand.Execute("-bl").Should().Pass();

        Run(buildCommand.GetOutputDirectory().File(testAsset.TestProject!.GetOutputFileName()));

        return buildCommand;
    }

    private void Run(FileInfo outputFile)
    {
        var runCommand = new RunExeCommand(Log, outputFile.FullName);
        runCommand.Execute().Should().Pass()
            .And.HaveStdOut("42");
    }

    private static void VerifyCompiler(TestCommand buildCommand, string compilerFileName, bool usedCompilerServer)
    {
        var binaryLogPath = Path.Join(buildCommand.WorkingDirectory, "msbuild.binlog");
        using (var reader = BinaryLogReader.Create(binaryLogPath))
        {
            var call = reader.ReadAllCompilerCalls().Should().ContainSingle().Subject;
            Path.GetFileName(call.CompilerFilePath).Should().Be(compilerFileName);
        }

        // Verify compiler server message.
        var compilerServerMesssages = BinaryLog.ReadBuild(binaryLogPath).FindChildrenRecursive<Message>(
            static message => message.Text.StartsWith("CompilerServer:", StringComparison.Ordinal));
        compilerServerMesssages.Should().ContainSingle().Which.Text.Should().StartWith(usedCompilerServer
            ? "CompilerServer: server - server processed compilation - "
            : "CompilerServer: tool - using command line tool by design");
    }

    public enum Language
    {
        CSharp,
        VisualBasic,
    }
}
