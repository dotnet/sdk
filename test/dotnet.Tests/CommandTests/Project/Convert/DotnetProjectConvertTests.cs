// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Run.Tests;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Project.Convert.Tests;

[TestClass]
public sealed class DotnetProjectConvertTests : SdkTest
{
    /// <summary>
    /// <c>dotnet project convert</c> should result in the same project file text as <c>dotnet new console</c>.
    /// If this test fails, <c>dotnet project convert</c> command implementation should be updated.
    /// </summary>
    [TestMethod]
    public void SameAsTemplate()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var dotnetProjectConvert = Path.Join(testInstance.Path, "dotnetProjectConvert");
        Directory.CreateDirectory(dotnetProjectConvert);

        var csFile = Path.Combine(dotnetProjectConvert, "Program.cs");
        File.WriteAllText(csFile, """Console.WriteLine("Test");""");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(dotnetProjectConvert)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(dotnetProjectConvert)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs"]);

        new DirectoryInfo(Path.Join(dotnetProjectConvert, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);

        var dotnetProjectConvertProject = Path.Join(dotnetProjectConvert, "Program", "Program.csproj");

        Path.GetFileName(dotnetProjectConvertProject).Should().Be("Program.csproj");

        var dotnetNewConsole = Path.Join(testInstance.Path, "DotnetNewConsole");
        Directory.CreateDirectory(dotnetNewConsole);

        new DotnetCommand(Log, "new", "console")
            .WithWorkingDirectory(dotnetNewConsole)
            .Execute()
            .Should().Pass();

        var dotnetNewConsoleProject = Directory.EnumerateFiles(dotnetNewConsole, "*.csproj").Single();

        var dotnetProjectConvertProjectText = File.ReadAllText(dotnetProjectConvertProject);
        var dotnetNewConsoleProjectText = File.ReadAllText(dotnetNewConsoleProject);

        // There are some differences: we add PublishAot=true, PackAsTool=true, and UserSecretsId.
        var patchedDotnetProjectConvertProjectText = dotnetProjectConvertProjectText
            .Replace("""
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>

                """, string.Empty);
        patchedDotnetProjectConvertProjectText = Regex.Replace(patchedDotnetProjectConvertProjectText,
            """    <UserSecretsId>[^<]*<\/UserSecretsId>""" + Environment.NewLine, string.Empty);

        patchedDotnetProjectConvertProjectText.Should().Be(dotnetNewConsoleProjectText)
            .And.StartWith("""<Project Sdk="Microsoft.NET.Sdk">""");
    }

    [TestMethod] // https://github.com/dotnet/sdk/issues/50832
    [DataRow("File", "File", "Lib", "../Lib", "Project", "..{/}Lib{/}lib.csproj")]
    [DataRow(".", ".", "Lib", "./Lib", "Project", "..{/}Lib{/}lib.csproj")]
    [DataRow(".", ".", "Lib", "Lib/../Lib", "Project", "..{/}Lib{/}lib.csproj")]
    [DataRow("File", "File", "Lib", "../Lib", "File/Project", "..{/}..{/}Lib{/}lib.csproj")]
    [DataRow(".", "File", "Lib", "../Lib", "File/Project", "..{/}..{/}Lib{/}lib.csproj")]
    [DataRow("File", "File", "Lib", @"..\Lib", "File/Project", @"..{/}..\Lib{/}lib.csproj")]
    [DataRow("File", "File", "Lib", "../$(LibProjectName)", "File/Project", "..{/}..{/}$(LibProjectName){/}lib.csproj")]
    [DataRow(".", "File", "Lib", "../$(LibProjectName)", "File/Project", "..{/}..{/}$(LibProjectName){/}lib.csproj")]
    [DataRow("File", "File", "Lib", @"..\$(LibProjectName)", "File/Project", @"..{/}..\$(LibProjectName){/}lib.csproj")]
    [DataRow("File", "File", "Lib", "$(MSBuildProjectDirectory)/../$(LibProjectName)", "File/Project", "..{/}..{/}Lib{/}lib.csproj")]
    public void ProjectReference_RelativePaths(string workingDir, string fileDir, string libraryDir, string reference, string outputDir, string convertedReference)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var libraryDirFullPath = Path.Join(testInstance.Path, libraryDir);
        Directory.CreateDirectory(libraryDirFullPath);
        File.WriteAllText(Path.Join(libraryDirFullPath, "lib.cs"), """
            public static class C
            {
                public static void M()
                {
                    System.Console.WriteLine("Hello from library");
                }
            }
            """);
        File.WriteAllText(Path.Join(libraryDirFullPath, "lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var fileDirFullPath = Path.Join(testInstance.Path, fileDir);
        Directory.CreateDirectory(fileDirFullPath);
        var fileFullPath = Path.Join(fileDirFullPath, "app.cs");
        File.WriteAllText(fileFullPath, $"""
            #:project {reference}
            #:property LibProjectName=Lib
            C.M();
            """);

        var expectedOutput = "Hello from library";
        var workingDirFullPath = Path.Join(testInstance.Path, workingDir);
        var fileRelativePath = Path.GetRelativePath(relativeTo: workingDirFullPath, path: fileFullPath);

        new DotnetCommand(Log, "run", fileRelativePath)
            .WithWorkingDirectory(workingDirFullPath)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        var outputDirFullPath = Path.Join(testInstance.Path, outputDir);
        new DotnetCommand(Log, "project", "convert", fileRelativePath, "-o", outputDirFullPath)
            .WithWorkingDirectory(workingDirFullPath)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(outputDirFullPath)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        File.ReadAllText(Path.Join(outputDirFullPath, "app.csproj"))
            .Should().Contain($"""
                <ProjectReference Include="{convertedReference.Replace("{/}", Path.DirectorySeparatorChar.ToString())}" />
                """);
    }

    [TestMethod] // https://github.com/dotnet/sdk/issues/50832
    public void ProjectReference_FullPath()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var libraryDirFullPath = Path.Join(testInstance.Path, "Lib");
        Directory.CreateDirectory(libraryDirFullPath);
        File.WriteAllText(Path.Join(libraryDirFullPath, "lib.cs"), """
            public static class C
            {
                public static void M()
                {
                    System.Console.WriteLine("Hello from library");
                }
            }
            """);
        File.WriteAllText(Path.Join(libraryDirFullPath, "lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var fileDirFullPath = Path.Join(testInstance.Path, "File");
        Directory.CreateDirectory(fileDirFullPath);
        File.WriteAllText(Path.Join(fileDirFullPath, "app.cs"), $"""
            #:project {libraryDirFullPath}
            C.M();
            """);

        var expectedOutput = "Hello from library";

        new DotnetCommand(Log, "run", "app.cs")
            .WithWorkingDirectory(fileDirFullPath)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        var outputDirFullPath = Path.Join(testInstance.Path, "File/Project");
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(fileDirFullPath)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(outputDirFullPath)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        File.ReadAllText(Path.Join(outputDirFullPath, "app.csproj"))
            .Should().Contain($"""
                <ProjectReference Include="{Path.Join(libraryDirFullPath, "lib.csproj")}" />
                """);
    }

    [TestMethod]
    public void RefDirective()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>true</{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "lib.cs"), """
            #:property OutputType=Library
            namespace MyLib;
            public static class Greeter
            {
                public static string Greet(string name) => $"Hello, {name}!";
            }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "app.cs"), """
            #!/usr/bin/env dotnet
            #:ref lib.cs
            Console.WriteLine(MyLib.Greeter.Greet("World"));
            """);

        var expectedOutput = "Hello, World!";

        new DotnetCommand(Log, "run", "app.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        var outputDirFullPath = Path.Join(testInstance.Path, "Project");
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // #:ref lib.cs should become a ProjectReference to ../lib/lib.csproj
        File.ReadAllText(Path.Join(outputDirFullPath, "app", "app.csproj"))
            .Should().Contain($"""
                <ProjectReference Include="..{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}lib.csproj" />
                """);

        // The referenced library should have been converted too.
        var libProjectDir = Path.Join(outputDirFullPath, "lib");
        File.Exists(Path.Join(libProjectDir, "lib.csproj")).Should().BeTrue();
        File.Exists(Path.Join(libProjectDir, "lib.cs")).Should().BeTrue();
        File.ReadAllText(Path.Join(libProjectDir, "lib.csproj"))
            .Should().Contain("<OutputType>Library</OutputType>");

        // The converted project should build and produce the same output.
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(Path.Join(outputDirFullPath, "app"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [TestMethod]
    public void RefDirective_Metadata_Convert()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>true</{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "lib.cs"), """
            #:property OutputType=Library
            namespace MyLib;
            public static class Greeter
            {
                public static string Greet(string name) => $"Hello, {name}!";
            }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "app.cs"), """
            #!/usr/bin/env dotnet
            #:ref lib.cs Category=test
            Console.WriteLine(MyLib.Greeter.Greet("World"));
            """);

        var outputDirFullPath = Path.Join(testInstance.Path, "Project");
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // #:ref metadata should be carried over to the converted ProjectReference as a child element.
        var appProject = File.ReadAllText(Path.Join(outputDirFullPath, "app", "app.csproj"));
        appProject.Should().Contain($"""
                <ProjectReference Include="..{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}lib.csproj">
                  <Category>test</Category>
                </ProjectReference>
            """);

