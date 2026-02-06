// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Basic.CompilerLog.Util;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Build.Tests;

public sealed class RoslynBuildTaskTests(ITestOutputHelper log) : SdkTest(log)
{
    private const string CoreTargetFrameworkName = ".NETCoreApp";
    private const string FxTargetFrameworkName = ".NETFramework";

    private static string CompilerFileNameWithoutExtension(Language language) => language switch
    {
        Language.CSharp => "csc",
        Language.VisualBasic => "vbc",
        _ => throw new ArgumentOutOfRangeException(paramName: nameof(language)),
    };

    private static string DotNetExecCompilerFileName(Language language) => CompilerFileNameWithoutExtension(language) + ".dll";

    private static string AppHostCompilerFileName(Language language) => CompilerFileNameWithoutExtension(language) + FileNameSuffixes.CurrentPlatform.Exe;

    [FullMSBuildOnlyTheory, CombinatorialData]
    public void FullMSBuild_SdkStyle(bool useSharedCompilation, Language language)
    {
        var testAsset = CreateProject(useSharedCompilation, language);
        var buildCommand = BuildAndRunUsingMSBuild(testAsset);
        VerifyCompiler(buildCommand, AppHostCompilerFileName(language), CoreTargetFrameworkName, useSharedCompilation);
    }

    [FullMSBuildOnlyTheory, CombinatorialData]
    public void FullMSBuild_SdkStyle_OptOut(bool useSharedCompilation, Language language)
    {
        var testAsset = CreateProject(useSharedCompilation, language).WithProjectChanges(static doc =>
        {
            doc.Root!.Element("PropertyGroup")!.Add(new XElement("RoslynCompilerType", "Framework"));
        });
        var buildCommand = BuildAndRunUsingMSBuild(testAsset);
        VerifyCompiler(buildCommand, AppHostCompilerFileName(language), FxTargetFrameworkName, useSharedCompilation);
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
        VerifyCompiler(buildCommand, AppHostCompilerFileName(language), FxTargetFrameworkName, useSharedCompilation);
    }

    [FullMSBuildOnlyTheory, CombinatorialData]
    public void FullMSBuild_SdkStyle_ToolsetPackage(bool useSharedCompilation, Language language, bool useFrameworkCompiler)
    {
        var testAsset = CreateProject(useSharedCompilation, language, AddCompilersToolsetPackage);
        ReadOnlySpan<string> args = useFrameworkCompiler ? ["-p:RoslynCompilerType=Framework"] : [];
        var buildCommand = BuildAndRunUsingMSBuild(testAsset, args);
        VerifyCompiler(buildCommand,
            useFrameworkCompiler ? AppHostCompilerFileName(language) : DotNetExecCompilerFileName(language),
            useFrameworkCompiler ? FxTargetFrameworkName : CoreTargetFrameworkName,
            useSharedCompilation, toolsetPackage: true);
    }

    [Theory, CombinatorialData]
    public void DotNet(bool useSharedCompilation, Language language)
    {
        var testAsset = CreateProject(useSharedCompilation, language);
        var buildCommand = BuildAndRunUsingDotNet(testAsset);
        VerifyCompiler(buildCommand, AppHostCompilerFileName(language), CoreTargetFrameworkName, useSharedCompilation);
    }

