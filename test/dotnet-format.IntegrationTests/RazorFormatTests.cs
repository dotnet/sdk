// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Format.IntegrationTests;

[TestClass]
public class RazorFormatTests : SdkTest
{
    [TestMethod]
    public void WriteModeFormatsRazorMarkupAndCSharp()
    {
        var projectFile = CreateProject(nameof(WriteModeFormatsRazorMarkupAndCSharp));
        var componentFile = WriteFile(
            projectFile,
            "Component.razor",
            """
            <div>
            <span>Value</span>
            </div>
            @code{
            private int Value{get;set;}
            }
            """);
        Restore(projectFile);

        var result = Format(projectFile);

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        var formatted = NormalizeLineEndings(File.ReadAllText(componentFile));
        Assert.AreEqual(
            NormalizeLineEndings(
                """
                <div>
                    <span>Value</span>
                </div>
                @code {
                    private int Value { get; set; }
                }
                """),
            formatted);
    }

    [TestMethod]
    public void VerifyNoChangesReportsRazorChangesWithoutWriting()
    {
        var projectFile = CreateProject(nameof(VerifyNoChangesReportsRazorChangesWithoutWriting));
        var original = """
            @code{
            private int Value{get;set;}
            }
            """;
        var componentFile = WriteFile(projectFile, "Component.razor", original);
        Restore(projectFile);

        var result = Format(projectFile, "--verify-no-changes");

        Assert.AreEqual(2, result.ExitCode, result.StdErr);
        Assert.AreEqual(original, File.ReadAllText(componentFile));
    }

    [TestMethod]
    public void IncludeAndExcludeFilterRazorDocuments()
    {
        var projectFile = CreateProject(nameof(IncludeAndExcludeFilterRazorDocuments));
        var original = """
            @code{
            private int Value{get;set;}
            }
            """;
        var includedFile = WriteFile(projectFile, "Included.razor", original);
        var excludedFile = WriteFile(projectFile, "Excluded.razor", original);
        Restore(projectFile);

        var result = Format(
            projectFile,
            "--include",
            "Included.razor",
            "Excluded.razor",
            "--exclude",
            "Excluded.razor");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("@code {", File.ReadAllText(includedFile));
        Assert.AreEqual(original, File.ReadAllText(excludedFile));
    }

    [TestMethod]
    public void NestedEditorConfigControlsRazorFormatting()
    {
        var projectFile = CreateProject(nameof(NestedEditorConfigControlsRazorFormatting));
        WriteFile(
            projectFile,
            ".editorconfig",
            """
            root = true

            [*.razor]
            csharp_space_after_keywords_in_control_flow_statements = true
            """);
        var nestedComponentFile = WriteFile(
            projectFile,
            Path.Combine("Nested", "Component.razor"),
            """
            @code{
            private void Method(){
            if (true){
            }
            }
            }
            """);
        WriteFile(
            projectFile,
            Path.Combine("Nested", ".editorconfig"),
            """
            [*.razor]
            csharp_space_after_keywords_in_control_flow_statements = false
            """);
        Restore(projectFile);

        var result = Format(projectFile);

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("if(true)", File.ReadAllText(nestedComponentFile));
    }

    [TestMethod]
    public void MalformedRazorDoesNotFailFormatting()
    {
        var projectFile = CreateProject(nameof(MalformedRazorDoesNotFailFormatting));
        var componentFile = WriteFile(
            projectFile,
            "Component.razor",
            """
            @code {
                private void Method(
            }
            """);
        var validComponentFile = WriteFile(
            projectFile,
            "ValidComponent.razor",
            """
            @code{
            private int Value{get;set;}
            }
            """);
        Restore(projectFile);

        var result = Format(projectFile);

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("private void Method(", File.ReadAllText(componentFile));
        Assert.Contains("    private int Value { get; set; }", File.ReadAllText(validComponentFile));
    }

    [TestMethod]
    public void NonRazorAdditionalFilesAreNotFormatted()
    {
        var projectFile = CreateProject(
            nameof(NonRazorAdditionalFilesAreNotFormatted),
            includeNonRazorAdditionalFile: true);
        var original = """
            @code{
            private int Value{get;set;}
            }
            """;
        var additionalFile = WriteFile(projectFile, "Additional.txt", original);
        Restore(projectFile);

        var result = Format(projectFile);

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.AreEqual(original, File.ReadAllText(additionalFile));
    }

    [TestMethod]
    public void MultiTargetedRazorDocumentIsReportedOnce()
    {
        var projectFile = CreateProject(
            nameof(MultiTargetedRazorDocumentIsReportedOnce),
            multiTarget: true);
        var componentFile = WriteFile(
            projectFile,
            "Component.razor",
            """
            @code{
            private int Value{get;set;}
            }
            """);
        var solutionFile = Path.Combine(Path.GetDirectoryName(projectFile)!, "RazorFormat.slnx");
        File.WriteAllText(
            solutionFile,
            $"""
            <Solution>
              <Project Path="{Path.GetFileName(projectFile)}" />
            </Solution>
            """);
        Restore(solutionFile);

        var reportFile = Path.Combine(Path.GetDirectoryName(projectFile)!, "format-report.json");
        var result = Format(
            solutionFile,
            "--verify-no-changes",
            "--report",
            reportFile,
            "--verbosity",
            "diagnostic");

        Assert.AreEqual(2, result.ExitCode, result.StdErr);
        using var report = JsonDocument.Parse(File.ReadAllText(reportFile));
        Assert.AreEqual(1, report.RootElement.GetArrayLength());
        Assert.AreEqual(componentFile, report.RootElement[0].GetProperty("FilePath").GetString());
    }

    private string CreateProject(string identifier, bool multiTarget = false, bool includeNonRazorAdditionalFile = false)
    {
        var projectDirectory = TestAssetsManager.CreateTestDirectory(identifier: identifier).Path;
        var projectFile = Path.Combine(projectDirectory, "RazorFormat.csproj");
        var targetFrameworkProperty = multiTarget
            ? $"<TargetFrameworks>{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}</TargetFrameworks>"
            : $"<TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>";
        var additionalFiles = includeNonRazorAdditionalFile
            ? """
              <ItemGroup>
                <AdditionalFiles Include="Additional.txt" />
              </ItemGroup>
            """
            : string.Empty;

        File.WriteAllText(
            projectFile,
            $"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                {targetFrameworkProperty}
                <RazorLangVersion>Latest</RazorLangVersion>
              </PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
            {additionalFiles}
            </Project>
            """);

        return projectFile;
    }

    private static string WriteFile(string projectFile, string relativePath, string contents)
    {
        var filePath = Path.Combine(Path.GetDirectoryName(projectFile)!, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, contents);
        return filePath;
    }

    private void Restore(string workspaceFile)
    {
        var result = new DotnetCommand(Log, "restore", workspaceFile)
            .WithWorkingDirectory(Path.GetDirectoryName(workspaceFile)!)
            .Execute();

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
    }

    private CommandResult Format(string workspaceFile, params string[] additionalArguments)
    {
        var arguments = new List<string>
        {
            "format",
            "whitespace",
            workspaceFile,
            "--no-restore",
        };
        arguments.AddRange(additionalArguments);

        return new DotnetCommand(Log, arguments.ToArray())
            .WithWorkingDirectory(Path.GetDirectoryName(workspaceFile)!)
            .Execute();
    }

    private static string NormalizeLineEndings(string value)
        => value.Replace("\r\n", "\n");
}
