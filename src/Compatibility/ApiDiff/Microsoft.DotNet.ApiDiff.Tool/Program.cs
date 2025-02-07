// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

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

        Option<string> optionOutputFolderPath = new(["--output", "-o"])
        {
            Description = "The path to the output folder.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true
        };

        Option<string> optionTableOfContentsTitle = new(["--tableOfContentsTitle", "-tc"])
        {
            Description = "The title of the markdown file that is placed in the output folder with a table of contents.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true
        };

        Option<string[]?> optionAttributesToExclude = new(["--attributesToExclude", "-eattrs"], () => null)
        {
            Description = "Attributes to exclude from the diff.",
            Arity = ArgumentArity.ZeroOrMore,
            IsRequired = false
        };

        Option<string[]?> optionApisToExclude = new(["--apisToExclude", "-eapis"], () => null)
        {
            Description = "APIs to exclude from the diff.",
            Arity = ArgumentArity.ZeroOrMore,
            IsRequired = false
        };

        Option<bool> optionAddPartialModifier = new(["--addPartialModifier", "-apm"], () => false)
        {
            Description = "Add the 'partial' modifier to types."
        };

        Option<bool> optionHideImplicitDefaultConstructors = new(["--hideImplicitDefaultConstructors", "-hidc"], () => false)
        {
            Description = "Hide implicit default constructors from types."
        };

        Option<bool> optionDebug = new(["--attachDebugger", "-d"], () => false)
        {
            Description = "Stops the tool at startup, prints the process ID and waits for a debugger to attach."
        };

        // Custom ordering for the help menu.
        rootCommand.Add(optionBeforeAssembliesFolderPath);
        rootCommand.Add(optionBeforeRefAssembliesFolderPath);
        rootCommand.Add(optionAfterAssembliesFolderPath);
        rootCommand.Add(optionAfterRefAssembliesFolderPath);
        rootCommand.Add(optionOutputFolderPath);
        rootCommand.Add(optionTableOfContentsTitle);
        rootCommand.Add(optionAttributesToExclude);
        rootCommand.Add(optionApisToExclude);
        rootCommand.Add(optionAddPartialModifier);
        rootCommand.Add(optionHideImplicitDefaultConstructors);
        rootCommand.Add(optionDebug);

        GenAPIDiffConfigurationBinder c = new(optionBeforeAssembliesFolderPath,
                                              optionBeforeRefAssembliesFolderPath,
                                              optionAfterAssembliesFolderPath,
                                              optionAfterRefAssembliesFolderPath,
                                              optionOutputFolderPath,
                                              optionTableOfContentsTitle,
                                              optionAttributesToExclude,
                                              optionApisToExclude,
                                              optionAddPartialModifier,
                                              optionHideImplicitDefaultConstructors,
                                              optionDebug);

        rootCommand.SetHandler(HandleCommand, c);
        await rootCommand.InvokeAsync(args);
    }

    private static void HandleCommand(DiffConfiguration diffConfig)
    {
        var log = new ConsoleLog(MessageImportance.Normal);

        string attributesToExclude = diffConfig.AttributesToExclude != null ? string.Join(", ", diffConfig.AttributesToExclude) : string.Empty;
        string apisToExclude = diffConfig.ApisToExclude != null ? string.Join(", ", diffConfig.ApisToExclude) : string.Empty;

        // Custom ordering to match help menu.
        log.LogMessage("Selected options:");
        log.LogMessage($" - 'Before' source assemblies:         {diffConfig.BeforeAssembliesFolderPath}");
        log.LogMessage($" - 'After'  source assemblies:         {diffConfig.AfterAssembliesFolderPath}");
        log.LogMessage($" - 'Before' reference assemblies:      {diffConfig.BeforeAssemblyReferencesFolderPath}");
        log.LogMessage($" - 'After'  reference assemblies:      {diffConfig.AfterAssemblyReferencesFolderPath}");
        log.LogMessage($" - Output:                             {diffConfig.OutputFolderPath}");
        log.LogMessage($" - Attributes to exclude:              {attributesToExclude}");
        log.LogMessage($" - APIs to exclude:                    {apisToExclude}");
        log.LogMessage($" - Table of contents title:            {diffConfig.TableOfContentsTitle}");
        log.LogMessage($" - Add partial modifier to types:      {diffConfig.AddPartialModifier}");
        log.LogMessage($" - Hide implicit default constructors: {diffConfig.HideImplicitDefaultConstructors}");
        log.LogMessage($" - Debug:                              {diffConfig.Debug}");
        log.LogMessage("");

        if (diffConfig.Debug)
        {
            WaitForDebugger();
        }

        IDiffGenerator diffGenerator = DiffGeneratorFactory.Create(log,
                                                                   diffConfig.BeforeAssembliesFolderPath,
                                                                   diffConfig.BeforeAssemblyReferencesFolderPath,
                                                                   diffConfig.AfterAssembliesFolderPath,
                                                                   diffConfig.AfterAssemblyReferencesFolderPath,
                                                                   diffConfig.OutputFolderPath,
                                                                   diffConfig.TableOfContentsTitle,
                                                                   diffConfig.AttributesToExclude,
                                                                   diffConfig.ApisToExclude,
                                                                   diffConfig.AddPartialModifier,
                                                                   diffConfig.HideImplicitDefaultConstructors,
                                                                   writeToDisk: true,
                                                                   diagnosticOptions: null // TODO: If needed, add CLI option to pass specific diagnostic options
                                                                   );

        diffGenerator.Run();
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
