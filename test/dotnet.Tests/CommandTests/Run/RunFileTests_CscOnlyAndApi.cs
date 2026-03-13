// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class RunFileTests_CscOnlyAndApi(ITestOutputHelper log) : RunFileTestBase(log)
{
    [Fact]
    public void UpToDate()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("Hello v1");
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "Hello v1");

        Build(testInstance, BuildLevel.None, expectedOutput: "Hello v1");

        Build(testInstance, BuildLevel.None, expectedOutput: "Hello v1");

        // Change the source file (a rebuild is necessary).
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        Build(testInstance, BuildLevel.Csc);

        Build(testInstance, BuildLevel.None);

        // Change an unrelated source file (no rebuild necessary).
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), "test");

        Build(testInstance, BuildLevel.None);

        // Add an implicit build file (a rebuild is necessary).
        string buildPropsFile = Path.Join(testInstance.Path, "Directory.Build.props");
        File.WriteAllText(buildPropsFile, """
            <Project>
                <PropertyGroup>
                    <DefineConstants>$(DefineConstants);CUSTOM_DEFINE</DefineConstants>
                </PropertyGroup>
            </Project>
            """);

        Build(testInstance, BuildLevel.All, expectedOutput: """
            Hello from Program
            Custom define
            """);

        Build(testInstance, BuildLevel.None, expectedOutput: """
            Hello from Program
            Custom define
            """);

        // Change the implicit build file (a rebuild is necessary).
        string importedFile = Path.Join(testInstance.Path, "Settings.props");
        File.WriteAllText(importedFile, """
            <Project>
            </Project>
            """);
        File.WriteAllText(buildPropsFile, """
            <Project>
                <Import Project="Settings.props" />
            </Project>
            """);

        Build(testInstance, BuildLevel.All);

        // Change the imported build file (this is not recognized).
        File.WriteAllText(importedFile, """
            <Project>
                <PropertyGroup>
                    <DefineConstants>$(DefineConstants);CUSTOM_DEFINE</DefineConstants>
                </PropertyGroup>
            </Project>
            """);

        Build(testInstance, BuildLevel.None);

        // Force rebuild.
        Build(testInstance, BuildLevel.All, args: ["--no-cache"], expectedOutput: """
            Hello from Program
            Custom define
            """);

        // Remove an implicit build file (a rebuild is necessary).
        File.Delete(buildPropsFile);
        Build(testInstance, BuildLevel.Csc);

        // Force rebuild.
        Build(testInstance, BuildLevel.All, args: ["--no-cache"]);

        Build(testInstance, BuildLevel.None);

        // Pass argument (no rebuild necessary).
        Build(testInstance, BuildLevel.None, args: ["--", "test-arg"], expectedOutput: """
            echo args:test-arg
            Hello from Program
            """);

        // Change config (a rebuild is necessary).
        Build(testInstance, BuildLevel.All, args: ["-c", "Release"], expectedOutput: """
            Hello from Program
            Release config
            """);

        // Keep changed config (no rebuild necessary).
        Build(testInstance, BuildLevel.None, args: ["-c", "Release"], expectedOutput: """
            Hello from Program
            Release config
            """);

        // Change config back (a rebuild is necessary).
        Build(testInstance, BuildLevel.Csc);

        // Build with a failure.
        new DotnetCommand(Log, ["run", "Program.cs", "-p:LangVersion=Invalid"])
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("error CS1617"); // Invalid option 'Invalid' for /langversion.

        // A rebuild is necessary since the last build failed.
        Build(testInstance, BuildLevel.Csc);
    }

    [Fact]
    public void UpToDate_InvalidOptions()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "Program.cs", "--no-cache", "--no-build")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.CannotCombineOptions, "--no-cache", "--no-build"));
    }

    /// <summary>
    /// <see cref="UpToDate"/> optimization should see through symlinks.
    /// See <see href="https://github.com/dotnet/sdk/issues/52063"/>.
    /// </summary>
    [Fact]
    public void UpToDate_SymbolicLink()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var originalPath = Path.Join(testInstance.Path, "original.cs");
        var code = """
            #!/usr/bin/env dotnet
            Console.WriteLine("v1");
            """;
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(originalPath, code, utf8NoBom);

        var programFileName = "linked";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.CreateSymbolicLink(path: programPath, pathToTarget: originalPath);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1", programFileName: programFileName);

        Build(testInstance, BuildLevel.None, expectedOutput: "v1", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(originalPath, code, utf8NoBom);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", programFileName: programFileName);
    }

    /// <summary>
    /// Similar to <see cref="UpToDate_SymbolicLink"/> but with a chain of symlinks.
    /// </summary>
    [Fact]
    public void UpToDate_SymbolicLink2()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var originalPath = Path.Join(testInstance.Path, "original.cs");
        var code = """
            #!/usr/bin/env dotnet
            Console.WriteLine("v1");
            """;
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(originalPath, code, utf8NoBom);

        var intermediateFileName = "linked1";
        var intermediatePath = Path.Join(testInstance.Path, intermediateFileName);

        File.CreateSymbolicLink(path: intermediatePath, pathToTarget: originalPath);

        var programFileName = "linked2";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.CreateSymbolicLink(path: programPath, pathToTarget: intermediatePath);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1", programFileName: programFileName);

        Build(testInstance, BuildLevel.None, expectedOutput: "v1", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(originalPath, code, utf8NoBom);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", programFileName: programFileName);
    }

    /// <summary>
    /// <see cref="UpToDate"/> optimization currently does not support <c>#:project</c> references and hence is disabled if those are present.
    /// See <see href="https://github.com/dotnet/sdk/issues/52057"/>.
    /// </summary>
    [Fact]
    public void UpToDate_ProjectReferences()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var libDir = Path.Join(testInstance.Path, "Lib");
        Directory.CreateDirectory(libDir);

        File.WriteAllText(Path.Join(libDir, "Lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var libPath = Path.Join(libDir, "Lib.cs");
        var libCode = """
            namespace Lib;
            public class LibClass
            {
                public static string GetMessage() => "Hello from Lib v1";
            }
            """;
        File.WriteAllText(libPath, libCode);

        var appDir = Path.Join(testInstance.Path, "App");
        Directory.CreateDirectory(appDir);

        var code = """
            #:project ../Lib
            Console.WriteLine("v1 " + Lib.LibClass.GetMessage());
            """;

        var programPath = Path.Join(appDir, "Program.cs");
        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var programFileName = "App/Program.cs";

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Hello from Lib v1", programFileName: programFileName);

        // We cannot detect changes in referenced projects, so we always rebuild.
        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Hello from Lib v1", programFileName: programFileName);

        libCode = libCode.Replace("v1", "v2");
        File.WriteAllText(libPath, libCode);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Hello from Lib v2", programFileName: programFileName);
    }

    /// <summary>
    /// <see cref="UpToDate"/> optimization considers default items.
    /// Also tests <see cref="CscOnly_AfterMSBuild"/> optimization.
    /// (We cannot test <see cref="CscOnly"/> because that optimization doesn't support neither <c>#:property</c> nor <c>#:sdk</c> which we need to enable default items.)
    /// See <see href="https://github.com/dotnet/sdk/issues/50912"/>.
    /// </summary>
    [Theory, CombinatorialData]
    public void UpToDate_DefaultItems(bool optOut)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var code = $"""
            {(optOut ? "#:property FileBasedProgramCanSkipMSBuild=false" : "")}
            #:property EnableDefaultEmbeddedResourceItems=true
            {s_programReadingEmbeddedResource}
            """;
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), code);

        Build(testInstance, BuildLevel.All, expectedOutput: "Resource not found");

        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx);

        if (!optOut)
        {
            // Adding a default item is currently not recognized (https://github.com/dotnet/sdk/issues/50912).
            Build(testInstance, BuildLevel.None, expectedOutput: "Resource not found");
            Build(testInstance, BuildLevel.All, args: ["--no-cache"], expectedOutput: "[MyString, TestValue]");
        }
        else
        {
            Build(testInstance, BuildLevel.All, expectedOutput: "[MyString, TestValue]");
        }

        // Update the RESX file.
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx.Replace("TestValue", "UpdatedValue"));

        Build(testInstance, BuildLevel.All, expectedOutput: "[MyString, UpdatedValue]");

        // Update the C# file.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), "//v2\n" + code);

        Build(testInstance, optOut ? BuildLevel.All : BuildLevel.Csc, expectedOutput: "[MyString, UpdatedValue]");

        // Update the RESX file again (to verify the CSC only compilation didn't corrupt the list of additional files in the cache).
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx.Replace("TestValue", "UpdatedValue2"));

        Build(testInstance, BuildLevel.All, expectedOutput: "[MyString, UpdatedValue2]");
    }

    /// <summary>
    /// Similar to <see cref="UpToDate_DefaultItems"/> but for <c>.razor</c> files instead of <c>.resx</c> files.
    /// </summary>
    [Fact]
    public void UpToDate_DefaultItems_Razor()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var programFileName = "MyRazorApp.cs";
        File.WriteAllText(Path.Join(testInstance.Path, programFileName), """
            #:sdk Microsoft.NET.Sdk.Web
            _ = new MyRazorApp.MyCoolApp();
            Console.WriteLine("Hello from Program");
            """);

        var razorFilePath = Path.Join(testInstance.Path, "MyCoolApp.razor");
        File.WriteAllText(razorFilePath, "");

        Build(testInstance, BuildLevel.All, programFileName: programFileName);

        Build(testInstance, BuildLevel.None, programFileName: programFileName);

        File.Delete(razorFilePath);

        new DotnetCommand(Log, "run", programFileName)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // error CS0246: The type or namespace name 'MyRazorApp' could not be found
            .And.HaveStdOutContaining("error CS0246");
    }

    [Fact]
    public void CscOnly()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("v1");
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v1");

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("v2");
            #if !DEBUG
            Console.WriteLine("Release config");
            #endif
            """);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2");

        // Customizing a property forces MSBuild to be used.
        Build(testInstance, BuildLevel.All, args: ["-c", "Release"], expectedOutput: """
            v2
            Release config
            """);
    }

    [Fact]
    public void CscOnly_CompilationDiagnostics()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            string x = null;
            Console.WriteLine("ran" + x);
            """);

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            // warning CS8600: Converting null literal or possible null value to non-nullable type.
            .And.HaveStdOutContaining("warning CS8600")
            .And.HaveStdOutContaining("ran");

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.Write
            """);

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            // error CS1002: ; expected
            .And.HaveStdOutContaining("error CS1002")
            .And.HaveStdErrContaining(CliCommandStrings.RunCommandException);
    }

    /// <summary>
    /// Checks that the <c>DOTNET_ROOT</c> env var is set the same in csc mode as in msbuild mode.
    /// </summary>
    [Fact]
    public void CscOnly_DotNetRoot()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            foreach (var entry in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process)
                .Cast<System.Collections.DictionaryEntry>()
                .Where(e => ((string)e.Key).StartsWith("DOTNET_ROOT")))
            {
                Console.WriteLine($"{entry.Key}={entry.Value}");
            }
            """);

        var expectedDotNetRoot = SdkTestContext.Current.ToolsetUnderTest.DotNetRoot;

        var cscResult = new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        cscResult.Should().Pass()
            .And.HaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            .And.HaveStdOutContaining("DOTNET_ROOT")
            .And.HaveStdOutContaining($"={expectedDotNetRoot}");

        // Add an implicit build file to force use of msbuild instead of csc.
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), "<Project />");

        var msbuildResult = new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        msbuildResult.Should().Pass()
            .And.NotHaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            .And.HaveStdOutContaining("DOTNET_ROOT")
            .And.HaveStdOutContaining($"={expectedDotNetRoot}");

        // The set of DOTNET_ROOT env vars should be the same in both cases.
        var cscVars = cscResult.StdOut!
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("DOTNET_ROOT"));
        var msbuildVars = msbuildResult.StdOut!
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("DOTNET_ROOT"));
        cscVars.Should().BeEquivalentTo(msbuildVars);
    }

    /// <summary>
    /// In CSC-only mode, the SDK needs to manually create intermediate files
    /// like GlobalUsings.g.cs which are normally generated by MSBuild targets.
    /// This tests the SDK recreates the files when they are outdated.
    /// </summary>
    [Fact]
    public void CscOnly_IntermediateFiles()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Expression<Func<int>> e = () => 1 + 1;
            Console.WriteLine(e);
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), "<Project />");

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // error CS0246: The type or namespace name 'Expression<>' could not be found
            .And.HaveStdOutContaining("error CS0246");

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
                <ItemGroup>
                    <Using Include="System.Linq.Expressions" />
                </ItemGroup>
            </Project>
            """);

        Build(testInstance, BuildLevel.All, expectedOutput: "() => 2");

        File.Delete(Path.Join(testInstance.Path, "Directory.Build.props"));

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            // error CS0246: The type or namespace name 'Expression<>' could not be found
            .And.HaveStdOutContaining("error CS0246");
    }

    /// <summary>
    /// If a file from a NuGet package (which would be used by CSC-only build) does not exist, full MSBuild should be used instead.
    /// </summary>
    [Fact]
    public void CscOnly_NotRestored()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "run", "Program.cs", "-bl", "--no-restore")
            .WithEnvironmentVariable("NUGET_PACKAGES", Path.Join(testInstance.Path, "packages"))
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // error NETSDK1004: Assets file '...\obj\project.assets.json' not found. Run a NuGet package restore to generate this file.
            .And.HaveStdOutContaining("NETSDK1004");

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithEnvironmentVariable("NUGET_PACKAGES", Path.Join(testInstance.Path, "packages"))
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("v2");
            """);

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithEnvironmentVariable("NUGET_PACKAGES", Path.Join(testInstance.Path, "packages"))
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"""
                {CliCommandStrings.NoBinaryLogBecauseRunningJustCsc}
                v2
                """);
    }

    [Fact]
    public void CscOnly_SpacesInPath()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var programFileName = "Program with spaces.cs";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.WriteAllText(programPath, """
            Console.WriteLine("v1");
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v1", programFileName: programFileName);
    }

    [Fact] // https://github.com/dotnet/sdk/issues/50778
    public void CscOnly_Args()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.Csc, args: ["test", "args"], expectedOutput: """
            echo args:test;args
            Hello from Program
            """);
    }

    /// <summary>
    /// Combination of <see cref="UpToDate_SymbolicLink"/> and <see cref="CscOnly"/>.
    /// </summary>
    [Fact]
    public void CscOnly_SymbolicLink()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var originalPath = Path.Join(testInstance.Path, "original.cs");
        var code = """
            #!/usr/bin/env dotnet
            Console.WriteLine("v1");
            """;
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(originalPath, code, utf8NoBom);

        var programFileName = "linked";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.CreateSymbolicLink(path: programPath, pathToTarget: originalPath);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v1", programFileName: programFileName);

        Build(testInstance, BuildLevel.None, expectedOutput: "v1", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(originalPath, code, utf8NoBom);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", programFileName: programFileName);
    }

    /// <summary>
    /// Tests an optimization which remembers CSC args from prior MSBuild runs and can skip subsequent MSBuild invocations and call CSC directly.
    /// This optimization kicks in when the file has some <c>#:</c> directives (then the simpler "hard-coded CSC args" optimization cannot be used).
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var code = """
            #:property Configuration=Release
            Console.Write("v1 ");
            #if !DEBUG
            Console.Write("Release");
            #endif
            """;

        var programPath = Path.Join(testInstance.Path, "Program.cs");

        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Release");

        Build(testInstance, BuildLevel.None, expectedOutput: "v1 Release");

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2 Release");

        code = code.Replace("v2", "v3");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v3 Release");

        // Customizing a property forces MSBuild to be used.
        code = code.Replace("Configuration=Release", "Configuration=Debug");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.All, expectedOutput: "v3 ");

        // This MSBuild will skip CoreBuild but we still need to preserve CSC args so the next build can be CSC-only.
        Build(testInstance, BuildLevel.All, ["--no-cache"], expectedOutput: "v3 ");

        code = code.Replace("v3", "v4");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v4 ");

        // Customizing a property on the command-line forces MSBuild to be used.
        Build(testInstance, BuildLevel.All, args: ["-c", "Release"], expectedOutput: "v4 Release");

        Build(testInstance, BuildLevel.All, expectedOutput: "v4 ");
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_SpacesInPath()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var code = """
            #:property Configuration=Release
            Console.Write("v1 ");
            #if !DEBUG
            Console.Write("Release");
            #endif
            """;

        var programFileName = "Program with spaces.cs";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Release", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2 Release", programFileName: programFileName);
    }

    /// <summary>
    /// Testing optimization <see cref="CscOnly_AfterMSBuild"/>.
    /// When compilation fails, the obj dll should not be copied to bin directory.
    /// This prevents spurious errors if the dll file was not even produced by roslyn due to compilation errors.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_CompilationFailure_NoCopyToBin()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        // First, create a valid program and build it successfully
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        var code = """
            #:property PublishAot=false
            Console.WriteLine("version 1");
            """;
        File.WriteAllText(programPath, code);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "version 1");

        // Verify that the dlls were created
        var objDll = Path.Join(artifactsDir, "obj", "debug", "Program.dll");
        new FileInfo(objDll).Should().Exist();
        var binDll = Path.Join(artifactsDir, "bin", "debug", "Program.dll");
        new FileInfo(binDll).Should().Exist();

        // Delete the dlls
        File.Delete(objDll);
        File.Delete(binDll);

        // Write invalid code that causes compilation to fail
        code = code + "\n#error my custom error";
        File.WriteAllText(programPath, code);

        // Try to build the invalid code
        new DotnetCommand(Log, "run", "-bl", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            .And.HaveStdOutContaining("my custom error")
            .And.HaveStdErrContaining(CliCommandStrings.RunCommandException);

        new FileInfo(objDll).Should().NotExist();
        new FileInfo(binDll).Should().NotExist();
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_Args()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        var programPath = Path.Join(testInstance.Path, "Program.cs");

        var code = $"""
            #:property Configuration=Release
            {s_program}
            """;

        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, args: ["test", "args"], expectedOutput: """
            echo args:test;args
            Hello from Program
            Release config
            """);

        code = code.Replace("Hello", "Hi");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, args: ["test", "args"], expectedOutput: """
            echo args:test;args
            Hi from Program
            Release config
            """);
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// If hard links are enabled, the <c>bin/app.dll</c> and <c>obj/app.dll</c> files are going to be the same,
    /// so our "copy obj to bin" logic must account for that.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_HardLinks()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        var programPath = Path.Join(testInstance.Path, "Program.cs");

        var code = $"""
            #:property CreateHardLinksForCopyFilesToOutputDirectoryIfPossible=true
            #:property CreateSymbolicLinksForCopyFilesToOutputDirectoryIfPossible=true
            {s_program}
            """;

        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All);

        code = code.Replace("Hello", "Hi");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "Hi from Program");
    }

    /// <summary>
    /// Combination of <see cref="UpToDate_SymbolicLink"/> and <see cref="CscOnly_AfterMSBuild"/>.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_SymbolicLink()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var originalPath = Path.Join(testInstance.Path, "original.cs");
        var code = """
            #!/usr/bin/env dotnet
            #:property Configuration=Release
            Console.WriteLine("v1");
            """;
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(originalPath, code, utf8NoBom);

        var programFileName = "linked";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.CreateSymbolicLink(path: programPath, pathToTarget: originalPath);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(originalPath, code, utf8NoBom);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", programFileName: programFileName);
    }

    /// <summary>
    /// Interaction of optimization <see cref="CscOnly_AfterMSBuild"/> and <c>Directory.Build.props</c> file.
    /// </summary>
    [Theory, CombinatorialData]
    public void CscOnly_AfterMSBuild_DirectoryBuildProps(bool touch1, bool touch2)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var propsPath = Path.Join(testInstance.Path, "Directory.Build.props");
        var propsContent = """
            <Project>
                <PropertyGroup>
                    <AssemblyName>CustomAssemblyName</AssemblyName>
                </PropertyGroup>
            </Project>
            """;
        File.WriteAllText(propsPath, propsContent);

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        var programVersion = 0;
        void WriteProgramContent()
        {
            programVersion++;

            // #: directive ensures we get CscOnly_AfterMSBuild optimization instead of CscOnly.
            File.WriteAllText(programPath, $"""
                #:property Configuration=Debug
                Console.WriteLine("v{programVersion} " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
                """);
        }
        WriteProgramContent();

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: $"v{programVersion} CustomAssemblyName");

        File.Delete(propsPath);

        if (touch1) WriteProgramContent();

        Build(testInstance, BuildLevel.All, expectedOutput: $"v{programVersion} Program");

        File.WriteAllText(propsPath, propsContent);

        if (touch2) WriteProgramContent();

        Build(testInstance, BuildLevel.All, expectedOutput: $"v{programVersion} CustomAssemblyName");
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// This optimization currently does not support <c>#:project</c> references and hence is disabled if those are present.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_ProjectReferences()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var libDir = Path.Join(testInstance.Path, "Lib");
        Directory.CreateDirectory(libDir);

        File.WriteAllText(Path.Join(libDir, "Lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var libPath = Path.Join(libDir, "Lib.cs");
        var libCode = """
            namespace Lib;
            public class LibClass
            {
                public static string GetMessage() => "Hello from Lib v1";
            }
            """;
        File.WriteAllText(libPath, libCode);

        var appDir = Path.Join(testInstance.Path, "App");
        Directory.CreateDirectory(appDir);

        var code = """
            #:project ../Lib
            Console.WriteLine("v1 " + Lib.LibClass.GetMessage());
            """;

        var programPath = Path.Join(appDir, "Program.cs");
        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var programFileName = "App/Program.cs";

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Hello from Lib v1", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        libCode = libCode.Replace("v1", "v2");
        File.WriteAllText(libPath, libCode);

        // Cannot use CSC because we cannot detect updates in the referenced project.
        Build(testInstance, BuildLevel.All, expectedOutput: "v2 Hello from Lib v2", programFileName: programFileName);
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// If users have more complex build customizations, they can opt out of the optimization.
    /// </summary>
    [Theory, CombinatorialData]
    public void CscOnly_AfterMSBuild_OptOut(bool canSkipMSBuild, bool inDirectoryBuildProps)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        const string propertyName = VirtualProjectBuildingCommand.FileBasedProgramCanSkipMSBuild;

        if (inDirectoryBuildProps)
        {
            File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
                <Project>
                  <PropertyGroup>
                    <{propertyName}>{canSkipMSBuild}</{propertyName}>
                  </PropertyGroup>
                </Project>
                """);
        }

        var code = $"""
            #:property Configuration=Release
            {(inDirectoryBuildProps ? "" : $"#:property {propertyName}={canSkipMSBuild}")}
            Console.Write("v1 ");
            #if !DEBUG
            Console.Write("Release");
            #endif
            """;

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Release");

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance, canSkipMSBuild ? BuildLevel.Csc : BuildLevel.All, expectedOutput: "v2 Release");
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_AuxiliaryFilesNotReused()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var code = """
            #:property Configuration=Release
            Console.Write("v1 ");
            #if !DEBUG
            Console.Write("Release");
            #endif
            """;

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Release");

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        // Reusing CSC args from previous run here.
        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2 Release");

        code = code.Replace("v2", "v3");
        code = code.Replace("#:property Configuration=Release", "");
        File.WriteAllText(programPath, code);

        // Using built-in CSC args here (cannot reuse auxiliary files like csc.rsp here).
        Build(testInstance, BuildLevel.Csc, expectedOutput: "v3 ");
    }

    /// <summary>
    /// Verifies that <c>csc.rsp</c> is written to disk after a full MSBuild build,
    /// so that IDEs can read it to create a virtual project.
    /// </summary>
    [Fact]
    public void MSBuild_WritesCscRsp()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, """
            #:property Configuration=Release
            Console.Write("Hello");
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        // A build directive forces a full MSBuild build.
        Build(testInstance, BuildLevel.All, expectedOutput: "Hello");

        // csc.rsp should be written to disk after a full MSBuild build.
        var rspPath = Path.Join(artifactsDir, "csc.rsp");
        File.Exists(rspPath).Should().BeTrue("csc.rsp should be written after a full MSBuild build");
        File.ReadAllLines(rspPath).Should().NotBeEmpty("csc.rsp should contain compiler arguments");
    }

    /// <summary>
    /// Verifies that <c>csc.rsp</c> is written to disk after <c>dotnet build file.cs</c>,
    /// so that IDEs can read it to create a virtual project.
    /// </summary>
    [Fact]
    public void DotnetBuild_WritesCscRsp()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, """
            Console.Write("Hello");
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // csc.rsp should be written to disk after dotnet build.
        var rspPath = Path.Join(artifactsDir, "csc.rsp");
        File.Exists(rspPath).Should().BeTrue("csc.rsp should be written after dotnet build file.cs");
        File.ReadAllLines(rspPath).Should().NotBeEmpty("csc.rsp should contain compiler arguments");
    }

    /// <summary>
    /// Testing <see cref="CscOnly"/> optimization when the NuGet cache is cleared between builds.
    /// See <see href="https://github.com/dotnet/sdk/issues/45169"/>.
    /// </summary>
    [Fact]
    public void CscOnly_NuGetCacheCleared()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var code = """
            Console.Write("v1");
            """;

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, code);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var packageDir = Path.Join(testInstance.Path, "packages");
        TestCommand CustomizeCommand(TestCommand command) => command.WithEnvironmentVariable("NUGET_PACKAGES", packageDir);

        Assert.False(Directory.Exists(packageDir));

        // Ensure the packages exist first.
        Build(testInstance, BuildLevel.All, expectedOutput: "v1", customizeCommand: CustomizeCommand);

        Assert.True(Directory.Exists(packageDir));

        // Now clear the build outputs (but not packages) to verify CSC is used even from "first run".
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", customizeCommand: CustomizeCommand);

        code = code.Replace("v2", "v3");
        File.WriteAllText(programPath, code);

        // Clear NuGet cache.
        Directory.Delete(packageDir, recursive: true);
        Assert.False(Directory.Exists(packageDir));

        Build(testInstance, BuildLevel.All, expectedOutput: "v3", customizeCommand: CustomizeCommand);

        Assert.True(Directory.Exists(packageDir));
    }

    /// <summary>
    /// Combination of <see cref="CscOnly_NuGetCacheCleared"/> and <see cref="CscOnly_AfterMSBuild"/>.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_NuGetCacheCleared()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var code = """
            #:property PublishAot=false
            #:package System.CommandLine@2.0.0-beta4.22272.1
            new System.CommandLine.RootCommand("v1");
            Console.WriteLine("v1");
            """;

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, code);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var packageDir = Path.Join(testInstance.Path, "packages");
        TestCommand CustomizeCommand(TestCommand command) => command.WithEnvironmentVariable("NUGET_PACKAGES", packageDir);

        Assert.False(Directory.Exists(packageDir));

        Build(testInstance, BuildLevel.All, expectedOutput: "v1", customizeCommand: CustomizeCommand);

        Assert.True(Directory.Exists(packageDir));

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", customizeCommand: CustomizeCommand);

        code = code.Replace("v2", "v3");
        File.WriteAllText(programPath, code);

        // Clear NuGet cache.
        Directory.Delete(packageDir, recursive: true);
        Assert.False(Directory.Exists(packageDir));

        Build(testInstance, BuildLevel.All, expectedOutput: "v3", customizeCommand: CustomizeCommand);

        Assert.True(Directory.Exists(packageDir));
    }

    private static string ToJson(string s) => JsonSerializer.Serialize(s);

    /// <summary>
    /// Simplifies using interpolated raw strings with nested JSON,
    /// e.g, in <c>$$"""{x:{y:1}}"""</c>, the <c>}}</c> would result in an error.
    /// </summary>
    private const string nop = "";

    [Fact]
    public void Api()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, """
            #!/program
            #:sdk Microsoft.NET.Sdk
            #:sdk Aspire.AppHost.Sdk@9.1.0
            #:property TargetFramework=net5.0
            #:package System.CommandLine@2.0.0-beta4.22272.1
            #:property LangVersion=preview
            Console.WriteLine();
            """);

        new DotnetCommand(Log, "run-api")
            .WithStandardInput($$"""
                {"$type":"GetProject","EntryPointFileFullPath":{{ToJson(programPath)}},"ArtifactsPath":"/artifacts"}
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($$"""
                {"$type":"Project","Version":1,"Content":{{ToJson($"""
                    <Project>

                      <PropertyGroup>
                        <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>
                        <ArtifactsPath>/artifacts</ArtifactsPath>
                        <PublishDir>artifacts/$(MSBuildProjectName)</PublishDir>
                        <PackageOutputPath>artifacts/$(MSBuildProjectName)</PackageOutputPath>
                        <FileBasedProgram>true</FileBasedProgram>
                        <FileBasedProgramsItemMapping>.cs=Compile;.resx=EmbeddedResource;.json=None;.razor=Content</FileBasedProgramsItemMapping>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                        <DisableDefaultItemsInProjectFolder>true</DisableDefaultItemsInProjectFolder>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                        <PackAsTool>true</PackAsTool>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
                      <Import Project="Sdk.props" Sdk="Aspire.AppHost.Sdk" Version="9.1.0" />

                      <PropertyGroup>
                        <TargetFramework>net5.0</TargetFramework>
                        <LangVersion>preview</LangVersion>
                        <RestoreUseStaticGraphEvaluation>false</RestoreUseStaticGraphEvaluation>
                        <Features>$(Features);FileBasedProgram</Features>
                      </PropertyGroup>

                      <ItemGroup>
                        <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
                      </ItemGroup>

                      <ItemGroup>
                        <Compile Include="{programPath}" Exclude="@(Compile)" />
                      </ItemGroup>

                      <ItemGroup>
                        <RuntimeHostConfigurationOption Include="EntryPointFilePath" Value="{programPath}" />
                        <RuntimeHostConfigurationOption Include="EntryPointFileDirectoryPath" Value="{testInstance.Path}" />
                      </ItemGroup>

                      <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
                      <Import Project="Sdk.targets" Sdk="Aspire.AppHost.Sdk" Version="9.1.0" />

                    </Project>

                    """)}},"Diagnostics":[]}
                """);
    }

    /// <summary>
    /// Directives should be evaluated before the project for run-api is constructed.
    /// </summary>
    [Fact]
    public void Api_Evaluation()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableIncludeDirective>true</ExperimentalFileBasedProgramEnableIncludeDirective>
              </PropertyGroup>
            </Project>
            """);

        var programPath = Path.Join(testInstance.Path, "A.cs");
        File.WriteAllText(programPath, """
            #:property P1=cs
            #:include B.$(P1)
            Console.WriteLine();
            """);

        var bPath = Path.Join(testInstance.Path, "B.cs");
        File.WriteAllText(bPath, "");

        new DotnetCommand(Log, "run-api")
            .WithStandardInput($$"""
                {"$type":"GetProject","EntryPointFileFullPath":{{ToJson(programPath)}},"ArtifactsPath":"/artifacts"}
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($$"""
                {"$type":"Project","Version":1,"Content":{{ToJson($"""
                    <Project>

                      <PropertyGroup>
                        <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>
                        <ArtifactsPath>/artifacts</ArtifactsPath>
                        <PublishDir>artifacts/$(MSBuildProjectName)</PublishDir>
                        <PackageOutputPath>artifacts/$(MSBuildProjectName)</PackageOutputPath>
                        <FileBasedProgram>true</FileBasedProgram>
                        <FileBasedProgramsItemMapping>.cs=Compile;.resx=EmbeddedResource;.json=None;.razor=Content</FileBasedProgramsItemMapping>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                        <DisableDefaultItemsInProjectFolder>true</DisableDefaultItemsInProjectFolder>
                        <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>
                        <EnableDefaultNoneItems>false</EnableDefaultNoneItems>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                        <PackAsTool>true</PackAsTool>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

                      <PropertyGroup>
                        <P1>cs</P1>
                        <RestoreUseStaticGraphEvaluation>false</RestoreUseStaticGraphEvaluation>
                        <Features>$(Features);FileBasedProgram</Features>
                      </PropertyGroup>

                      <ItemGroup>
                        <Compile Include="{bPath}" />
                      </ItemGroup>

                      <ItemGroup>
                        <Compile Include="{programPath}" Exclude="@(Compile)" />
                      </ItemGroup>

                      <ItemGroup>
                        <RuntimeHostConfigurationOption Include="EntryPointFilePath" Value="{programPath}" />
                        <RuntimeHostConfigurationOption Include="EntryPointFileDirectoryPath" Value="{testInstance.Path}" />
                      </ItemGroup>

                      <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />

                    </Project>

                    """)}},"Diagnostics":[]}
                """);
    }

    [Fact]
    public void Api_Diagnostic_01()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, """
            Console.WriteLine();
            #:property LangVersion=preview
            """);

        new DotnetCommand(Log, "run-api")
            .WithStandardInput($$"""
                {"$type":"GetProject","EntryPointFileFullPath":{{ToJson(programPath)}},"ArtifactsPath":"/artifacts"}
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($$"""
                {"$type":"Project","Version":1,"Content":{{ToJson($"""
                    <Project>

                      <PropertyGroup>
                        <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>
                        <ArtifactsPath>/artifacts</ArtifactsPath>
                        <PublishDir>artifacts/$(MSBuildProjectName)</PublishDir>
                        <PackageOutputPath>artifacts/$(MSBuildProjectName)</PackageOutputPath>
                        <FileBasedProgram>true</FileBasedProgram>
                        <FileBasedProgramsItemMapping>.cs=Compile;.resx=EmbeddedResource;.json=None;.razor=Content</FileBasedProgramsItemMapping>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                        <DisableDefaultItemsInProjectFolder>true</DisableDefaultItemsInProjectFolder>
                        <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>
                        <EnableDefaultNoneItems>false</EnableDefaultNoneItems>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                        <PackAsTool>true</PackAsTool>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

                      <PropertyGroup>
                        <RestoreUseStaticGraphEvaluation>false</RestoreUseStaticGraphEvaluation>
                        <Features>$(Features);FileBasedProgram</Features>
                      </PropertyGroup>

                      <ItemGroup>
                        <Compile Include="{programPath}" Exclude="@(Compile)" />
                      </ItemGroup>

                      <ItemGroup>
                        <RuntimeHostConfigurationOption Include="EntryPointFilePath" Value="{programPath}" />
                        <RuntimeHostConfigurationOption Include="EntryPointFileDirectoryPath" Value="{testInstance.Path}" />
                      </ItemGroup>

                      <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />

                    </Project>

                    """)}},"Diagnostics":
                [{"Location":{
                "Path":{{ToJson(programPath)}},
                "Span":{"Start":{"Line":1,"Character":0},"End":{"Line":1,"Character":30}{{nop}}}{{nop}}},
                "Message":{{ToJson(FileBasedProgramsResources.CannotConvertDirective)}}}]}
                """.ReplaceLineEndings(""));
    }

    [Fact]
    public void Api_Diagnostic_02()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, """
            #:unknown directive
            Console.WriteLine();
            """);

        new DotnetCommand(Log, "run-api")
            .WithStandardInput($$"""
                {"$type":"GetProject","EntryPointFileFullPath":{{ToJson(programPath)}},"ArtifactsPath":"/artifacts"}
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($$"""
                {"$type":"Project","Version":1,"Content":{{ToJson($"""
                    <Project>

                      <PropertyGroup>
                        <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>
                        <ArtifactsPath>/artifacts</ArtifactsPath>
                        <PublishDir>artifacts/$(MSBuildProjectName)</PublishDir>
                        <PackageOutputPath>artifacts/$(MSBuildProjectName)</PackageOutputPath>
                        <FileBasedProgram>true</FileBasedProgram>
                        <FileBasedProgramsItemMapping>.cs=Compile;.resx=EmbeddedResource;.json=None;.razor=Content</FileBasedProgramsItemMapping>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                        <DisableDefaultItemsInProjectFolder>true</DisableDefaultItemsInProjectFolder>
                        <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>
                        <EnableDefaultNoneItems>false</EnableDefaultNoneItems>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                        <PackAsTool>true</PackAsTool>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

                      <PropertyGroup>
                        <RestoreUseStaticGraphEvaluation>false</RestoreUseStaticGraphEvaluation>
                        <Features>$(Features);FileBasedProgram</Features>
                      </PropertyGroup>

                      <ItemGroup>
                        <Compile Include="{programPath}" Exclude="@(Compile)" />
                      </ItemGroup>

                      <ItemGroup>
                        <RuntimeHostConfigurationOption Include="EntryPointFilePath" Value="{programPath}" />
                        <RuntimeHostConfigurationOption Include="EntryPointFileDirectoryPath" Value="{testInstance.Path}" />
                      </ItemGroup>

                      <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />

                    </Project>

                    """)}},"Diagnostics":
                [{"Location":{
                "Path":{{ToJson(programPath)}},
                "Span":{"Start":{"Line":0,"Character":0},"End":{"Line":1,"Character":0}{{nop}}}{{nop}}},
                "Message":{{ToJson(string.Format(FileBasedProgramsResources.UnrecognizedDirective, "unknown"))}}}]}
                """.ReplaceLineEndings(""));
    }

    [Fact]
    public void Api_Error()
    {
        new DotnetCommand(Log, "run-api")
            .WithStandardInput("""
                {"$type":"Unknown1"}
                {"$type":"Unknown2"}
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("""
                {"$type":"Error","Version":1,"Message":
                """)
            .And.HaveStdOutContaining("Unknown1")
            .And.HaveStdOutContaining("Unknown2");
    }

    [Fact]
    public void Api_RunCommand()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, """
            Console.WriteLine();
            """);

        string artifactsPath = OperatingSystem.IsWindows() ? @"C:\artifacts" : "/artifacts";
        string executablePath = OperatingSystem.IsWindows() ? @"C:\artifacts\bin\debug\Program.exe" : "/artifacts/bin/debug/Program";
        new DotnetCommand(Log, "run-api")
            // The command outputs only _custom_ environment variables (not inherited ones),
            // so make sure we don't pass DOTNET_ROOT_* so we can assert that it is set by the run command.
            .WithEnvironmentVariable("DOTNET_ROOT", string.Empty)
            .WithEnvironmentVariable($"DOTNET_ROOT_{RuntimeInformation.OSArchitecture.ToString().ToUpperInvariant()}", string.Empty)
            .WithStandardInput($$"""
                {"$type":"GetRunCommand","EntryPointFileFullPath":{{ToJson(programPath)}},"ArtifactsPath":{{ToJson(artifactsPath)}}}
                """)
            .Execute()
            .Should().Pass()
            // DOTNET_ROOT environment variable is platform dependent so we don't verify it fully for simplicity
            .And.HaveStdOutContaining($$"""
                {"$type":"RunCommand","Version":1,"ExecutablePath":{{ToJson(executablePath)}},"CommandLineArguments":"","WorkingDirectory":"","EnvironmentVariables":{"DOTNET_ROOT
                """);
    }

    [Theory, CombinatorialData]
    public void EntryPointFilePath(bool cscOnly)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: cscOnly ? OutOfTreeBaseDirectory : null);
        var filePath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(filePath, """"
            var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
            Console.WriteLine($"""EntryPointFilePath: {entryPointFilePath}""");
            """");

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(filePath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var prefix = cscOnly
            ? CliCommandStrings.NoBinaryLogBecauseRunningJustCsc + Environment.NewLine
            : string.Empty;

        new DotnetCommand(Log, "run", "-bl", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(prefix + $"EntryPointFilePath: {filePath}");
    }

    [Fact]
    public void EntryPointFileDirectoryPath()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """"
            var entryPointFileDirectoryPath = AppContext.GetData("EntryPointFileDirectoryPath") as string;
            Console.WriteLine($"""EntryPointFileDirectoryPath: {entryPointFileDirectoryPath}""");
            """");

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFileDirectoryPath: {testInstance.Path}");
    }

    [Fact]
    public void EntryPointFilePath_WithRelativePath()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var fileName = "Program.cs";
        File.WriteAllText(Path.Join(testInstance.Path, fileName), """
            var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
            Console.WriteLine($"EntryPointFilePath: {entryPointFilePath}");
            """);

        var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Join(testInstance.Path, fileName));
        new DotnetCommand(Log, "run", relativePath)
            .WithWorkingDirectory(Directory.GetCurrentDirectory())
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFilePath: {Path.GetFullPath(relativePath)}");
    }

    [Fact]
    public void EntryPointFilePath_WithSpacesInPath()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var dirWithSpaces = Path.Join(testInstance.Path, "dir with spaces");
        Directory.CreateDirectory(dirWithSpaces);
        var filePath = Path.Join(dirWithSpaces, "Program.cs");
        File.WriteAllText(filePath, """
        var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
        Console.WriteLine($"EntryPointFilePath: {entryPointFilePath}");
        """);

        new DotnetCommand(Log, "run", filePath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFilePath: {filePath}");
    }

    [Fact]
    public void EntryPointFileDirectoryPath_WithDotSlash()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var fileName = "Program.cs";
        File.WriteAllText(Path.Join(testInstance.Path, fileName), """
        var entryPointFileDirectoryPath = AppContext.GetData("EntryPointFileDirectoryPath") as string;
        Console.WriteLine($"EntryPointFileDirectoryPath: {entryPointFileDirectoryPath}");
        """);

        new DotnetCommand(Log, "run", $"./{fileName}")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFileDirectoryPath: {testInstance.Path}");
    }

    [Fact]
    public void EntryPointFilePath_WithUnicodeCharacters()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var unicodeFileName = "Программа.cs";
        var filePath = Path.Join(testInstance.Path, unicodeFileName);
        File.WriteAllText(filePath, """
        var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
        Console.WriteLine($"EntryPointFilePath: {entryPointFilePath}");
        """);

        new DotnetCommand(Log, "run", unicodeFileName)
            .WithWorkingDirectory(testInstance.Path)
            .WithStandardOutputEncoding(Encoding.UTF8)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFilePath: {filePath}");
    }

    [Fact]
    public void EntryPointFilePath_SymbolicLink()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var fileName = "Program.cs";
        var programPath = Path.Join(testInstance.Path, fileName);
        File.WriteAllText(programPath, """
            #!/usr/bin/env dotnet
            var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
            Console.WriteLine($"EntryPointFilePath: {entryPointFilePath}");
            """);

        new DotnetCommand(Log, "run", fileName)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFilePath: {programPath}");

        var linkName = "linked";
        var linkPath = Path.Join(testInstance.Path, linkName);
        File.CreateSymbolicLink(linkPath, programPath);

        new DotnetCommand(Log, "run", linkName)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFilePath: {linkPath}");
    }

    [Fact]
    public void MSBuildGet_Simple()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "build", "Program.cs", "-getProperty:TargetFramework")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(ToolsetInfo.CurrentTargetFramework);
    }

    /// <summary>
    /// Check that <c>-get</c> commands work the same in project-based and file-based apps.
    /// </summary>
    [Theory]
    [InlineData(true, "build", "--getProperty:TargetFramework;Configuration")]
    [InlineData(true, "build", "--getItem:MyItem", "--getProperty:MyProperty")]
    [InlineData(true, "build", "--getItem:MyItem", "--getProperty:MyProperty", "-t:MyTarget")]
    [InlineData(true, "build", "--getItem:MyItem", "--getProperty:MyProperty", "--getTargetResult:MyTarget")]
    [InlineData(true, "build", "/getProperty:TargetFramework")]
    [InlineData(true, "build", "/getProperty:TargetFramework", "-p:LangVersion=wrong")] // evaluated only, so no failure
    [InlineData(false, "build", "/getProperty:TargetFramework", "-t:Build", "-p:LangVersion=wrong")] // fails with build error but still outputs info
    [InlineData(true, "build", "-getProperty:Configuration", "-getResultOutputFile:out.txt")]
    [InlineData(true, "build", "-getProperty:OutputType,Configuration", "-getResultOutputFile:out1.txt", "-getResultOutputFile:out2.txt")]
    [InlineData(true, "run", "-getProperty:Configuration")] // not supported, the arg is passed through to the app
    [InlineData(true, "restore", "-getProperty:Configuration")]
    [InlineData(true, "publish", "-getProperty:OutputType", "-p:PublishAot=false")]
    [InlineData(true, "pack", "-getProperty:OutputType", "-p:PublishAot=false")]
    [InlineData(true, "clean", "-getProperty:Configuration")]
    public void MSBuildGet_Consistent(bool success, string subcommand, params string[] args)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
                <Target Name="MyTarget" Returns="MyTargetReturn">
                    <ItemGroup>
                        <MyItem Include="item.txt" />
                        <MyTargetReturn Include="return.txt" />
                    </ItemGroup>
                    <PropertyGroup>
                        <MyProperty>MyValue</MyProperty>
                    </PropertyGroup>
                </Target>
            </Project>
            """);

        var fileBasedResult = new DotnetCommand(Log, [subcommand, "Program.cs", .. args])
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        var fileBasedFiles = ReadFiles();

        File.WriteAllText(Path.Join(testInstance.Path, "Program.csproj"), s_consoleProject);

        var projectBasedResult = new DotnetCommand(Log, [subcommand, .. args])
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        var projectBasedFiles = ReadFiles();

        fileBasedResult.StdOut.Should().Be(projectBasedResult.StdOut);
        fileBasedResult.StdErr.Should().Be(projectBasedResult.StdErr);
        fileBasedResult.ExitCode.Should().Be(projectBasedResult.ExitCode).And.Be(success ? 0 : 1);
        fileBasedFiles.Should().Equal(projectBasedFiles);

        Dictionary<string, string> ReadFiles()
        {
            var result = new DirectoryInfo(testInstance.Path)
                .EnumerateFiles()
                .ExceptBy(["Program.cs", "Directory.Build.props", "Program.csproj"], f => f.Name)
                .ToDictionary(f => f.Name, f => File.ReadAllText(f.FullName));

            foreach (var (file, text) in result)
            {
                Log.WriteLine($"File '{file}':");
                Log.WriteLine(text);
                File.Delete(Path.Join(testInstance.Path, file));
            }

            return result;
        }
    }
}
