// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.GenAPI;

namespace Microsoft.DotNet.ApiDiff.Tool;

/// <summary>
/// Entrypoint for the genapidiff tool, which generates a markdown diff of two
/// different versions of the same assembly, using the specified command line options.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        RootCommand rootCommand = new("genapidiff");

        Option<bool> optionAddPartialModifier = new(["--addPartialModifier", "-apm"], () => false)
        {
            Description = "Add the 'partial' modifier to types."
        };

        Option<string> optionAfterAssembliesFolderPath = new(["--after", "-a"])
        {
            Description = "The path to the folder containing the new (after) assemblies.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = false
        };

        Option<string> optionAfterRefAssembliesFolderPath = new(["--refafter", "-ra"])
        {
            Description = "The path to the folder containing the new (after) reference assemblies.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = false
        };

        Option<string[]?> optionAttributesToExclude = new(["--attributesToExclude", "-ate"], () => null)
        {
            Description = "Attributes to exclude from the diff.",
            Arity = ArgumentArity.ZeroOrMore,
            IsRequired = false
        };

        Option<string> optionBeforeAssembliesFolderPath = new(["--before", "-b"])
        {
            Description = "The path to the folder containing the old (before) assemblies.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true
        };

        Option<string> optionBeforeRefAssembliesFolderPath = new(["--refbefore", "-rb"])
        {
            Description = "The path to the folder containing the old (before) reference assemblies.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = false
        };

        Option<bool> optionDebug = new(["--debug"], () => false)
        {
            Description = "Stops the tool at startup, prints the process ID and waits for a debugger to attach."
        };

        Option<bool> optionHideImplicitDefaultConstructors = new(["--hideImplicitDefaultConstructors", "-hidc"], () => false)
        {
            Description = "Hide implicit default constructors from types."
        };

        Option<bool> optionIncludeTableOfContents = new(["--includeTableOfContents", "-toc"], () => true)
        {
            Description = "Include a markdown file at the root output folder with a table of contents."
        };

        Option<string> optionOutputFolderPath = new(["--output", "-o"])
        {
            Description = "The path to the output folder.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true
        };

        // Custom ordering for the help menu.
        rootCommand.Add(optionBeforeAssembliesFolderPath);
        rootCommand.Add(optionBeforeRefAssembliesFolderPath);
        rootCommand.Add(optionAfterAssembliesFolderPath);
        rootCommand.Add(optionAfterRefAssembliesFolderPath);
        rootCommand.Add(optionOutputFolderPath);
        rootCommand.Add(optionAttributesToExclude);
        rootCommand.Add(optionIncludeTableOfContents);
        rootCommand.Add(optionAddPartialModifier);
        rootCommand.Add(optionHideImplicitDefaultConstructors);
        rootCommand.Add(optionDebug);

        GenAPIDiffConfigurationBinder c = new(optionAddPartialModifier,
                                              optionAfterAssembliesFolderPath,
                                              optionAfterRefAssembliesFolderPath,
                                              optionAttributesToExclude,
                                              optionBeforeAssembliesFolderPath,
                                              optionBeforeRefAssembliesFolderPath,
                                              optionDebug,
                                              optionHideImplicitDefaultConstructors,
                                              optionIncludeTableOfContents,
                                              optionOutputFolderPath);

        rootCommand.SetHandler(HandleCommand, c);
        await rootCommand.InvokeAsync(args);
    }

    private static void HandleCommand(DiffConfiguration diffConfig)
    {
        var log = new ConsoleLog(MessageImportance.Normal);

        string attributesToExclude = diffConfig.AttributesToExclude != null ? string.Join(", ", diffConfig.AttributesToExclude) : string.Empty;

    // Custom ordering to match help menu.
    log.LogMessage("Selected options:");
        log.LogMessage($" - 'Before' assemblies:                {diffConfig.BeforeAssembliesFolderPath}");
        log.LogMessage($" - 'Before' reference assemblies:      {diffConfig.BeforeAssemblyReferencesFolderPath}");
        log.LogMessage($" - 'After' assemblies:                 {diffConfig.AfterAssembliesFolderPath}");
        log.LogMessage($" - 'After' ref assemblies:             {diffConfig.AfterAssemblyReferencesFolderPath}");
        log.LogMessage($" - Output:                             {diffConfig.OutputFolderPath}");
        log.LogMessage($" - Attributes to exclude:              {attributesToExclude}");
        log.LogMessage($" - Include table of contents:          {diffConfig.IncludeTableOfContents}");
        log.LogMessage($" - Add partial modifier to types:      {diffConfig.AddPartialModifier}");
        log.LogMessage($" - Hide implicit default constructors: {diffConfig.HideImplicitDefaultConstructors}");
        log.LogMessage($" - Debug:                              {diffConfig.Debug}");
        log.LogMessage("");

        if (diffConfig.Debug)
        {
            WaitForDebugger();
        }

        Dictionary<string, string> results = DiffGenerator.Run(log,
            diffConfig.AttributesToExclude,
            diffConfig.BeforeAssembliesFolderPath,
            diffConfig.BeforeAssemblyReferencesFolderPath,
            diffConfig.AfterAssembliesFolderPath,
            diffConfig.AfterAssemblyReferencesFolderPath,
            diffConfig.AddPartialModifier,
            diffConfig.HideImplicitDefaultConstructors);

        Directory.CreateDirectory(diffConfig.OutputFolderPath);
        foreach ((string assemblyName, string text) in results)
        {
            string filePath = Path.Combine(diffConfig.OutputFolderPath, $"{assemblyName}.md");
            File.WriteAllText(filePath, text);
            log.LogMessage($"Wrote '{filePath}'.");
        }
    }

    private static void WaitForDebugger()
    {
        while (!Debugger.IsAttached)
        {
            Console.WriteLine($"Attach to process {Environment.ProcessId}...");
            Thread.Sleep(1000);
        }
        Console.WriteLine("Debugger attached!");
        Debugger.Break();
    }
}