        // The converted project should build and produce the same output.
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(Path.Join(outputDirFullPath, "app"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello, World!");
    }

    [TestMethod]
    public void RefDirective_Transitive_Convert()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>true</{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "lib2.cs"), """
            #:property OutputType=Library
            namespace Lib2;
            public static class Helper
            {
                public static string Get() => "from lib2";
            }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "lib1.cs"), """
            #!/usr/bin/env dotnet
            #:property OutputType=Library
            #:ref lib2.cs
            namespace Lib1;
            public static class Facade
            {
                public static string Get() => $"from lib1 and {Lib2.Helper.Get()}";
            }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "app.cs"), """
            #!/usr/bin/env dotnet
            #:ref lib1.cs
            Console.WriteLine(Lib1.Facade.Get());
            """);

        var expectedOutput = "from lib1 and from lib2";

        new DotnetCommand(Log, "run", "app.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        var outputDirFullPath = Path.Join(testInstance.Path, "Project");
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // All three projects should exist.
        File.Exists(Path.Join(outputDirFullPath, "app", "app.csproj")).Should().BeTrue();
        File.Exists(Path.Join(outputDirFullPath, "lib1", "lib1.csproj")).Should().BeTrue();
        File.Exists(Path.Join(outputDirFullPath, "lib2", "lib2.csproj")).Should().BeTrue();

        // lib1.csproj should reference lib2.
        File.ReadAllText(Path.Join(outputDirFullPath, "lib1", "lib1.csproj"))
            .Should().Contain($"""
                <ProjectReference Include="..{Path.DirectorySeparatorChar}lib2{Path.DirectorySeparatorChar}lib2.csproj" />
                """);

        // The converted project should build and produce the same output.
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(Path.Join(outputDirFullPath, "app"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [TestMethod]
    public void RefDirective_DuplicateFolderName()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>true</{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>
              </PropertyGroup>
            </Project>
            """);

        Directory.CreateDirectory(Path.Join(testInstance.Path, "a"));
        File.WriteAllText(Path.Join(testInstance.Path, "a", "lib.cs"), """
            #:property OutputType=Library
            namespace A;
            public static class Lib { public static string Get() => "a"; }
            """);

        Directory.CreateDirectory(Path.Join(testInstance.Path, "b"));
        File.WriteAllText(Path.Join(testInstance.Path, "b", "lib.cs"), """
            #:property OutputType=Library
            namespace B;
            public static class Lib { public static string Get() => "b"; }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "app.cs"), """
            #:ref a/lib.cs
            #:ref b/lib.cs
            Console.WriteLine(A.Lib.Get() + B.Lib.Get());
            """);

        var outputDirFullPath = Path.Join(testInstance.Path, "Project");
        var duplicateTargetDirectory = Path.Join(outputDirFullPath, "lib");
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.ProjectConvertDuplicateRefFolderName, duplicateTargetDirectory));

        // Nothing should have been converted.
        Directory.Exists(outputDirFullPath).Should().BeFalse();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["a", "app.cs", "b", "Directory.Build.props"]);
    }

    [TestMethod]
    public void RefDirective_DuplicateFolderName_Transitive()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>true</{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>
              </PropertyGroup>
            </Project>
            """);

        // a/lib.cs is referenced by mid.cs
        Directory.CreateDirectory(Path.Join(testInstance.Path, "a"));
        File.WriteAllText(Path.Join(testInstance.Path, "a", "lib.cs"), """
            #:property OutputType=Library
            namespace A;
            public static class Lib { public static string Get() => "a"; }
            """);

        // mid.cs references a/lib.cs
        File.WriteAllText(Path.Join(testInstance.Path, "mid.cs"), """
            #:property OutputType=Library
            #:ref a/lib.cs
            namespace Mid;
            public static class Mid { public static string Get() => A.Lib.Get(); }
            """);

        // b/lib.cs would conflict with a/lib.cs (both "lib")
        Directory.CreateDirectory(Path.Join(testInstance.Path, "b"));
        File.WriteAllText(Path.Join(testInstance.Path, "b", "lib.cs"), """
            #:property OutputType=Library
            namespace B;
            public static class Lib { public static string Get() => "b"; }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "app.cs"), """
            #:ref mid.cs
            #:ref b/lib.cs
            Console.WriteLine(Mid.Mid.Get() + B.Lib.Get());
            """);

        var outputDirFullPath = Path.Join(testInstance.Path, "Project");
        var duplicateTargetDirectory = Path.Join(outputDirFullPath, "lib");
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.ProjectConvertDuplicateRefFolderName, duplicateTargetDirectory));

        // Nothing should have been converted.
        Directory.Exists(outputDirFullPath).Should().BeFalse();
    }

    [TestMethod]
    public void RefDirective_DuplicateFolderName_ViaInclude()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>true</{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>
              </PropertyGroup>
            </Project>
            """);

        // a/lib.cs is referenced by the app directly
        Directory.CreateDirectory(Path.Join(testInstance.Path, "a"));
        File.WriteAllText(Path.Join(testInstance.Path, "a", "lib.cs"), """
            #:property OutputType=Library
            namespace A;
            public static class Lib { public static string Get() => "a"; }
            """);

        // b/lib.cs would conflict (same name "lib") - referenced via #:include-d file
        Directory.CreateDirectory(Path.Join(testInstance.Path, "b"));
        File.WriteAllText(Path.Join(testInstance.Path, "b", "lib.cs"), """
            #:property OutputType=Library
            namespace B;
            public static class Lib { public static string Get() => "b"; }
            """);

        // extra.cs is included and references b/lib.cs
        File.WriteAllText(Path.Join(testInstance.Path, "extra.cs"), """
            #:ref b/lib.cs
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "app.cs"), """
            #:ref a/lib.cs
            #:include extra.cs
            Console.WriteLine(A.Lib.Get() + B.Lib.Get());
            """);

        var outputDirFullPath = Path.Join(testInstance.Path, "Project");
        var duplicateTargetDirectory = Path.Join(outputDirFullPath, "lib");
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.ProjectConvertDuplicateRefFolderName, duplicateTargetDirectory));

        // Nothing should have been converted.
        Directory.Exists(outputDirFullPath).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that default items (e.g., <c>appsettings.json</c>) in a <c>#:ref</c>'d file's directory
    /// are copied to the converted project output directory.
    /// </summary>
    [TestMethod]
    public void RefDirective_IncludedItemsCopied()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>true</{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>
              </PropertyGroup>
            </Project>
            """);

        var libDir = Path.Join(testInstance.Path, "lib");
        Directory.CreateDirectory(libDir);

        File.WriteAllText(Path.Join(libDir, "mylib.cs"), """
            #:property OutputType=Library
            #:property EnableDefaultNoneItems=true
            namespace MyLib;
            public static class Greeter
            {
                public static string Greet() => "Hello!";
            }
            """);

        // A non-code file next to the library that should be picked up as a default item.
        File.WriteAllText(Path.Join(libDir, "data.json"), """{ "key": "value" }""");

        File.WriteAllText(Path.Join(testInstance.Path, "app.cs"), """
            #:ref lib/mylib.cs
            Console.WriteLine(MyLib.Greeter.Greet());
            """);

        var outputDirFullPath = Path.Join(testInstance.Path, "Project");
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // The library's included item (data.json) should be copied to the ref output directory.
        var libOutputDir = Path.Join(outputDirFullPath, "mylib");
        File.Exists(Path.Join(libOutputDir, "mylib.cs")).Should().BeTrue();
        File.Exists(Path.Join(libOutputDir, "mylib.csproj")).Should().BeTrue();
        File.Exists(Path.Join(libOutputDir, "data.json")).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that <c>--delete-source</c> also deletes included items of <c>#:ref</c>'d files.
    /// </summary>
    [TestMethod]
    public void RefDirective_IncludedItemsDeleted()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>true</{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>
              </PropertyGroup>
            </Project>
            """);

        var libDir = Path.Join(testInstance.Path, "lib");
        Directory.CreateDirectory(libDir);

        File.WriteAllText(Path.Join(libDir, "mylib.cs"), """
            #:property OutputType=Library
            #:property EnableDefaultNoneItems=true
            namespace MyLib;
            public static class Greeter
            {
                public static string Greet() => "Hello!";
            }
            """);

        File.WriteAllText(Path.Join(libDir, "config.json"), """{ "setting": true }""");

        File.WriteAllText(Path.Join(testInstance.Path, "app.cs"), """
            #:ref lib/mylib.cs
            Console.WriteLine(MyLib.Greeter.Greet());
            """);

        var outputDirFullPath = Path.Join(testInstance.Path, "Project");
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath, "--delete-source")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // Source files should be deleted.
        File.Exists(Path.Join(testInstance.Path, "app.cs")).Should().BeFalse();
        File.Exists(Path.Join(libDir, "mylib.cs")).Should().BeFalse();
        File.Exists(Path.Join(libDir, "config.json")).Should().BeFalse();

        // Converted files should exist.
        var libOutputDir = Path.Join(outputDirFullPath, "mylib");
        File.Exists(Path.Join(libOutputDir, "mylib.cs")).Should().BeTrue();
        File.Exists(Path.Join(libOutputDir, "mylib.csproj")).Should().BeTrue();
        File.Exists(Path.Join(libOutputDir, "config.json")).Should().BeTrue();
    }

    /// <summary>
    /// Converting one app that <c>#:ref</c>s a library does not affect other apps that also reference the same library.
    /// </summary>
    [TestMethod]
    public void RefDirective_ConvertScope()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>true</{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "lib.cs"), """
            #:property OutputType=Library
            namespace MyLib;
            public static class Greeter
            {
                public static string Greet() => "Hello!";
            }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "app1.cs"), """
            #:ref lib.cs
            Console.WriteLine(MyLib.Greeter.Greet());
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "app2.cs"), """
            #:ref lib.cs
            Console.WriteLine(MyLib.Greeter.Greet());
            """);

        var unrelatedDir = Path.Join(testInstance.Path, "unrelated");
        Directory.CreateDirectory(unrelatedDir);
        File.WriteAllText(Path.Join(unrelatedDir, "app3.cs"), """
            #:ref ../lib.cs
            Console.WriteLine(MyLib.Greeter.Greet());
            """);

        var outputDirFullPath = Path.Join(testInstance.Path, "Project");
        new DotnetCommand(Log, "project", "convert", "app1.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // app1 should be converted.
        File.Exists(Path.Join(outputDirFullPath, "app1", "app1.csproj")).Should().BeTrue();
        File.Exists(Path.Join(outputDirFullPath, "lib", "lib.csproj")).Should().BeTrue();

        // app2 and app3 should be unaffected (still exist as .cs files with their directives intact).
        File.ReadAllText(Path.Join(testInstance.Path, "app2.cs")).Should().Contain("#:ref lib.cs");
        File.ReadAllText(Path.Join(unrelatedDir, "app3.cs")).Should().Contain("#:ref ../lib.cs");
    }

    [TestMethod]
    public void ProjectReference_FullPath_WithVars()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var libraryDirFullPath = Path.Join(testInstance.Path, "Lib");
        Directory.CreateDirectory(libraryDirFullPath);
        File.WriteAllText(Path.Join(libraryDirFullPath, "lib.cs"), """
            public static class C
            {
                public static void M()
                {
                    System.Console.WriteLine("Hello from library");
                }
            }
            """);
        File.WriteAllText(Path.Join(libraryDirFullPath, "lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var fileDirFullPath = Path.Join(testInstance.Path, "File");
        Directory.CreateDirectory(fileDirFullPath);
        File.WriteAllText(Path.Join(fileDirFullPath, "app.cs"), $"""
            #:project {fileDirFullPath}/../$(LibProjectName)
            #:property LibProjectName=Lib
            C.M();
            """);

        var expectedOutput = "Hello from library";

        new DotnetCommand(Log, "run", "app.cs")
            .WithWorkingDirectory(fileDirFullPath)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        var outputDirFullPath = Path.Join(testInstance.Path, "File/Project");
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(fileDirFullPath)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(outputDirFullPath)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        File.ReadAllText(Path.Join(outputDirFullPath, "app.csproj"))
            .Should().Contain($"""
                <ProjectReference Include="{"../../$(LibProjectName)/lib.csproj".Replace('/', Path.DirectorySeparatorChar)}" />
                """);
    }

    [TestMethod]
    public void DirectoryAlreadyExists()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var directoryPath = Path.Join(testInstance.Path, "MyApp");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(Path.Join(testInstance.Path, "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "MyApp.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.DirectoryAlreadyExists, directoryPath));

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["MyApp", "MyApp.cs"]);
    }

    [TestMethod]
    public void OutputOption()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        Directory.CreateDirectory(Path.Join(testInstance.Path, "MyApp"));
        File.WriteAllText(Path.Join(testInstance.Path, "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "MyApp.cs", "-o", "MyApp1")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateDirectories().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["MyApp", "MyApp1"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "MyApp"))
            .EnumerateFileSystemInfos().Should().BeEmpty();

        new DirectoryInfo(Path.Join(testInstance.Path, "MyApp1"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["MyApp.csproj", "MyApp.cs"]);
    }

    [TestMethod]
    public void OutputOption_DirectoryAlreadyExists()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var directoryPath = Path.Join(testInstance.Path, "SomeOutput");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(Path.Join(testInstance.Path, "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "MyApp.cs", "-o", "SomeOutput")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.DirectoryAlreadyExists, directoryPath));

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["MyApp.cs", "SomeOutput"]);
    }

    [TestMethod]
    public void MultipleEntryPointFiles()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program1.cs"), "Console.WriteLine(1);");
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), "Console.WriteLine(2);");

        new DotnetCommand(Log, "project", "convert", "Program1.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["Program1", "Program1.cs", "Program2.cs"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program1"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program1.csproj", "Program1.cs"]);
    }

    [TestMethod]
    public void NoFileArgument()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        new DotnetCommand(Log, "project", "convert")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("convert"); // Required argument missing for command 'convert'

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Should().BeEmpty();
    }

    [TestMethod]
    public void NonExistentFile()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        new DotnetCommand(Log, "project", "convert", "NotHere.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidFilePath, Path.Join(testInstance.Path, "NotHere.cs")));

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Should().BeEmpty();
    }

    [TestMethod]
    public void NonCSharpFile()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var filePath = Path.Join(testInstance.Path, "Program.vb");
        File.WriteAllText(filePath, "");

        new DotnetCommand(Log, "project", "convert", "Program.vb")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidFilePath, filePath));

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.vb"]);
    }

    [TestMethod]
    public void ExtensionCasing()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.CS"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "Program.CS")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.CS"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.CS"]);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("class C;")]
    public void FileContent(string content)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), content);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs"]);

        File.ReadAllText(Path.Join(testInstance.Path, "Program.cs"))
            .Should().Be(content);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.cs"))
            .Should().Be(content);
    }

    [TestMethod]
    public void NestedDirectory()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var appDirectory = Path.Join(testInstance.Path, "app");
        Directory.CreateDirectory(appDirectory);
        File.WriteAllText(Path.Join(appDirectory, "Program.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "app/Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(Path.Join(testInstance.Path, "app"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "app", "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);
    }

    /// <summary>
    /// Default items like <c>None</c> or <c>Content</c> are copied over if non-default SDK is used.
    /// </summary>
    [TestMethod]
    public void DefaultItems_NonDefaultSdk()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:sdk Microsoft.NET.Sdk.Web
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "my.json"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "");
        Directory.CreateDirectory(Path.Join(testInstance.Path, "subdir"));
        File.WriteAllText(Path.Join(testInstance.Path, "subdir", "second.json"), "");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs", "Resources.resx", "Util.cs", "my.json", "subdir"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Resources.resx", "Program.cs", "my.json", "subdir"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program", "subdir"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["second.json"]);
    }

    [TestMethod]
    public void DefaultItems_MoreIncluded()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property EnableDefaultCompileItems=true
            #:property EnableDefaultEmbeddedResourceItems=true
            #:property EnableDefaultNoneItems=true
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "my.json"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs", "Resources.resx", "Util.cs", "my.json"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs", "Resources.resx", "Util.cs", "my.json"]);
    }

    [TestMethod]
    public void DefaultItems_MoreExcluded()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property EnableDefaultItems=false
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "my.json"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs", "Resources.resx", "Util.cs", "my.json"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);
    }

    /// <summary>
    /// <c>ExcludeFromFileBasedAppConversion</c> metadata can be used to exclude items from the conversion.
    /// </summary>
    [TestMethod]
    public void DefaultItems_ExcludedViaMetadata()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property EnableDefaultEmbeddedResourceItems=true
            #:property EnableDefaultNoneItems=true
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "my.json"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "second.json"), "");

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.targets"), """
            <Project>
                <ItemGroup>
                    <None Update="$(MSBuildThisFileDirectory)second.json" ExcludeFromFileBasedAppConversion="true" />
                </ItemGroup>
            </Project>
            """);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Directory.Build.targets", "Program", "Program.cs", "Resources.resx", "Util.cs", "my.json", "second.json"]);

        // `second.json` is excluded from the conversion.
        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Directory.Build.targets", "Program.csproj", "Resources.resx", "Program.cs", "my.json"]);
    }

    [TestMethod]
    public void DefaultItems_ImplicitBuildFileInDirectory()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var srcDir = Path.Join(testInstance.Path, "src");
        Directory.CreateDirectory(srcDir);

        File.WriteAllText(Path.Join(srcDir, "Program.cs"), """
            #!/usr/bin/env dotnet
            #:sdk Microsoft.NET.Sdk.Web
            Console.WriteLine(Util.GetText());
            """);
        File.WriteAllText(Path.Join(srcDir, "Util.cs"), """
            class Util { public static string GetText() => "Hi from Util"; }
            """);
        File.WriteAllText(Path.Join(srcDir, "Directory.Build.props"), """
            <Project>
                <ItemGroup>
                    <Compile Include="Util.cs" />
                </ItemGroup>
            </Project>
            """);

        // The app works before conversion.
        string expectedOutput = "Hi from Util";
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(srcDir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        // Convert.
        new DotnetCommand(Log, "project", "convert", "Program.cs", "-o", "../out")
            .WithWorkingDirectory(srcDir)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(srcDir)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Directory.Build.props", "Program.cs", "Util.cs"]);

        var outDir = Path.Join(testInstance.Path, "out");

        // Directory.Build.props is included as it's a None item.
        new DirectoryInfo(outDir)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Directory.Build.props", "Program.csproj", "Program.cs", "Util.cs"]);

        // The app doesn't work immediately after conversion due to the Directory.Build.props file.
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(outDir)
            .Execute()
            .Should().Fail()
            // error NETSDK1022: Duplicate 'Compile' items were included.
            .And.HaveStdOutContaining("NETSDK1022");

        File.Delete(Path.Join(outDir, "Directory.Build.props"));

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(outDir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [TestMethod]
    public void DefaultItems_ImplicitBuildFileOutsideDirectory()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var srcDir = Path.Join(testInstance.Path, "src");
        var subdir = Path.Join(srcDir, "subdir");
        Directory.CreateDirectory(subdir);
        File.WriteAllText(Path.Join(subdir, "Program.cs"), """
            #!/usr/bin/env dotnet
            Console.WriteLine(Util.GetText());
            """);
        File.WriteAllText(Path.Join(subdir, "Util.cs"), """
            class Util { public static string GetText() => "Hi from Util"; }
            """);
        File.WriteAllText(Path.Join(srcDir, "Directory.Build.props"), """
            <Project>
                <ItemGroup>
                    <Compile Include="$(MSBuildThisFileDirectory)subdir\Util.cs" />
                </ItemGroup>
            </Project>
            """);

        // The app works before conversion.
        string expectedOutput = "Hi from Util";
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(subdir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        // Convert.
        new DotnetCommand(Log, "project", "convert", "Program.cs", "-o", "../../out")
            .WithWorkingDirectory(subdir)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(subdir)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.cs", "Util.cs"]);

        var outDir = Path.Join(testInstance.Path, "out");

        new DirectoryInfo(outDir)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs", "Util.cs"]);

        // The app works after conversion.
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(outDir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [TestMethod]
    public void DefaultItems_ImplicitBuildFileAndUtilOutsideDirectory()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var srcDir = Path.Join(testInstance.Path, "src");
        var subdir = Path.Join(srcDir, "subdir");
        Directory.CreateDirectory(subdir);
        File.WriteAllText(Path.Join(subdir, "Program.cs"), """
            #!/usr/bin/env dotnet
            Console.WriteLine(Util.GetText());
            """);
        File.WriteAllText(Path.Join(srcDir, "Util.cs"), """
            class Util { public static string GetText() => "Hi from Util"; }
            """);
        File.WriteAllText(Path.Join(srcDir, "Directory.Build.props"), """
            <Project>
                <ItemGroup>
                    <Compile Include="$(MSBuildThisFileDirectory)Util.cs" />
                </ItemGroup>
            </Project>
            """);

        // The app works before conversion.
        string expectedOutput = "Hi from Util";
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(subdir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        // Convert.
        new DotnetCommand(Log, "project", "convert", "Program.cs", "-o", "../../out")
            .WithWorkingDirectory(subdir)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(subdir)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.cs"]);

        var outDir = Path.Join(testInstance.Path, "out");

        new DirectoryInfo(outDir)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs", "Util.cs"]);

        // The app works after conversion.
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(outDir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    /// <summary>
    /// Scripts in repo root should not include default items.
    /// Part of <see href="https://github.com/dotnet/sdk/issues/49826"/>.
    /// </summary>
    [TestMethod, CombinatorialData]
    public void DefaultItems_AlongsideProj([CombinatorialValues("sln", "slnx", "csproj", "vbproj", "shproj", "proj")] string ext)
    {
        bool considered = ext is "sln" or "slnx" or "csproj";

        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property EnableDefaultEmbeddedResourceItems=true
            #:property EnableDefaultNoneItems=true
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "my.json"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "");
        File.WriteAllText(Path.Join(testInstance.Path, $"repo.{ext}"), "");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs", "Resources.resx", "Util.cs", "my.json", $"repo.{ext}"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(considered
                ? ["Program.csproj", "Program.cs"]
                : ["my.json", "Program.csproj", "Program.cs", "Resources.resx"]);
    }

    /// <summary>
    /// When processing fails due to invalid directives, no conversion should be performed
    /// (e.g., the target directory should not be created).
    /// </summary>
    [TestMethod]
    public void ProcessingFails()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var filePath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(filePath, "#:invalid");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(RunFileTests_General.DirectiveError(filePath, 1, FileBasedProgramsResources.UnrecognizedDirective, "invalid"));

        new DirectoryInfo(Path.Join(testInstance.Path))
            .EnumerateDirectories().Should().BeEmpty();
    }

    /// <summary>
    /// Since we perform MSBuild evaluation during the conversion (to find included items to copy over),
    /// the conversion can fail when the specified SDK does not exist.
    /// </summary>
    [TestMethod]
    public void ProcessingFails_Evaluation()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var filePath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(filePath, "#:sdk Microsoft.ThisSdkDoesNotExist");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // The SDK 'Microsoft.ThisSdkDoesNotExist' specified could not be found.
            .And.HaveStdErrContaining("Microsoft.ThisSdkDoesNotExist");

        new DirectoryInfo(Path.Join(testInstance.Path))
            .EnumerateDirectories().Should().BeEmpty();
    }

    /// <summary>
    /// End-to-end test of directive processing. More cases are covered by faster unit tests below.
    /// </summary>
    [TestMethod]
    public void ProcessingSucceeds()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var originalSource = """
            #:package Humanizer@2.14.1
            Console.WriteLine();
            """;
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), originalSource);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs"]);

        File.ReadAllText(Path.Join(testInstance.Path, "Program.cs"))
            .Should().Be(originalSource);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.cs"))
            .Should().Be("Console.WriteLine();");

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Match($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <UserSecretsId>Program-*</UserSecretsId>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Humanizer" Version="2.14.1" />
                  </ItemGroup>

                </Project>

                """);
    }

    [TestMethod, CombinatorialData]
    public void UserSecretsId_Overridden_ViaDirective(bool hasDirectiveBuildProps)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property UserSecretsId=MyIdFromDirective
            Console.WriteLine();
            """);

        if (hasDirectiveBuildProps)
        {
            File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
                <Project>
                  <PropertyGroup>
                    <UserSecretsId>MyIdFromDirBuildProps</UserSecretsId>
                  </PropertyGroup>
                </Project>
                """);
        }

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Be($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <UserSecretsId>MyIdFromDirective</UserSecretsId>
                  </PropertyGroup>

                </Project>

                """);
    }

    [TestMethod]
    public void UserSecretsId_Overridden_ViaDirectoryBuildProps()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <UserSecretsId>MyIdFromDirBuildProps</UserSecretsId>
              </PropertyGroup>
            </Project>
            """);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Be($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                </Project>

                """);
    }

    [TestMethod, CombinatorialData]
    public void UserSecretsId_Overridden_SameAsImplicit(bool hasDirective, bool hasDirectiveBuildProps)
    {
        const string implicitValue = "$(AssemblyName)-$([MSBuild]::StableStringHash($(MSBuildProjectFullPath.ToLowerInvariant()), 'Sha256'))";

        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            {(hasDirective ? $"#:property UserSecretsId={implicitValue}" : "")}
            Console.WriteLine();
            """);

        if (hasDirectiveBuildProps)
        {
            File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
                <Project>
                  <PropertyGroup>
                    <UserSecretsId>{SecurityElement.Escape(implicitValue)}</UserSecretsId>
                  </PropertyGroup>
                </Project>
                """);
        }

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Match($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <UserSecretsId>{(hasDirective ? SecurityElement.Escape(implicitValue) : "Program-*")}</UserSecretsId>
                  </PropertyGroup>

                </Project>

                """);
    }

    [TestMethod]
    public void ForceOption_Off()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var filePath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(filePath, """
            #:property Prop1=1
            #define X
            #:property Prop2=2
            Console.WriteLine();
            #:property Prop1=3
            """);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(FileBasedProgramsResources.CannotConvertDirective);

        new DirectoryInfo(Path.Join(testInstance.Path))
            .EnumerateDirectories().Should().BeEmpty();
    }

    [TestMethod]
    public void ForceOption_On()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var filePath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(filePath, """
            #:property Prop1=1
            #define X
            #:property Prop2=2
            Console.WriteLine();
            #:property Prop1=3
            """);

        new DotnetCommand(Log, "project", "convert", "--force", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(Path.Join(testInstance.Path))
            .EnumerateDirectories().Should().NotBeEmpty();
    }

    [TestMethod]
    public void Directives()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #!/program
                #:sdk Microsoft.NET.Sdk
                #:sdk Aspire.Hosting.Sdk@9.1.0
                #:property TargetFramework=net472
                #:package System.CommandLine@2.0.0-beta4.22272.1
                #:property LangVersion=preview
                Console.WriteLine();
                """,
            expectedProject: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <Sdk Name="Aspire.Hosting.Sdk" Version="9.1.0" />

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <TargetFramework>net472</TargetFramework>
                    <LangVersion>preview</LangVersion>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: """
                Console.WriteLine();
                """);
    }

    /// <summary>
    /// There should be only one <c>PropertyGroup</c> element when the default properties are overridden.
    /// </summary>
    [TestMethod]
    public void Directives_AllDefaultOverridden()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #!/program
                #:sdk Microsoft.NET.Web.Sdk
                #:property OutputType=Exe
                #:property TargetFramework=net472
                #:property Nullable=disable
                #:property PublishAot=false
                #:property PackAsTool=false
                #:property Custom=1
                #:property ImplicitUsings=disable
                Console.WriteLine();
                """,
            expectedProject: """
                <Project Sdk="Microsoft.NET.Web.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net472</TargetFramework>
                    <Nullable>disable</Nullable>
                    <PublishAot>false</PublishAot>
                    <PackAsTool>false</PackAsTool>
                    <Custom>1</Custom>
                    <ImplicitUsings>disable</ImplicitUsings>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                Console.WriteLine();
                """);
    }

    [TestMethod]
    public void Directives_Variable()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package MyPackage@$(MyProp)
                #:property MyProp=MyValue
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <MyProp>MyValue</MyProp>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="MyPackage" Version="$(MyProp)" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_DirectoryPath()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var libDir = Path.Join(testInstance.Path, "lib");
        Directory.CreateDirectory(libDir);
        File.WriteAllText(Path.Join(libDir, "Lib.csproj"), "test");

        var appDir = Path.Join(testInstance.Path, "app");
        Directory.CreateDirectory(appDir);

        var slash = Path.DirectorySeparatorChar;
        VerifyConversion(
            baseDirectory: testInstance.Path,
            filePath: Path.Join(appDir, "Program.cs"),
            evaluateDirectives: true,
            inputCSharp: """
                #:project ../lib
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                  <ItemGroup>
                    <ProjectReference Include="..{slash}lib{slash}Lib.csproj" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_IncludeExclude()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        VerifyConversion(
            baseDirectory: testInstance.Path,
            evaluateDirectives: true,
            inputCSharp: """
                #:include A.cs
                #:include ./**/*.cs
                #:exclude B.cs
                #:include C.ReSX
                #:include D.json
                #:include E.razor
                #:include F.cshtml
                #:exclude **/*
                #:include |.cs
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: "",
            expectedErrors:
            [
                (7, string.Format(FileBasedProgramsResources.IncludeOrExcludeDirectiveUnknownFileType, "#:include", RunFileTests_General.s_includeExcludeDefaultKnownExtensions)),
                (8, string.Format(FileBasedProgramsResources.IncludeOrExcludeDirectiveUnknownFileType, "#:exclude", RunFileTests_General.s_includeExcludeDefaultKnownExtensions)),
                (1, string.Format(FileBasedProgramsResources.IncludedFileNotFound, Path.Join(testInstance.Path, "A.cs"))),
                (1, string.Format(FileBasedProgramsResources.IncludedFileNotFound, Path.Join(testInstance.Path, "|.cs"))),
            ]);
    }

    [TestMethod]
    public void Directives_IncludeCompile_DefaultCompileItemsDisabled()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #!/usr/bin/env dotnet
            #:property EnableDefaultCompileItems=false
            #:include Util.cs
            Console.WriteLine(Util.GetMessage());
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), """
            static class Util
            {
                public static string GetMessage() => "Hello from Util";
            }
            """);

        var expectedOutput = "Hello from Util";

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Match($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <UserSecretsId>Program-*</UserSecretsId>
                    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                  </PropertyGroup>

                  <ItemGroup>
                    <Compile Include="Program.cs" />
                    <Compile Include="Util.cs" />
                  </ItemGroup>

                </Project>

                """);

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(Path.Join(testInstance.Path, "Program"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [TestMethod]
    public void Directives_IncludeDll()
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

        File.WriteAllText(Path.Join(libDir, "LibClass.cs"), """
            namespace Lib;
            public static class LibClass
            {
                public static string GetMessage() => "Hello from Lib";
            }
            """);

        new DotnetCommand(Log, "build", "Lib.csproj")
            .WithWorkingDirectory(libDir)
            .Execute()
            .Should().Pass();

        var libDllDirectivePath = $"Lib/bin/Debug/{ToolsetInfo.CurrentTargetFramework}/Lib.dll";
        var libDllProjectPath = Path.Join("Lib", "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "Lib.dll");

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $$"""
            #!/usr/bin/env dotnet
            #:include {{libDllDirectivePath}}
            Console.WriteLine(Lib.LibClass.GetMessage());
            """);

        var expectedOutput = "Hello from Lib";

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Match($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <UserSecretsId>Program-*</UserSecretsId>
                  </PropertyGroup>

                  <ItemGroup>
                    <Reference Include="{libDllProjectPath}" />
                  </ItemGroup>

                </Project>

                """);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Lib", "Program.cs", "Program.csproj"]);

        File.Exists(Path.Join(testInstance.Path, "Program", libDllProjectPath)).Should().BeTrue();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(Path.Join(testInstance.Path, "Program"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [TestMethod]
    public void Directives_IncludeDll_Wildcard()
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

        File.WriteAllText(Path.Join(libDir, "LibClass.cs"), """
            namespace Lib;
            public static class LibClass
            {
                public static string GetMessage() => "Hello from Lib";
            }
            """);

        new DotnetCommand(Log, "build", "Lib.csproj")
            .WithWorkingDirectory(libDir)
            .Execute()
            .Should().Pass();

        File.Copy(
            Path.Join(libDir, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "Lib.dll"),
            Path.Join(testInstance.Path, "Lib.dll"));

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #!/usr/bin/env dotnet
            #:include *.dll
            Console.WriteLine(Lib.LibClass.GetMessage());
            """);

        var expectedOutput = "Hello from Lib";

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Contain("<Reference Include=\"Lib.dll\" />");

        File.Exists(Path.Join(testInstance.Path, "Program", "Lib.dll")).Should().BeTrue();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(Path.Join(testInstance.Path, "Program"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    /// <summary>
    /// If a file is included only via MSBuild code but is not in the mapped conversion item set, conversion won't copy it to the output directory.
    /// </summary>
    [TestMethod]
    public void Directives_IncludeDll_DirectoryBuildProps_NoMapping()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var srcDir = Path.Join(testInstance.Path, "src");
        var appDir = Path.Join(srcDir, "app");
        var libDir = Path.Join(srcDir, "Lib");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(libDir);

        File.WriteAllText(Path.Join(libDir, "Lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(libDir, "LibClass.cs"), """
            namespace Lib;
            public static class LibClass
            {
                public static string GetMessage() => "Hello from Lib";
            }
            """);

        new DotnetCommand(Log, "build", "Lib.csproj")
            .WithWorkingDirectory(libDir)
            .Execute()
            .Should().Pass();

        var libDllPropsPath = Path.Join("Lib", "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "Lib.dll");

        File.WriteAllText(Path.Join(srcDir, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.IncludeOrExclude.MappingPropertyName}></{CSharpDirective.IncludeOrExclude.MappingPropertyName}>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="$(MSBuildThisFileDirectory){libDllPropsPath}" />
              </ItemGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(appDir, "Program.cs"), """
            Console.WriteLine(Lib.LibClass.GetMessage());
            """);

        var expectedOutput = "Hello from Lib";

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(appDir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        new DotnetCommand(Log, "project", "convert", "Program.cs", "-o", "../../out")
            .WithWorkingDirectory(appDir)
            .Execute()
            .Should().Pass();

        var outDir = Path.Join(testInstance.Path, "out");
        File.Exists(Path.Join(outDir, "Lib.dll")).Should().BeFalse();
        File.ReadAllText(Path.Join(outDir, "Program.csproj"))
            .Should().NotContain("<Reference");

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(outDir)
            .Execute()
            .Should().Fail()
            // error CS0103: The name 'Lib' does not exist in the current context
            .And.HaveStdOutContaining("error CS0103");
    }

    /// <summary>
    /// If an item added by MSBuild has corresponding FileBasedProgramsItemMapping, conversion copies it but does not write it to the project file.
    /// </summary>
    [TestMethod]
    public void Directives_IncludeDll_DirectoryBuildProps_ItemMapping()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var srcDir = Path.Join(testInstance.Path, "src");
        var appDir = Path.Join(srcDir, "app");
        var libDir = Path.Join(srcDir, "Lib");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(libDir);

        File.WriteAllText(Path.Join(libDir, "Lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(libDir, "LibClass.cs"), """
            namespace Lib;
            public static class LibClass
            {
                public static string GetMessage() => "Hello from Lib";
            }
            """);

        new DotnetCommand(Log, "build", "Lib.csproj")
            .WithWorkingDirectory(libDir)
            .Execute()
            .Should().Pass();

        var libDllPropsPath = Path.Join("Lib", "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "Lib.dll");

        File.WriteAllText(Path.Join(srcDir, "Directory.Build.props"), $"""
            <Project>
              <ItemGroup>
                <Reference Include="$(MSBuildThisFileDirectory){libDllPropsPath}" />
              </ItemGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(appDir, "Program.cs"), """
            Console.WriteLine(Lib.LibClass.GetMessage());
            """);

        var expectedOutput = "Hello from Lib";

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(appDir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        new DotnetCommand(Log, "project", "convert", "Program.cs", "-o", "../../out")
            .WithWorkingDirectory(appDir)
            .Execute()
            .Should().Pass();

        var outDir = Path.Join(testInstance.Path, "out");
        File.Exists(Path.Join(outDir, "Lib.dll")).Should().BeTrue();
        File.ReadAllText(Path.Join(outDir, "Program.csproj"))
            .Should().NotContain("<Reference");

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(outDir)
            .Execute()
            .Should().Fail()
            // error CS0103: The name 'Lib' does not exist in the current context
            .And.HaveStdOutContaining("error CS0103");
    }

    [TestMethod]
    public void Directives_IncludeDll_ViaIncludedFile()
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

        File.WriteAllText(Path.Join(libDir, "LibClass.cs"), """
            namespace Lib;
            public static class LibClass
            {
                public static string GetMessage() => "Hello from Lib";
            }
            """);

        new DotnetCommand(Log, "build", "Lib.csproj")
            .WithWorkingDirectory(libDir)
            .Execute()
            .Should().Pass();

        var libDllDirectivePath = $"Lib/bin/Debug/{ToolsetInfo.CurrentTargetFramework}/Lib.dll";
        var libDllProjectPath = Path.Join("Lib", "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "Lib.dll");

        File.WriteAllText(Path.Join(testInstance.Path, "extra.cs"), $$"""
            #:include {{libDllDirectivePath}}
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #!/usr/bin/env dotnet
            #:include extra.cs
            Console.WriteLine(Lib.LibClass.GetMessage());
            """);

        var expectedOutput = "Hello from Lib";

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Contain($"<Reference Include=\"{libDllProjectPath}\" />");

        File.Exists(Path.Join(testInstance.Path, "Program", libDllProjectPath)).Should().BeTrue();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(Path.Join(testInstance.Path, "Program"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [TestMethod]
    public void Directives_IncludeDll_ViaRef()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>true</{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>
              </PropertyGroup>
            </Project>
            """);

        var libDir = Path.Join(testInstance.Path, "lib");
        Directory.CreateDirectory(libDir);

        var dependencyDir = Path.Join(libDir, "Dependency");
        Directory.CreateDirectory(dependencyDir);

        File.WriteAllText(Path.Join(dependencyDir, "Dependency.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(dependencyDir, "DependencyClass.cs"), """
            namespace Dependency;
            public static class DependencyClass
            {
                public static string GetMessage() => "Hello from dependency";
            }
            """);

        new DotnetCommand(Log, "build", "Dependency.csproj")
            .WithWorkingDirectory(dependencyDir)
            .Execute()
            .Should().Pass();

        var dependencyDllDirectivePath = $"Dependency/bin/Debug/{ToolsetInfo.CurrentTargetFramework}/Dependency.dll";
        var dependencyDllProjectPath = Path.Join("Dependency", "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "Dependency.dll");

        File.WriteAllText(Path.Join(libDir, "lib.cs"), $$"""
            #!/usr/bin/env dotnet
            #:property OutputType=Library
            #:include {{dependencyDllDirectivePath}}
            namespace MyLib;
            public static class Wrapper
            {
                public static string GetMessage() => Dependency.DependencyClass.GetMessage();
            }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #!/usr/bin/env dotnet
            #:ref lib/lib.cs
            Console.WriteLine(MyLib.Wrapper.GetMessage());
            """);

        var expectedOutput = "Hello from dependency";

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        var outputDirFullPath = Path.Join(testInstance.Path, "Project");
        new DotnetCommand(Log, "project", "convert", "Program.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        File.ReadAllText(Path.Join(outputDirFullPath, "lib", "lib.csproj"))
            .Should().Contain($"<Reference Include=\"{dependencyDllProjectPath}\" />");

        File.Exists(Path.Join(outputDirFullPath, "lib", dependencyDllProjectPath)).Should().BeTrue();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(Path.Join(outputDirFullPath, "Program"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [TestMethod]
    public void Directives_IncludeExclude_FilesCopied()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:include **/*.cs
            #:include *.json
            #:exclude my.json
            #:include */*.resx
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "my.json"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs", "Resources.resx", "Util.cs", "my.json"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs", "Util.cs"]);
    }

    [TestMethod]
    public void Directives_Separators()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property Prop1 = One=a/b
                #:property Prop2 = Two/a=b
                #:sdk First @ 1.0=a/b
                #:sdk Second @ 2.0/a=b
                #:sdk Third @ 3.0=a/b
                #:package P1 @ 1.0/a=b
                #:package P2 @ 2.0/a=b
                #:package P3@1.0 ab
                """,
            expectedProject: $"""
                <Project Sdk="First/1.0=a/b">

                  <Sdk Name="Second" Version="2.0/a=b" />
                  <Sdk Name="Third" Version="3.0=a/b" />

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Prop1>One=a/b</Prop1>
                    <Prop2>Two/a=b</Prop2>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="P1" Version="1.0/a=b" />
                    <PackageReference Include="P2" Version="2.0/a=b" />
                    <PackageReference Include="P3" Version="1.0 ab" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_PackageMetadata()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package Microsoft.Build@18.0.2 ExcludeAssets=runtime PrivateAssets=all
                #:package NoVersion IncludeAssets=build
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Microsoft.Build" Version="18.0.2">
                      <ExcludeAssets>runtime</ExcludeAssets>
                      <PrivateAssets>all</PrivateAssets>
                    </PackageReference>
                    <PackageReference Include="NoVersion">
                      <IncludeAssets>build</IncludeAssets>
                    </PackageReference>
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_ProjectMetadata()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Lib.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk" />
            """);

        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:project Lib.csproj Private=false OutputItemType=Analyzer
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                  <ItemGroup>
                    <ProjectReference Include="Lib.csproj">
                      <Private>false</Private>
                      <OutputItemType>Analyzer</OutputItemType>
                    </ProjectReference>
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_Quoting()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property Description="Hello World"
                #:package P1@1.0.0 Note="see the docs" Label="second quoted value"
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Description>Hello World</Description>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="P1" Version="1.0.0">
                      <Note>see the docs</Note>
                      <Label>second quoted value</Label>
                    </PackageReference>
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_WhitespaceAroundSeparator()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Lib.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk" />
            """);

        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:sdk My.Sdk @ "1.2.3"
                #:property Xyz = "abc "
                #:package P1 @ "1.0.0" Note = "see the docs"
                #:project Lib.csproj Private = "false" OutputItemType = Analyzer
                """,
            expectedProject: $"""
                <Project Sdk="My.Sdk/1.2.3">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Xyz>abc{" "}</Xyz>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="P1" Version="1.0.0">
                      <Note>see the docs</Note>
                    </PackageReference>
                  </ItemGroup>

                  <ItemGroup>
                    <ProjectReference Include="Lib.csproj">
                      <Private>false</Private>
                      <OutputItemType>Analyzer</OutputItemType>
                    </ProjectReference>
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_QuotedNameBeforeSeparator()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:sdk "My.Sdk"@1.2.3
                #:property "Xyz"="abc "
                #:package "Humanizer"@2.0
                """,
            expectedProject: $"""
                <Project Sdk="My.Sdk/1.2.3">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Xyz>abc{" "}</Xyz>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Humanizer" Version="2.0" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    [DataRow("""#:package "we@ird"@2.0""", "package", "@")]
    [DataRow("""#:sdk "we@ird"@2.0""", "sdk", "@")]
    [DataRow("""#:property "a=b"=c""", "property", "=")]
    public void Directives_QuotedNameContainingSeparator(string directive, string directiveKind, string expectedSeparator)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: directive,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.InvalidDirectiveName, directiveKind, expectedSeparator)),
            ]);
    }

    [TestMethod]
    public void Directives_QuotedVersion()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:sdk My.Sdk@"1.0 beta"
                #:package P1@"1.0 beta"
                """,
            expectedProject: $"""
                <Project Sdk="My.Sdk/1.0 beta">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="P1" Version="1.0 beta" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_QuoteEscapes()
    {
        // A quoted value is lexed as a regular C# string literal, so escape sequences are decoded.
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property Quote="a\"b"
                #:property Backslash="a\\b"
                #:property Tab="a\tb c"
                #:package P1@1.0.0 Note="quote\"and\\slash"
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Quote>a&quot;b</Quote>
                    <Backslash>a\b</Backslash>
                    <Tab>a{"\t"}b c</Tab>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="P1" Version="1.0.0">
                      <Note>quote&quot;and\slash</Note>
                    </PackageReference>
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_UnterminatedQuote()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property Description="unterminated
                """,
            expectedErrors:
            [
                (1, FileBasedProgramsResources.UnterminatedQuoteInDirective),
            ]);
    }

    [TestMethod]
    public void Directives_InvalidEscapeSequence()
    {
        // A terminated literal with a bad escape reports the underlying C# lexer error (not "unterminated").
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property Description="a\qb"
                """,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.InvalidStringLiteralInDirective, "Unrecognized escape sequence")),
            ]);
    }

    [TestMethod]
    [DataRow("#:property A=B\"C\"")]
    [DataRow("#:property A=B\"C\"D")]
    [DataRow("#:property A=\"B\"C")]
    [DataRow("#:property A\"B\"=C")]
    public void Directives_InvalidQuote(string directive)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: directive,
            expectedErrors:
            [
                (1, FileBasedProgramsResources.InvalidQuoteInDirective),
            ]);
    }

    [TestMethod]
    public void Directives_QuoteFollowedByComment()
    {
        // To the C# lexer, the '//' is trailing trivia of the preceding string literal and runs to the
        // end of the line. Directive text is not C#, so the rest of the line must still be tokenized as
        // metadata - here reported as an invalid metadata name rather than silently dropped as a comment.
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package P1@1.0.0 Note="x" //y="z"
                """,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.DirectiveMetadataInvalidName, "//y", "Name cannot begin with the '/' character, hexadecimal value 0x2F.")),
            ]);
    }

    [TestMethod]
    public void Directives_CommentLikeMetadataValue()
    {
        // '//' is only meaningful to the C# lexer, never in directive text, so it round-trips verbatim
        // in a metadata value both bare and inside a quoted value.
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package P1@1.0.0 Note="x" Bare=//y Quoted="//server/my share"
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="P1" Version="1.0.0">
                      <Note>x</Note>
                      <Bare>//y</Bare>
                      <Quoted>//server/my share</Quoted>
                    </PackageReference>
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    [DataRow("#:property Description=\"\"\"abc\"\"\"", "\"\"\"abc\"\"\"")]
    public void Directives_RawStringLiteralRejected(string directive, string expectedTokenText)
    {
        // Raw string literals ('"""..."""') lex to a different token kind and are not supported; the
        // error shows the offending token text rather than assuming a specific kind.
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: directive,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.ExpectedSimpleStringLiteralInDirective, expectedTokenText)),
            ]);
    }

    [TestMethod]
    public void Directives_InvalidMetadataName()
    {
        // A quote forces the strict (new) form, so the metadata name is validated.
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package P1@1.0.0 1Invalid="value"
                """,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.DirectiveMetadataInvalidName, "1Invalid", "Name cannot begin with the '1' character, hexadecimal value 0x31.")),
            ]);
    }

    [TestMethod]
    public void Directives_EmptyMetadataName()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package P1@1.0.0 ="value"
                """,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.InvalidDirectiveMetadata, "=value")),
            ]);
    }

    [TestMethod]
    public void Directives_ConflictingPackageVersionMetadata()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package P1@1.0.0 Version=2.0.0
                """,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.ConflictingDirectiveMetadata, "Version")),
            ]);
    }

    [TestMethod]
    [DataRow("#:package P1@1.0.0 Note=a note=b", "note")]
    [DataRow("#:project Lib.csproj Private=false private=true", "private")]
    [DataRow("#:ref Lib.cs Alias=a Alias=b", "Alias")]
    public void Directives_DuplicateMetadata(string directive, string duplicateName)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: directive,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.DuplicateDirectiveMetadata, duplicateName)),
            ]);
    }

    [TestMethod]
    // Directive kinds that do not support trailing 'Name=Value' metadata. A quote is used to force the
    // strict (new) form; otherwise the extra tokens would be accepted verbatim as a legacy single value.
    [DataRow("#:sdk MySdk Extra=\"a b\"", "sdk")]
    [DataRow("#:property Name=\"v\" Extra=\"a b\"", "property")]
    [DataRow("#:include \"a.cs\" Extra=\"a b\"", "include")]
    [DataRow("#:exclude \"a.cs\" Extra=\"a b\"", "exclude")]
    public void Directives_MetadataOnUnsupportedKind(string directive, string kind)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: directive,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.UnexpectedDirectiveText, kind)),
            ]);
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("SDK")]
    public void Directives_Unknown(string directive)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: $"""
                #:sdk Test
                #:{directive} Test
                """,
            expectedCSharp: $"""
                #:{directive} Test
                """,
            expectedErrors:
            [
                (2, string.Format(FileBasedProgramsResources.UnrecognizedDirective, directive)),
            ]);
    }

    [TestMethod]
    public void Directives_Empty()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:
                #:sdk Test
                """,
            expectedCSharp: """
                #:

                """,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.UnrecognizedDirective, "")),
            ]);
    }

    [TestMethod, CombinatorialData]
    public void Directives_EmptyName(
        [CombinatorialValues("sdk", "property", "package", "project", "include", "exclude")] string directive,
        [CombinatorialValues(" ", "")] string value)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: $"""
                #:{directive}{value}
                """,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.MissingDirectiveName, directive)),
            ]);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\"\"")]
    [DataRow(" \"\"")]
    public void Directives_EmptyValue(string value)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: $"""
                #:property TargetFramework={value}
                #:property Prop1={value}
                #:sdk First@{value}
                #:sdk Second@{value}
                #:package P1@{value}
                """,
            expectedProject: """
                <Project Sdk="First/">

                  <Sdk Name="Second" Version="" />

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <TargetFramework></TargetFramework>
                    <Prop1></Prop1>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="P1" Version="" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");

        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: $"""
                #:project {value}
                """,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.MissingDirectiveName, "project")),
            ]);
    }

    [TestMethod]
    public void Directives_EmptyValueBeforeMetadata()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package P1@ IncludeAssets=build Note="two words"
                #:package P2@1.0 First= Second=false
                #:package P3@ IncludeAssets = "build"
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="P1" Version="">
                      <IncludeAssets>build</IncludeAssets>
                      <Note>two words</Note>
                    </PackageReference>
                    <PackageReference Include="P2" Version="1.0">
                      <First></First>
                      <Second>false</Second>
                    </PackageReference>
                    <PackageReference Include="P3" Version="">
                      <IncludeAssets>build</IncludeAssets>
                    </PackageReference>
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_MissingPropertyValue()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property Test
                """,
            expectedErrors:
            [
                (1, FileBasedProgramsResources.PropertyDirectiveMissingParts),
            ]);
    }

    [TestMethod]
    [DataRow("sdk")]
    [DataRow("property")]
    [DataRow("package")]
    [DataRow("project")]
    [DataRow("ref")]
    [DataRow("include")]
    [DataRow("exclude")]
    public void Directives_EmptyQuotedName(string directiveKind)
    {
        // Quoting makes an explicitly empty name expressible; it is rejected like a missing one.
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: $"""
                #:{directiveKind} ""
                """,
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.MissingDirectiveName, directiveKind)),
            ]);
    }

    [TestMethod]
    public void Directives_EmptyMetadataValue()
    {
        // Item metadata may have an empty value, bare or quoted, in trailing as well as middle position.
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Lib.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk" />
            """);

        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package P1@1.0 Bare= Quoted="" Last=
                #:project Lib.csproj Private= Trailing=""
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="P1" Version="1.0">
                      <Bare></Bare>
                      <Quoted></Quoted>
                      <Last></Last>
                    </PackageReference>
                  </ItemGroup>

                  <ItemGroup>
                    <ProjectReference Include="Lib.csproj">
                      <Private></Private>
                      <Trailing></Trailing>
                    </ProjectReference>
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    [DataRow("123Name", "Name cannot begin with the '1' character, hexadecimal value 0x31.")]
    [DataRow("Prefix:Name", "The ':' character, hexadecimal value 0x3A, cannot be included in a name.")]
    public void Directives_InvalidPropertyName(string propertyName, string errorMessage)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: $"#:property {propertyName}=Value",
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.PropertyDirectiveInvalidName, errorMessage)),
            ]);
    }

    [TestMethod]
    [DataRow("sdk", "@", "/")]
    [DataRow("sdk", "@", " ")]
    [DataRow("sdk", "@", "=")]
    [DataRow("package", "@", "/")]
    [DataRow("package", "@", " ")]
    [DataRow("package", "@", "=")]
    [DataRow("property", "=", "/")]
    [DataRow("property", "=", " ")]
    [DataRow("property", "=", "@")]
    public void Directives_InvalidName(string directiveKind, string expectedSeparator, string actualSeparator)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: $"#:{directiveKind} Abc{actualSeparator}Xyz",
            expectedErrors:
            [
                (1, string.Format(FileBasedProgramsResources.InvalidDirectiveName, directiveKind, expectedSeparator)),
            ]);
    }

    [TestMethod]
    public void Directives_Escaping()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property Prop=<te'st>&x
                #:sdk Name@<>te'st
                #:package Pack@1.0 Meta=<a&b>
                #:property Desc="a <b> c"
                """,
            expectedProject: $"""
                <Project Sdk="Name/&lt;&gt;te&apos;st">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Prop>&lt;te&apos;st&gt;&amp;x</Prop>
                    <Desc>a &lt;b&gt; c</Desc>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Pack" Version="1.0">
                      <Meta>&lt;a&amp;b&gt;</Meta>
                    </PackageReference>
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_Whitespace()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                    #:   sdk   TestSdk
                #:property Name  =  Value   
                #:property NugetPackageDescription="My package with spaces"
                 #  !  /test
                  #!  /program   x   
                 # :property Name=Value
                """,
            expectedProject: $"""
                <Project Sdk="TestSdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Name>Value</Name>
                    <NugetPackageDescription>My package with spaces</NugetPackageDescription>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                 #  !  /test
                  #!  /program   x   
                 # :property Name=Value
                """);
    }

    [TestMethod]
    public void Directives_BlankLines()
    {
        var expectedProject = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <PublishAot>true</PublishAot>
                <PackAsTool>true</PackAsTool>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="A" Version="B" />
              </ItemGroup>

            </Project>

            """;

        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package A@B

                Console.WriteLine();
                """,
            expectedProject: expectedProject,
            expectedCSharp: """
                Console.WriteLine();
                """);

        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """

                #:package A@B
                Console.WriteLine();
                """,
            expectedProject: expectedProject,
            expectedCSharp: """

                Console.WriteLine();
                """);
    }

    /// <summary>
    /// <c>#:</c> directives after C# code are ignored.
    /// </summary>
    [TestMethod]
    public void Directives_AfterToken()
    {
        string source = """
            #:property Prop1=1
            #define X
            #:property Prop2=2
            Console.WriteLine();
            #:property Prop1=3
            """;

        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: source,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Prop1>1</Prop1>
                    <Prop2>2</Prop2>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                #define X
                Console.WriteLine();
                #:property Prop1=3
                """,
            expectedErrors:
            [
                (5, FileBasedProgramsResources.CannotConvertDirective),
            ]);
    }

    /// <summary>
    /// <c>#:</c> directives after <c>#if</c> are ignored.
    /// </summary>
    [TestMethod]
    public void Directives_AfterIf()
    {
        string source = """
            #:property Prop1=1
            #define X
            #:property Prop2=2
            #if X
            #:property Prop1=3
            #endif
            #:property Prop2=4
            """;

        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: source,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Prop1>1</Prop1>
                    <Prop2>2</Prop2>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                #define X
                #if X
                #:property Prop1=3
                #endif
                #:property Prop2=4
                """,
            expectedErrors:
            [
                (5, FileBasedProgramsResources.CannotConvertDirective),
                (7, FileBasedProgramsResources.CannotConvertDirective),
            ]);
    }

    /// <summary>
    /// Comments are not currently converted.
    /// </summary>
    [TestMethod]
    public void Directives_Comments()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                // License for this file
                #:sdk MySdk
                // This package is needed for Json
                #:package MyJson
                // #:package Unused
                /* Custom props: */
                #:property Prop1=1
                #:property Prop2=2
                Console.Write();
                """,
            expectedProject: $"""
                <Project Sdk="MySdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Prop1>1</Prop1>
                    <Prop2>2</Prop2>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="MyJson" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: """
                // License for this file
                // This package is needed for Json
                // #:package Unused
                /* Custom props: */
                Console.Write();
                """);
    }

    [TestMethod]
    public void Directives_IdenticalDuplicate()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:sdk Microsoft.NET.Sdk
                #:sdk Microsoft.NET.Sdk
                #:package Package@1.0
                #:package Package@1.0
                #:property Prop=1
                #:property Prop=1
                Console.Write();
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Prop>1</Prop>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Package" Version="1.0" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: """
                Console.Write();
                """,
            evaluateDirectives: true);
    }

    [TestMethod]
    public void Directives_Duplicate_UsesUnevaluatedValues()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property A=a
                #:property B=b
                #:property B=b
                #:property C=a
                #:property C=$(A)
                #:property D=$(A)
                #:property D=$(A)
                Console.Write();
                """,
            expectedProject: null,
            expectedCSharp: """
                Console.Write();
                """,
            expectedErrors:
            [
                (5, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:property C")),
            ],
            evaluateDirectives: true);
    }

    [TestMethod]
    public void Directives_Duplicate_EvaluatedProjectDirectivesAreAllowed()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Lib.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk" />
            """);

        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property LibraryProject=Lib.csproj
                #:project Lib.csproj
                #:project $(LibraryProject)
                Console.Write();
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <LibraryProject>Lib.csproj</LibraryProject>
                  </PropertyGroup>

                  <ItemGroup>
                    <ProjectReference Include="Lib.csproj" />
                    <ProjectReference Include="Lib.csproj" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: """
                Console.Write();
                """,
            evaluateDirectives: true);
    }

    [TestMethod]
    public void Directives_Duplicate()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property Prop=1
                #:property Prop=2
                """,
            expectedCSharp: "",
            expectedErrors:
            [
                (2, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:property Prop")),
            ]);

        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:sdk Name
                #:sdk Name@X
                #:sdk Name@Y
                #:sdk Name
                #:sdk Name2
                """,
            expectedCSharp: "",
            expectedErrors:
            [
                (2, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:sdk Name")),
                (3, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:sdk Name")),
            ]);

        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package Name
                #:package Name@X
                #:package Name@Y
                #:package Name
                #:package Name2
                """,
            expectedCSharp: "",
            expectedErrors:
            [
                (2, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:package Name")),
                (3, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:package Name")),
            ]);

        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:sdk Prop@1
                #:property Prop=2
                """,
            expectedCSharp: "");

        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property Prop=1
                #:property Prop=2
                #:property Prop2=3
                #:property Prop=4
                """,
            expectedCSharp: "",
            expectedErrors:
            [
                (2, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:property Prop")),
                (4, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:property Prop")),
            ]);

        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:property prop=1
                #:property PROP=2
                """,
            expectedCSharp: "",
            expectedErrors:
            [
                (2, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:property prop")),
            ]);
    }

    [TestMethod]
    public void Directives_DuplicatePackageMetadataNamesAreCaseInsensitive()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package Name@1.0 PrivateAssets=all
                #:package Name@1.0 privateassets=all
                """,
            expectedCSharp: "");
    }

    [TestMethod]
    public void Directives_DuplicatePackageMetadataValuesAreCaseSensitive()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:package Name@1.0 PrivateAssets=all
                #:package Name@1.0 privateassets=ALL
                """,
            expectedCSharp: "",
            expectedErrors:
            [
                (2, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:package Name")),
            ]);
    }

    [TestMethod]
    public void Directives_Duplicate_AcrossIncludedFiles()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        var utilPath = Path.Join(testInstance.Path, "Util.cs");

        File.WriteAllText(utilPath, """
            #:package SomePackage@1.0
            #:property MyProp=Value2
            static class Util { }
            """);

        VerifyConversion(
            inputCSharp: """
                #:include Util.cs
                #:package SomePackage@2.0
                #:property MyProp=Value1
                Console.WriteLine();
                """,
            expectedProject: null,
            expectedCSharp: """
                Console.WriteLine();
                """,
            filePath: programPath,
            expectedErrors: [(utilPath, 1, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:package SomePackage")),
                (utilPath, 2, string.Format(FileBasedProgramsResources.DuplicateDirective, "#:property MyProp"))],
            evaluateDirectives: true);
    }

    [TestMethod] // https://github.com/dotnet/sdk/issues/49797
    public void Directives_VersionedSdkFirst()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        VerifyConversion(
            baseDirectory: testInstance.Path,
            inputCSharp: """
                #:sdk Microsoft.NET.Sdk@9.0.0
                Console.WriteLine();
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk/9.0.0">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                Console.WriteLine();
                """);
    }

    private static string GetProgramPath(string baseDirectory)
    {
        return Path.Join(baseDirectory, "Program.cs");
    }

    private static void Convert(
        string inputCSharp,
        out string actualProject,
        out string? actualCSharp,
        string filePath,
        bool evaluateDirectives,
        out ImmutableArray<SimpleDiagnostic>.Builder? actualDiagnostics)
    {
        var builder = new VirtualProjectBuilder(
            BuildService.Instance,
            entryPointFileFullPath: filePath,
            targetFramework: VirtualProjectBuildingCommand.TargetFramework,
            sourceText: SourceText.From(inputCSharp, Encoding.UTF8));

        var errorReporter = ErrorReporters.CreateCollectingReporter(out actualDiagnostics);

        ImmutableArray<CSharpDirective> directives;
        if (evaluateDirectives)
        {
            var result = builder.CreateProjectInstanceAsync(
                new ProjectCollection().Wrap(),
                errorReporter).AsTask().GetAwaiter().GetResult();
            directives = result.EvaluatedDirectives;
        }
        else
        {
            directives = FileLevelDirectiveHelpers.FindDirectives(
                builder.EntryPointSourceFile,
                reportAllErrors: true,
                errorReporter);
        }

        var projectWriter = new StringWriter();
        VirtualProjectBuilder.WriteProjectFile(
            projectWriter,
            directives,
            VirtualProjectBuilder.GetDefaultProperties(VirtualProjectBuildingCommand.TargetFramework),
            isVirtualProject: false);

        actualProject = projectWriter.ToString();

        var convertedFile = VirtualProjectBuildingCommand.RemoveDirectivesFromFile(builder.EntryPointSourceFile);
        actualCSharp = convertedFile.Text != builder.EntryPointSourceFile.Text ? convertedFile.Text.ToString() : null;
    }

    /// <param name="expectedProject">
    /// <see langword="null"/> means we don't care about the resulting project in this test.
    /// </param>
    /// <param name="expectedCSharp">
    /// <see langword="null"/> means the conversion should not touch the C# content.
    /// </param>
    private static void VerifyConversion(
        string baseDirectory,
        string inputCSharp,
        string? expectedProject = null,
        string? expectedCSharp = null,
        string? filePath = null,
        IEnumerable<(int LineNumber, string Message)>? expectedErrors = null,
        bool evaluateDirectives = false)
    {
        filePath ??= GetProgramPath(baseDirectory);

        VerifyConversion(
            inputCSharp,
            expectedProject,
            expectedCSharp,
            filePath,
            expectedErrors?.Select(e => (filePath, e.LineNumber, e.Message)),
            evaluateDirectives);
    }

    private static void VerifyConversion(
        string inputCSharp,
        string? expectedProject,
        string? expectedCSharp,
        string filePath,
        IEnumerable<(string FilePath, int LineNumber, string Message)>? expectedErrors,
        bool evaluateDirectives)
    {
        Convert(
            inputCSharp,
            out var actualProject,
            out var actualCSharp,
            filePath: filePath,
            evaluateDirectives: evaluateDirectives,
            out var actualDiagnostics);

        if (expectedProject != null) actualProject.Should().Be(expectedProject);
        actualCSharp.Should().Be(expectedCSharp);

        if (actualDiagnostics is null or [])
        {
            Assert.IsNull(expectedErrors);
        }
        else if (expectedErrors is null)
        {
            Assert.IsNull(actualDiagnostics);
        }
        else
        {
            actualDiagnostics.Select(d => (d.Location.Path, d.Location.Span.Start.Line + 1, d.Message)).Should().BeEquivalentTo(expectedErrors);
        }
    }

    [TestMethod]
    public void DeleteSource_WithFlag()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        
        var csFile = Path.Combine(testInstance.Path, "Program.cs");
        File.WriteAllText(csFile, """Console.WriteLine("Test");""");

        new DotnetCommand(Log, "project", "convert", "Program.cs", "--delete-source")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // Source file should be deleted
        File.Exists(csFile).Should().BeFalse();
        
        // Converted files should exist
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.csproj")).Should().BeTrue();
    }

    [TestMethod]
    public void DeleteSource_WithoutFlag_NonInteractive()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        
        var csFile = Path.Combine(testInstance.Path, "Program.cs");
        File.WriteAllText(csFile, """Console.WriteLine("Test");""");

        // Without --delete-source and without --interactive, source file should remain
        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // Source file should still exist
        File.Exists(csFile).Should().BeTrue();
        
        // Converted files should also exist
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.csproj")).Should().BeTrue();
    }

    [TestMethod]
    public void DeleteSource_DryRun()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        
        var csFile = Path.Combine(testInstance.Path, "Program.cs");
        File.WriteAllText(csFile, """Console.WriteLine("Test");""");

        var result = new DotnetCommand(Log, "project", "convert", "Program.cs", "--delete-source", "--dry-run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        result.Should().Pass();
        result.StdOut.Should().Contain("Dry run: would delete source file: " + csFile);

        // Source file should still exist in dry-run mode
        File.Exists(csFile).Should().BeTrue();
        
        // No files should be created in dry-run mode
        Directory.Exists(Path.Join(testInstance.Path, "Program")).Should().BeFalse();
    }

    [TestMethod]
    public void DeleteSource_WithCustomOutput()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        
        var csFile = Path.Combine(testInstance.Path, "Program.cs");
        File.WriteAllText(csFile, """Console.WriteLine("Test");""");

        var outputDir = Path.Combine(testInstance.Path, "CustomOutput");

        new DotnetCommand(Log, "project", "convert", "Program.cs", "-o", outputDir, "--delete-source")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // Source file should be deleted
        File.Exists(csFile).Should().BeFalse();
        
        // Converted files should exist in custom output directory
        File.Exists(Path.Join(outputDir, "Program.cs")).Should().BeTrue();
        File.Exists(Path.Join(outputDir, "Program.csproj")).Should().BeTrue();
    }

    [TestMethod]
    public void DeleteSource_WithDefaultFiles()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        // Create entry point file with default items enabled
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property EnableDefaultNoneItems=true
            #:property EnableDefaultCompileItems=true
            Console.WriteLine("Test");
            """);

        // Create additional default files
        File.WriteAllText(Path.Join(testInstance.Path, "appsettings.json"), "{}");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "class Util { }");

        new DotnetCommand(Log, "project", "convert", "Program.cs", "--delete-source")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // ALL included files should be deleted (both Compile and None items)
        File.Exists(Path.Join(testInstance.Path, "Program.cs")).Should().BeFalse();
        File.Exists(Path.Join(testInstance.Path, "Util.cs")).Should().BeFalse();
        File.Exists(Path.Join(testInstance.Path, "appsettings.json")).Should().BeFalse();

        // All files should be copied to the output directory
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.csproj")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "appsettings.json")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Util.cs")).Should().BeTrue();
    }

    [TestMethod]
    public void DeleteSource_WithDefaultFiles_NotDeleted()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        
        // Create entry point file with default items enabled
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property EnableDefaultNoneItems=true
            #:property EnableDefaultCompileItems=true
            Console.WriteLine("Test");
            """);
        
        // Create additional default files
        File.WriteAllText(Path.Join(testInstance.Path, "appsettings.json"), "{}");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "class Util { }");

        // Without --delete-source, all files should remain
        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // All source files should still exist
        File.Exists(Path.Join(testInstance.Path, "Program.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "appsettings.json")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Util.cs")).Should().BeTrue();
        
        // All files should be copied to the output directory
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.csproj")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "appsettings.json")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Util.cs")).Should().BeTrue();
    }

    [TestMethod]
    public void DeleteSource_WithIncludeDirective()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        // Create entry point file with #:include directive
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property ExperimentalFileBasedProgramEnableIncludeDirective=true
            #:include Util.cs
            Console.WriteLine("Test");
            """);

        // Create included file
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "class Util { }");

        new DotnetCommand(Log, "project", "convert", "Program.cs", "--delete-source")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // ALL source files should be deleted (entry point AND included files)
        File.Exists(Path.Join(testInstance.Path, "Program.cs")).Should().BeFalse();
        File.Exists(Path.Join(testInstance.Path, "Util.cs")).Should().BeFalse();

        // Both files should be copied to the output directory
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.csproj")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Util.cs")).Should().BeTrue();
    }

    [TestMethod]
    public void DeleteSource_WithIncludeDirective_NotDeleted()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        // Create entry point file with #:include directive
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:include Util.cs
            Console.WriteLine("Test");
            """);

        // Create included file
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "class Util { }");

        // Without --delete-source, all files should remain
        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // All source files should still exist
        File.Exists(Path.Join(testInstance.Path, "Program.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Util.cs")).Should().BeTrue();

        // Both files should be copied to the output directory
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.csproj")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Util.cs")).Should().BeTrue();
    }

    [TestMethod]
    public void DeleteSource_WithIncludeDirective_MultipleFiles()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        // Create entry point file with multiple #:include directives
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:include Util.cs
            #:include Helper.cs
            #:include config.json
            Console.WriteLine("Test");
            """);

        // Create included files
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "class Util { }");
        File.WriteAllText(Path.Join(testInstance.Path, "Helper.cs"), "class Helper { }");
        File.WriteAllText(Path.Join(testInstance.Path, "config.json"), "{}");

        new DotnetCommand(Log, "project", "convert", "Program.cs", "--delete-source")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // ALL included files should be deleted (Compile and non-Compile)
        File.Exists(Path.Join(testInstance.Path, "Program.cs")).Should().BeFalse();
        File.Exists(Path.Join(testInstance.Path, "Util.cs")).Should().BeFalse();
        File.Exists(Path.Join(testInstance.Path, "Helper.cs")).Should().BeFalse();
        File.Exists(Path.Join(testInstance.Path, "config.json")).Should().BeFalse();

        // All files should be copied to the output directory
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.csproj")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Util.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Helper.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "config.json")).Should().BeTrue();
    }

    [TestMethod]
    public void DeleteSource_WithIncludeDirective_Transitive()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        // Create entry point file with #:include directive
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:include Util.cs
            Console.WriteLine("Test");
            """);

        // Create included file that itself has #:include directive (transitive)
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), """
            #:include Helper.cs
            class Util { }
            """);

        // Create transitively included file
        File.WriteAllText(Path.Join(testInstance.Path, "Helper.cs"), "class Helper { }");

        new DotnetCommand(Log, "project", "convert", "Program.cs", "--delete-source")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // ALL Compile (source) files should be deleted (entry point, direct, and transitive)
        File.Exists(Path.Join(testInstance.Path, "Program.cs")).Should().BeFalse();
        File.Exists(Path.Join(testInstance.Path, "Util.cs")).Should().BeFalse();
        File.Exists(Path.Join(testInstance.Path, "Helper.cs")).Should().BeFalse();

        // All files should be copied to the output directory
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Program.csproj")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Util.cs")).Should().BeTrue();
        File.Exists(Path.Join(testInstance.Path, "Program", "Helper.cs")).Should().BeTrue();
    }
}
