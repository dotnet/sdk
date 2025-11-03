// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.DotNet.Cli.Utils;
using Spectre.Console;

namespace Microsoft.DotNet.Cli.Commands.Run;

internal static class TargetFrameworkSelector
{
    /// <summary>
    /// Evaluates the project to determine if target framework selection is needed.
    /// If the project has multiple target frameworks and none was specified, prompts the user to select one.
    /// </summary>
    /// <param name="projectFilePath">Path to the project file</param>
    /// <param name="globalProperties">Global properties for MSBuild evaluation</param>
    /// <param name="isInteractive">Whether we're running in interactive mode (can prompt user)</param>
    /// <param name="selectedFramework">The selected target framework, or null if not needed</param>
    /// <returns>True if we should continue, false if we should exit with error</returns>
    public static bool TrySelectTargetFramework(
        string projectFilePath,
        Dictionary<string, string> globalProperties,
        bool isInteractive,
        out string? selectedFramework)
    {
        selectedFramework = null;

        // If a framework is already specified, no need to prompt
        if (globalProperties.TryGetValue("TargetFramework", out var existingFramework) && !string.IsNullOrWhiteSpace(existingFramework))
        {
            return true;
        }

        // Evaluate the project to get TargetFrameworks
        Microsoft.Build.Evaluation.Project project;
        try
        {
            using var collection = new ProjectCollection(globalProperties: globalProperties);
            project = collection.LoadProject(projectFilePath);
        }
        catch (InvalidProjectFileException)
        {
            // Invalid project file, return true to continue for normal error handling
            return true;
        }

        string targetFrameworks = project.GetPropertyValue("TargetFrameworks");

        // If there's no TargetFrameworks property or only one framework, no selection needed
        if (string.IsNullOrWhiteSpace(targetFrameworks))
        {
            return true;
        }

        // parse the TargetFrameworks property and make sure to account for any additional whitespace
        // users may have added for formatting reasons.
        var frameworks = targetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // If there's only one framework in the TargetFrameworks, we do need to pick it to force the subsequent builds/evaluations
        // to act against the correct 'view' of the project
        if (frameworks.Length == 1)
        {
            selectedFramework = frameworks[0];
            return true;
        }

        if (isInteractive)
        {
            selectedFramework = PromptForTargetFramework(frameworks);
            return selectedFramework != null;
        }
        else
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"));
            Reporter.Error.WriteLine();
            Reporter.Error.WriteLine(CliCommandStrings.RunCommandAvailableTargetFrameworks);
            Reporter.Error.WriteLine();

            for (int i = 0; i < frameworks.Length; i++)
            {
                Reporter.Error.WriteLine($"  {i + 1}. {frameworks[i]}");
            }

            Reporter.Error.WriteLine();
            Reporter.Error.WriteLine($"{CliCommandStrings.RunCommandExampleText}: dotnet run --framework {frameworks[0]}");
            Reporter.Error.WriteLine();
            return false;
        }
    }

    /// <summary>
    /// Prompts the user to select a target framework from the available options using Spectre.Console.
    /// </summary>
    private static string? PromptForTargetFramework(string[] frameworks)
    {
        try
        {
            var prompt = new SelectionPrompt<string>()
                .Title($"[cyan]{Markup.Escape(CliCommandStrings.RunCommandSelectTargetFrameworkPrompt)}[/]")
                .PageSize(10)
                .MoreChoicesText($"[grey]({Markup.Escape(CliCommandStrings.RunCommandMoreFrameworksText)})[/]")
                .AddChoices(frameworks)
                .EnableSearch()
                .SearchPlaceholderText(CliCommandStrings.RunCommandSearchPlaceholderText);

            return Spectre.Console.AnsiConsole.Prompt(prompt);
        }
        catch (Exception)
        {
            // If Spectre.Console fails (e.g., terminal doesn't support it), return null
            return null;
        }
    }
}