    //  https://github.com/dotnet/sdk/issues/49665
    [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX), CombinatorialData]
    public void DotNet_ToolsetPackage(bool useSharedCompilation, Language language)
    {
        var testAsset = CreateProject(useSharedCompilation, language, AddCompilersToolsetPackage);
        var buildCommand = BuildAndRunUsingDotNet(testAsset);
        VerifyCompiler(buildCommand, DotNetExecCompilerFileName(language), CoreTargetFrameworkName, useSharedCompilation, toolsetPackage: true);
    }

    /// <summary>
    /// SDK side test for <see href="https://github.com/dotnet/roslyn/pull/80993"/>.
    /// </summary>
    [FullMSBuildOnlyFact]
    public void UsingCscManually()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Test.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                </PropertyGroup>
                <Target Name="CustomTarget">
                    <Csc Sources="File.cs" />
                </Target>
            </Project>
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "File.cs"), """
            using System.Linq;
            System.Console.WriteLine();
            """);

        new MSBuildCommand(Log, "CustomTarget", testInstance.Path)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();
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
            TargetExtension = projExtension,
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
        return TestAssetsManager.CreateTestProject(project, callingMethod: callingMethod);
    }

    private static void AddCompilersToolsetPackage(TestProject project)
    {
        string roslynVersion = typeof(Compilation).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        Assert.False(string.IsNullOrEmpty(roslynVersion));
        project.PackageReferences.Add(new TestPackageReference("Microsoft.Net.Compilers.Toolset", roslynVersion));
    }

    private TestCommand BuildAndRunUsingMSBuild(TestAsset testAsset, params ReadOnlySpan<string> args)
    {
        var buildCommand = new MSBuildCommand(testAsset, "Build");
        buildCommand.WithWorkingDirectory(testAsset.Path)
            .Execute(["-bl", .. args]).Should().Pass();

        Run(buildCommand.GetOutputDirectory().File(testAsset.TestProject!.GetOutputFileName()));

        return buildCommand;
    }

    private TestCommand BuildAndRunUsingDotNet(TestAsset testAsset, params ReadOnlySpan<string> args)
    {
        var buildCommand = new DotnetBuildCommand(testAsset);
        buildCommand.Execute(["-bl", .. args]).Should().Pass();

        Run(buildCommand.GetOutputDirectory().File(testAsset.TestProject!.GetOutputFileName()));

        return buildCommand;
    }

    private void Run(FileInfo outputFile)
    {
        var runCommand = new RunExeCommand(Log, outputFile.FullName);
        runCommand.Execute().Should().Pass()
            .And.HaveStdOut("42");
    }

    private static void VerifyCompiler(TestCommand buildCommand, string compilerFileName, string targetFrameworkName, bool usedCompilerServer, bool toolsetPackage = false)
    {
        var binaryLogPath = Path.Join(buildCommand.WorkingDirectory, "msbuild.binlog");
        using (var reader = BinaryLogReader.Create(binaryLogPath))
        {
            var call = reader.ReadAllCompilerCalls().Should().ContainSingle().Subject;
            Path.GetFileNameWithoutExtension(call.CompilerFilePath).Should().Be(Path.GetFileNameWithoutExtension(compilerFileName));

            const string toolsetPackageName = "microsoft.net.compilers.toolset";
            if (toolsetPackage)
            {
                call.CompilerFilePath.Should().Contain(toolsetPackageName);
            }
            else
            {
                call.CompilerFilePath.Should().NotContain(toolsetPackageName);
            }

            GetTargetFramework(call.CompilerFilePath).Should().StartWith($"{targetFrameworkName},");
        }

        // Verify compiler server message.
        var compilerServerMessages = BinaryLog.ReadBuild(binaryLogPath).FindChildrenRecursive<Message>(
            static message => message.Text.StartsWith("CompilerServer:", StringComparison.Ordinal));
        compilerServerMessages.Should().ContainSingle().Which.Text.Should().StartWith(usedCompilerServer
            ? "CompilerServer: server - server processed compilation - "
            : "CompilerServer: tool - using command line tool by design");
    }

    private static string? GetTargetFramework(string dllPath)
    {
        // If `dllPath` is an apphost (unmanaged exe), we need to inspect the corresponding dll instead.
        var ext = Path.GetExtension(dllPath);
        if (ext == FileNameSuffixes.CurrentPlatform.Exe)
        {
            var fixedDllPath = Path.ChangeExtension(dllPath, ".dll");
            // If a `.dll` does not exist, the `.exe` is a netfx managed assembly and we can use it.
            if (File.Exists(fixedDllPath))
            {
                dllPath = fixedDllPath;
            }
        }
        else
        {
            Assert.Equal(".dll", ext);
        }

        var tpa = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? [];
        var resolver = new PathAssemblyResolver([.. tpa, dllPath]);
        using var mlc = new MetadataLoadContext(resolver);
        var asm = mlc.LoadFromAssemblyPath(dllPath);
        var attrFullName = typeof(TargetFrameworkAttribute).FullName;
        return asm.GetCustomAttributesData()
            .Where(a => a.AttributeType.FullName == attrFullName)
            .Select(a => a.ConstructorArguments[0].Value)
            .FirstOrDefault() as string;
    }

    public enum Language
    {
        CSharp,
        VisualBasic,
    }
}
