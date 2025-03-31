// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Basic.CompilerLog.Util;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.NET.Build.Tests;

public sealed class RoslynBuildTaskTests(ITestOutputHelper log) : SdkTest(log)
{
    private const string CoreCompilerFileName = "csc.dll";
    private const string FxCompilerFileName = "csc.exe";

    [FullMSBuildOnlyTheory, CombinatorialData]
    public void FullMSBuild_SdkStyle(bool useSharedCompilation)
    {
        var testAsset = CreateProject(useSharedCompilation);
        var buildCommand = BuildAndRunUsingMSBuild(testAsset);
        VerifyCompiler(buildCommand, CoreCompilerFileName, useSharedCompilation);
    }

    [FullMSBuildOnlyTheory, CombinatorialData]
    public void FullMSBuild_SdkStyle_OptOut(bool useSharedCompilation)
    {
        var testAsset = CreateProject(useSharedCompilation).WithProjectChanges(static doc =>
        {
            doc.Root!.Element("PropertyGroup")!.Add(new XElement("RoslynUseSdkCompiler", "false"));
        });
        var buildCommand = BuildAndRunUsingMSBuild(testAsset);
        VerifyCompiler(buildCommand, FxCompilerFileName, useSharedCompilation);
    }

    [FullMSBuildOnlyTheory, CombinatorialData]
    public void FullMSBuild_NonSdkStyle(bool useSharedCompilation)
    {
        var testAsset = CreateProject(useSharedCompilation, static project =>
        {
            project.IsSdkProject = false;
            project.TargetFrameworkVersion = "v4.7.2";
        });
        var buildCommand = BuildAndRunUsingMSBuild(testAsset);
        VerifyCompiler(buildCommand, FxCompilerFileName, useSharedCompilation);
    }

    [Theory, CombinatorialData]
    public void DotNet(bool useSharedCompilation)
    {
        var testAsset = CreateProject(useSharedCompilation);
        var buildCommand = BuildAndRunUsingDotNet(testAsset);
        VerifyCompiler(buildCommand, CoreCompilerFileName, useSharedCompilation);
    }

    private TestAsset CreateProject(bool useSharedCompilation, Action<TestProject>? configure = null, [CallerMemberName] string callingMethod = "")
    {
        var project = new TestProject
        {
            Name = "App1",
            IsExe = true,
            SourceFiles =
            {
                ["Program.cs"] = """
                    class Program
                    {
                        static void Main()
                        {
                            System.Console.WriteLine(40 + 2);
                        }
                    }
                    """,
            },
        };

        // UseSharedCompilation should be the default, so set it only if false.
        if (!useSharedCompilation)
        {
            project.AdditionalProperties["UseSharedCompilation"] = "false";
        }

        configure?.Invoke(project);
        return _testAssetsManager.CreateTestProject(project, callingMethod: callingMethod);
    }

    private TestCommand BuildAndRunUsingMSBuild(TestAsset testAsset)
    {
        var buildCommand = new MSBuildCommand(testAsset, "Build");
        buildCommand.WithWorkingDirectory(testAsset.Path)
            .Execute("-bl").Should().Pass();

        Run(buildCommand.GetOutputFile());

        return buildCommand;
    }

    private TestCommand BuildAndRunUsingDotNet(TestAsset testAsset)
    {
        var buildCommand = new DotnetBuildCommand(testAsset);
        buildCommand.Execute("-bl").Should().Pass();

        Run(buildCommand.GetOutputFile());

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
}
