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
            Description = "The path to the folder containing the old (before) assemblies to be included in the diff.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true
        };

        Option<string> optionBeforeRefAssembliesFolderPath = new(["--refbefore", "-rb"])
        {
            Description = "The path to the folder containing the references required by old (before) assemblies, not to be included in the diff.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = false
        };

        Option<string> optionAfterAssembliesFolderPath = new(["--after", "-a"])
        {
            Description = "The path to the folder containing the new (after) assemblies to be included in the diff.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true
        };

        Option<string> optionAfterRefAssembliesFolderPath = new(["--refafter", "-ra"])
        {
            Description = "The path to the folder containing references required by the new (after) reference assemblies, not to be included in the diff.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = false
        };

        Option<string> optionOutputFolderPath = new(["--output", "-o"])
        {
            Description = "The path to the output folder.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true
        };

        Option<string> optionBeforeFriendlyName = new(["--beforeFriendlyName", "-bfn"])
        {
            Description = "The friendly name to describe the 'before' assembly.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true
        };

        Option<string> optionAfterFriendlyName = new(["--afterFriendlyName", "-afn"])
        {
            Description = "The friendly name to describe the 'after' assembly.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true
        };

        Option<string> optionTableOfContentsTitle = new(["--tableOfContentsTitle", "-tc"], () => "api_diff")
        {
            Description = $"The optional title of the markdown table of contents file that is placed in the output folder.",
            Arity = ArgumentArity.ZeroOrMore,
            IsRequired = true
        };

        Option<string[]?> optionAssembliesToExclude = new(["--assembliesToExclude", "-eas"], () => null)
        {
            Description = "Assemblies from both before and after to exclude from the diff.",
            Arity = ArgumentArity.ZeroOrMore,
            IsRequired = false,
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

        Option<bool> optionAttachDebugger = new(["--attachDebugger", "-d"], () => false)
        {
            Description = "Stops the tool at startup, prints the process ID and waits for a debugger to attach."
        };

        // Custom ordering for the help menu.
        rootCommand.Add(optionBeforeAssembliesFolderPath);
        rootCommand.Add(optionBeforeRefAssembliesFolderPath);
        rootCommand.Add(optionAfterAssembliesFolderPath);
        rootCommand.Add(optionAfterRefAssembliesFolderPath);
        rootCommand.Add(optionOutputFolderPath);
        rootCommand.Add(optionBeforeFriendlyName);
        rootCommand.Add(optionAfterFriendlyName);
        rootCommand.Add(optionTableOfContentsTitle);
        rootCommand.Add(optionAssembliesToExclude);
        rootCommand.Add(optionAttributesToExclude);
        rootCommand.Add(optionApisToExclude);
        rootCommand.Add(optionAddPartialModifier);
        rootCommand.Add(optionAttachDebugger);

        GenAPIDiffConfigurationBinder c = new(optionBeforeAssembliesFolderPath,
                                              optionBeforeRefAssembliesFolderPath,
                                              optionAfterAssembliesFolderPath,
                                              optionAfterRefAssembliesFolderPath,
                                              optionOutputFolderPath,
                                              optionBeforeFriendlyName,
                                              optionAfterFriendlyName,
                                              optionTableOfContentsTitle,
                                              optionAssembliesToExclude,
                                              optionAttributesToExclude,
                                              optionApisToExclude,
                                              optionAddPartialModifier,
                                              optionAttachDebugger);

        rootCommand.SetHandler(async (DiffConfiguration diffConfig) => await HandleCommandAsync(diffConfig).ConfigureAwait(false), c);
        await rootCommand.InvokeAsync(args);
    }

    private static Task HandleCommandAsync(DiffConfiguration diffConfig)
    {
        var log = new ConsoleLog(MessageImportance.Normal);

        string assembliesToExclude = string.Join(", ", diffConfig.AssembliesToExclude ?? []);
        string attributesToExclude = string.Join(", ", diffConfig.AttributesToExclude ?? []);
        string apisToExclude = string.Join(", ", diffConfig.ApisToExclude ?? []);

        // Custom ordering to match help menu.
        log.LogMessage("Selected options:");
        log.LogMessage($" - 'Before' source assemblies:         {diffConfig.BeforeAssembliesFolderPath}");
        log.LogMessage($" - 'After'  source assemblies:         {diffConfig.AfterAssembliesFolderPath}");
        log.LogMessage($" - 'Before' reference assemblies:      {diffConfig.BeforeAssemblyReferencesFolderPath}");
        log.LogMessage($" - 'After'  reference assemblies:      {diffConfig.AfterAssemblyReferencesFolderPath}");
        log.LogMessage($" - Output:                             {diffConfig.OutputFolderPath}");
        log.LogMessage($" - Assemblies to exclude:              {assembliesToExclude}");
        log.LogMessage($" - Attributes to exclude:              {attributesToExclude}");
        log.LogMessage($" - APIs to exclude:                    {apisToExclude}");
        log.LogMessage($" - 'Before' friendly name:             {diffConfig.BeforeFriendlyName}");
        log.LogMessage($" - 'After' friendly name:              {diffConfig.AfterFriendlyName}");
        log.LogMessage($" - Table of contents title:            {diffConfig.TableOfContentsTitle}");
        log.LogMessage($" - Add partial modifier to types:      {diffConfig.AddPartialModifier}");
        log.LogMessage($" - Attach debugger:                    {diffConfig.AttachDebugger}");
        log.LogMessage("");

        if (diffConfig.AttachDebugger)
        {
            WaitForDebugger();
        }

        IDiffGenerator diffGenerator = DiffGeneratorFactory.Create(log,
                                                                   diffConfig.BeforeAssembliesFolderPath,
                                                                   diffConfig.BeforeAssemblyReferencesFolderPath,
                                                                   diffConfig.AfterAssembliesFolderPath,
                                                                   diffConfig.AfterAssemblyReferencesFolderPath,
                                                                   diffConfig.OutputFolderPath,
                                                                   diffConfig.BeforeFriendlyName,
                                                                   diffConfig.AfterFriendlyName,
                                                                   diffConfig.TableOfContentsTitle,
                                                                   diffConfig.AssembliesToExclude,
                                                                   diffConfig.AttributesToExclude,
                                                                   diffConfig.ApisToExclude,
                                                                   diffConfig.AddPartialModifier,
                                                                   writeToDisk: true,
                                                                   diagnosticOptions: null // TODO: If needed, add CLI option to pass specific diagnostic options
                                                                   );

        return diffGenerator.RunAsync();
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
