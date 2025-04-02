// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.Cli.Project.Convert.Tests;

public sealed class DotnetProjectConvertTests(ITestOutputHelper log) : SdkTest(log)
{
    /// <summary>
    /// <c>dotnet project convert</c> should result in the same project file text as <c>dotnet new console</c>.
    /// If this test fails, <c>dotnet project convert</c> command implementation should be updated.
    /// </summary>
    [Fact]
    public void SameAsTemplate()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

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
            .Should().BeEquivalentTo(["Program"]);

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
        dotnetProjectConvertProjectText.Should().Be(dotnetNewConsoleProjectText)
            .And.StartWith("""<Project Sdk="Microsoft.NET.Sdk">""");
    }

    [Fact]
    public void DirectoryAlreadyExists()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        Directory.CreateDirectory(Path.Join(testInstance.Path, "MyApp"));
        File.WriteAllText(Path.Join(testInstance.Path, "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "MyApp.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("The target directory already exists");

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["MyApp", "MyApp.cs"]);
    }

    [Fact]
    public void OutputOption()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
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

    [Fact]
    public void OutputOption_DirectoryAlreadyExists()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        Directory.CreateDirectory(Path.Join(testInstance.Path, "SomeOutput"));
        File.WriteAllText(Path.Join(testInstance.Path, "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "MyApp.cs", "-o", "SomeOutput")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("The target directory already exists");

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["MyApp.cs", "SomeOutput"]);
    }

    [Fact]
    public void MultipleEntryPointFiles()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program1.cs"), "Console.WriteLine(1);");
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), "Console.WriteLine(2);");

        new DotnetCommand(Log, "project", "convert", "Program1.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["Program1", "Program2.cs"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program1"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program1.csproj", "Program1.cs"]);
    }

    [Fact]
    public void NoFileArgument()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        new DotnetCommand(Log, "project", "convert")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("Required argument missing for command");

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Should().BeEmpty();
    }

    [Fact]
    public void NonExistentFile()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        new DotnetCommand(Log, "project", "convert", "NotHere.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("The specified file must exist");

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Should().BeEmpty();
    }

    [Fact]
    public void NonCSharpFile()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.vb"), "");

        new DotnetCommand(Log, "project", "convert", "Program.vb")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("The specified file must exist and have '.cs' file extension");

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.vb"]);
    }

    [Fact]
    public void ExtensionCasing()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.CS"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "Program.CS")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.CS"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("class C;")]
    public void FileContent(string content)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), content);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.cs"))
            .Should().Be(content);
    }

    [Fact]
    public void NestedDirectory()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var appDirectory = Path.Join(testInstance.Path, "app");
        Directory.CreateDirectory(appDirectory);
        File.WriteAllText(Path.Join(appDirectory, "Program.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "app/Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(Path.Join(testInstance.Path, "app"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "app", "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);
    }

    /// <summary>
    /// When processing fails due to invalid directives, no conversion should be performed
    /// (e.g., the target directory should not be created).
    /// </summary>
    [Fact]
    public void ProcessingFails()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), "#:invalid");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("Unrecognized directive 'invalid' at");

        new DirectoryInfo(Path.Join(testInstance.Path))
            .EnumerateDirectories().Should().BeEmpty();
    }

    /// <summary>
    /// End-to-end test of directive processing. More cases are covered by faster unit tests below.
    /// </summary>
    [Fact]
    public void ProcessingSucceeds()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:sdk Aspire.Hosting.Sdk/9.1.0
            Console.WriteLine();
            """);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.cs"))
            .Should().Be("Console.WriteLine();");

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Be("""
                <Project Sdk="Aspire.Hosting.Sdk/9.1.0">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                </Project>

                """);
    }

    [Fact]
    public void Directives()
    {
        VerifyConversion(
            inputCSharp: """
                #!/program
                #:sdk Microsoft.NET.Sdk
                #:sdk Aspire.Hosting.Sdk 9.1.0
                #:property TargetFramework net11.0
                #:package System.CommandLine 2.0.0-beta4.22272.1
                #:property LangVersion preview
                Console.WriteLine();
                """,
            expectedProject: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <Sdk Name="Aspire.Hosting.Sdk" Version="9.1.0" />

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <TargetFramework>net11.0</TargetFramework>
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

    [Fact]
    public void Directives_Variable()
    {
        VerifyConversion(
            inputCSharp: """
                #:package MyPackage $(MyProp)
                #:property MyProp MyValue
                """,
            expectedProject: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <MyProp>MyValue</MyProp>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="MyPackage" Version="$(MyProp)" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [Fact]
    public void Directives_Separators()
    {
        VerifyConversion(
            inputCSharp: """
                #:property Prop1   One=a/b
                #:property Prop2   Two/a=b
                #:sdk First 1.0=a/b
                #:sdk Second 2.0/a=b
                #:sdk Third 3.0=a/b
                #:package P1 1.0/a=b
                #:package P2 2.0/a=b
                """,
            expectedProject: """
                <Project Sdk="First/1.0=a/b">

                  <Sdk Name="Second" Version="2.0/a=b" />
                  <Sdk Name="Third" Version="3.0=a/b" />

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <Prop1>One=a/b</Prop1>
                    <Prop2>Two/a=b</Prop2>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="P1" Version="1.0/a=b" />
                    <PackageReference Include="P2" Version="2.0/a=b" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("SDK")]
    public void Directives_Unknown(string directive)
    {
        VerifyConversionThrows(
            inputCSharp: $"""
                #:sdk Test
                #:{directive} Test
                """,
            expectedWildcardPattern: $"Unrecognized directive '{directive}' at /app/Program.cs:2.");
    }

    [Fact]
    public void Directives_Empty()
    {
        VerifyConversionThrows(
            inputCSharp: """
                #:
                #:sdk Test
                """,
            expectedWildcardPattern: "Unrecognized directive '' at /app/Program.cs:1.");
    }

    [Theory, CombinatorialData]
    public void Directives_EmptyName(
        [CombinatorialValues("sdk", "property", "package")] string directive,
        [CombinatorialValues(" ", "")] string value)
    {
        VerifyConversionThrows(
            inputCSharp: $"""
                #:{directive}{value}
                """,
            expectedWildcardPattern: $"Missing name of '{directive}' at /app/Program.cs:1.");
    }

    [Fact]
    public void Directives_MissingPropertyValue()
    {
        VerifyConversionThrows(
            inputCSharp: """
                #:property Test
                """,
            expectedWildcardPattern: "The property directive needs to have two parts separated by '=' like 'PropertyName=PropertyValue': /app/Program.cs:1");
    }

    [Fact]
    public void Directives_InvalidPropertyName()
    {
        VerifyConversionThrows(
            inputCSharp: """
                #:property Name" Value
                """,
            expectedWildcardPattern: """
                Invalid property name at /app/Program.cs:1. The '"' character, hexadecimal value 0x22, cannot be included in a name.
                """);
    }

    [Fact]
    public void Directives_Escaping()
    {
        VerifyConversion(
            inputCSharp: """
                #:property Prop <test">
                #:sdk <test"> ="<>test
                #:package <test"> ="<>test
                """,
            expectedProject: """
                <Project Sdk="&lt;test&quot;&gt;/=&quot;&lt;&gt;test">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <Prop>&lt;test&quot;&gt;</Prop>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="&lt;test&quot;&gt;" Version="=&quot;&lt;&gt;test" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [Fact]
    public void Directives_Whitespace()
    {
        VerifyConversion(
            inputCSharp: """
                 #  !  /test
                  #!  /program   x   
                    #:   sdk   TestSdk
                #:property Name   Value   
                #:property NugetPackageDescription "My package with spaces"
                 # :property Name Value
                """,
            expectedProject: """
                <Project Sdk="TestSdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <Name>Value</Name>
                    <NugetPackageDescription>&quot;My package with spaces&quot;</NugetPackageDescription>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                 #  !  /test
                 # :property Name Value
                """);
    }

    [Fact]
    public void Directives_Whitespace_Invalid()
    {
        VerifyConversionThrows(
            inputCSharp: $"""
                #:   property   Name{'\t'}     Value
                """,
            expectedWildcardPattern: "Invalid property name at /app/Program.cs:1. The '\t' character, hexadecimal value 0x09, cannot be included in a name.");
    }

    private static void Convert(string inputCSharp, out string actualProject, out string? actualCSharp)
    {
        var sourceFile = new SourceFile("/app/Program.cs", SourceText.From(inputCSharp, Encoding.UTF8));
        var directives = VirtualProjectBuildingCommand.FindDirectives(sourceFile);
        var projectWriter = new StringWriter();
        VirtualProjectBuildingCommand.WriteProjectFile(projectWriter, directives);
        actualProject = projectWriter.ToString();
        actualCSharp = VirtualProjectBuildingCommand.RemoveDirectivesFromFile(directives, sourceFile.Text)?.ToString();
    }

    /// <param name="expectedCSharp">
    /// <see langword="null"/> means the conversion should not touch the C# content.
    /// </param>
    private static void VerifyConversion(string inputCSharp, string expectedProject, string? expectedCSharp)
    {
        Convert(inputCSharp, out var actualProject, out var actualCSharp);
        actualProject.Should().Be(expectedProject);
        actualCSharp.Should().Be(expectedCSharp);
    }

    private static void VerifyConversionThrows(string inputCSharp, string expectedWildcardPattern)
    {
        var convert = () => Convert(inputCSharp, out _, out _);
        convert.Should().Throw<GracefulException>().WithMessage(expectedWildcardPattern);
    }
}
