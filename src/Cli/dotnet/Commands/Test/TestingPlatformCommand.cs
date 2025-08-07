// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.Help;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal partial class TestingPlatformCommand : System.CommandLine.Command, ICustomHelp
{
    private MSBuildHandler _msBuildHandler;
    private TerminalTestReporter _output;
    private TestApplicationActionQueue _actionQueue;
    private TestApplicationsEventHandlers _eventHandlers;

    private byte _cancelled;
    private bool _isDiscovery;
    private bool _isRetry;

    public TestingPlatformCommand(string name, string description = null) : base(name, description)
    {
        TreatUnmatchedTokensAsErrors = false;
    }

    public int Run(ParseResult parseResult)
    {
        int? exitCode = null;
        try
        {
            exitCode = RunInternal(parseResult);
            return exitCode.Value;
        }
        finally
        {
            CompleteRun(exitCode);
            CleanUp();
        }
    }

    private int RunInternal(ParseResult parseResult)
    {
        ValidationUtility.ValidateMutuallyExclusiveOptions(parseResult);

        // Validate arguments for required flags
        var validationResult = ValidateArgumentsForRequiredFlags(parseResult);
        if (validationResult != 0)
        {
            return validationResult;
        }

        PrepareEnvironment(parseResult, out TestOptions testOptions, out int degreeOfParallelism);

        InitializeOutput(degreeOfParallelism, parseResult, testOptions.IsHelp);

        BuildOptions buildOptions = MSBuildUtility.GetBuildOptions(parseResult, degreeOfParallelism);

        InitializeActionQueue(degreeOfParallelism, testOptions, buildOptions);

        _msBuildHandler = new(buildOptions, _actionQueue, _output);
        TestModulesFilterHandler testModulesFilterHandler = new(_actionQueue, _output);

        _eventHandlers = new TestApplicationsEventHandlers(_output);

        if (testOptions.HasFilterMode)
        {
            if (!testModulesFilterHandler.RunWithTestModulesFilter(parseResult))
            {
                return ExitCode.GenericFailure;
            }
        }
        else
        {
            if (!_msBuildHandler.RunMSBuild())
            {
                return ExitCode.GenericFailure;
            }

            if (!_msBuildHandler.EnqueueTestApplications())
            {
                return ExitCode.GenericFailure;
            }
        }

        _actionQueue.EnqueueCompleted();
        return _actionQueue.WaitAllActions();
    }

    private void PrepareEnvironment(ParseResult parseResult, out TestOptions testOptions, out int degreeOfParallelism)
    {
        SetupCancelKeyPressHandler();

        degreeOfParallelism = GetDegreeOfParallelism(parseResult);

        bool filterModeEnabled = parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption);

        var arguments = parseResult.GetArguments();
        testOptions = GetTestOptions(filterModeEnabled, isHelp: ContainsHelpOption(arguments));

        _isDiscovery = ContainsListTestsOption(arguments);

        // This is ugly, and we need to replace it by passing out some info from testing platform to inform us that some process level retry plugin is active.
        _isRetry = arguments.Contains("--retry-failed-tests");
    }

    private void InitializeActionQueue(int degreeOfParallelism, TestOptions testOptions, BuildOptions buildOptions)
    {
        if (testOptions.IsHelp)
        {
            InitializeHelpActionQueue(degreeOfParallelism, testOptions, buildOptions);
        }
        else
        {
            InitializeTestExecutionActionQueue(degreeOfParallelism, testOptions, buildOptions);
        }
    }

    private void SetupCancelKeyPressHandler()
    {
        Console.CancelKeyPress += (s, e) =>
        {
            _output?.StartCancelling();
            // We are not sure what the exit code will be, there might be an exception.
            CompleteRun(exitCode: null);
        };
    }

    private void InitializeOutput(int degreeOfParallelism, ParseResult parseResult, bool isHelp)
    {
        var console = new SystemConsole();
        var showPassedTests = parseResult.GetValue(TestingPlatformOptions.OutputOption) == OutputOptions.Detailed;
        var noProgress = parseResult.HasOption(TestingPlatformOptions.NoProgressOption);
        var noAnsi = parseResult.HasOption(TestingPlatformOptions.NoAnsiOption);

        // TODO: Replace this with proper CI detection that we already have in telemetry. https://github.com/microsoft/testfx/issues/5533#issuecomment-2838893327
        bool inCI = string.Equals(Environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

        _output = new TerminalTestReporter(console, new TerminalTestReporterOptions()
        {
            ShowPassedTests = () => showPassedTests,
            ShowProgress = () => !noProgress,
            UseAnsi = !noAnsi,
            UseCIAnsi = inCI,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        });

        _output.TestExecutionStarted(DateTimeOffset.Now, degreeOfParallelism, _isDiscovery, isHelp, _isRetry);
    }

    private void InitializeHelpActionQueue(int degreeOfParallelism, TestOptions testOptions, BuildOptions buildOptions)
    {
        _actionQueue = new(degreeOfParallelism, buildOptions, async (TestApplication testApp) =>
        {
            testApp.HelpRequested += OnHelpRequested;
            testApp.ErrorReceived += _eventHandlers.OnErrorReceived;
            testApp.TestProcessExited += _eventHandlers.OnTestProcessExited;

            return await testApp.RunAsync(testOptions);
        });
    }

    private void InitializeTestExecutionActionQueue(int degreeOfParallelism, TestOptions testOptions, BuildOptions buildOptions)
    {
        _actionQueue = new(degreeOfParallelism, buildOptions, async (TestApplication testApp) =>
        {
            testApp.HandshakeReceived += _eventHandlers.OnHandshakeReceived;
            testApp.DiscoveredTestsReceived += _eventHandlers.OnDiscoveredTestsReceived;
            testApp.TestResultsReceived += _eventHandlers.OnTestResultsReceived;
            testApp.FileArtifactsReceived += _eventHandlers.OnFileArtifactsReceived;
            testApp.SessionEventReceived += _eventHandlers.OnSessionEventReceived;
            testApp.ErrorReceived += _eventHandlers.OnErrorReceived;
            testApp.TestProcessExited += _eventHandlers.OnTestProcessExited;

            return await testApp.RunAsync(testOptions);
        });
    }

    private static int GetDegreeOfParallelism(ParseResult parseResult)
    {
        if (!int.TryParse(parseResult.GetValue(TestingPlatformOptions.MaxParallelTestModulesOption), out int degreeOfParallelism) || degreeOfParallelism <= 0)
            degreeOfParallelism = Environment.ProcessorCount;
        return degreeOfParallelism;
    }

    private static TestOptions GetTestOptions(bool hasFilterMode, bool isHelp) =>
        new(hasFilterMode, isHelp);

    private static bool ContainsHelpOption(IEnumerable<string> args)
    {
        return args.Contains(TestingPlatformOptions.HelpOption.Name) || TestingPlatformOptions.HelpOption.Aliases.Any(alias => args.Contains(alias));
    }

    private static bool ContainsListTestsOption(IEnumerable<string> args)
    {
        return args.Contains(TestingPlatformOptions.ListTestsOption.Name);
    }

    private void CompleteRun(int? exitCode)
    {
        if (Interlocked.CompareExchange(ref _cancelled, 1, 0) == 0)
        {
            _output?.TestExecutionCompleted(DateTimeOffset.Now, exitCode);
        }
    }

    private void CleanUp()
    {
        _eventHandlers?.Dispose();
    }

    /// <summary>
    /// Validates that arguments requiring specific flags are used correctly for Microsoft Testing Platform.
    /// Provides helpful error messages when users provide file/directory arguments without proper flags.
    /// </summary>
    /// <returns>0 if validation passes, non-zero error code if validation fails</returns>
    private static int ValidateArgumentsForRequiredFlags(ParseResult parseResult)
    {
        // Check unmatched tokens for file/directory arguments that should use flags
        var unmatchedTokens = parseResult.UnmatchedTokens.ToList();
        
        foreach (string token in unmatchedTokens)
        {
            if (token.StartsWith("-"))
            {
                // Skip options/flags
                continue;
            }

            string errorMessage = null;
            string suggestedUsage = null;

            // Check for .sln files
            if (token.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) && File.Exists(token))
            {
                errorMessage = $"Solution file '{token}' was provided as a positional argument.";
                suggestedUsage = $"Use the --solution flag: dotnet test --solution {token}";
            }
            // Check for .csproj/.vbproj/.fsproj files
            else if ((token.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                     token.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                     token.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)) && File.Exists(token))
            {
                errorMessage = $"Project file '{token}' was provided as a positional argument.";
                suggestedUsage = $"Use the --project flag: dotnet test --project {token}";
            }
            // Check for directories (if they exist)
            else if (Directory.Exists(token))
            {
                errorMessage = $"Directory '{token}' was provided as a positional argument.";
                suggestedUsage = $"Use the --directory flag: dotnet test --directory {token}";
            }
            // Check for .dll or .exe files
            else if ((token.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || 
                      token.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) &&
                     File.Exists(token))
            {
                errorMessage = $"Test assembly '{token}' was provided as a positional argument.";
                suggestedUsage = $"Use the --test-modules flag: dotnet test --test-modules {token}";
            }

            if (errorMessage != null && suggestedUsage != null)
            {
                Reporter.Error.WriteLine(errorMessage);
                Reporter.Error.WriteLine(suggestedUsage);
                Reporter.Error.WriteLine("\nFor more information about the available options, run 'dotnet test --help'.");
                return 1;
            }
        }

        return 0;
    }
}
