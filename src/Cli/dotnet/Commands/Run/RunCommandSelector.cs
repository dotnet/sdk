// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Utils;
using Spectre.Console;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Handles target framework and device selection for dotnet run.
/// Caches the project instance to avoid reloading it multiple times.
/// </summary>
internal sealed class RunCommandSelector : IDisposable
{
    // Spectre.Console markup color constants
    private const string CyanMarkup = "[cyan]";
    private const string GrayMarkup = "[gray]";
    private const string EndMarkup = "[/]";

    private readonly string _projectFilePath;
    private readonly Dictionary<string, string> _globalProperties;
    private readonly FacadeLogger? _binaryLogger;
    private readonly bool _isInteractive;
    private readonly MSBuildArgs _msbuildArgs;
    
    private ProjectCollection? _collection;
    private Microsoft.Build.Evaluation.Project? _project;

    /// <summary>
    /// Gets whether the selector has a valid project that can be evaluated.
    /// This is false for .sln files or other invalid project files.
    /// </summary>
    public bool HasValidProject { get; private set; }

    /// <param name="projectFilePath">Path to the project file to evaluate</param>
    /// <param name="isInteractive">Whether to prompt the user for selections</param>
    /// <param name="msbuildArgs">MSBuild arguments containing properties and verbosity settings</param>
    /// <param name="binaryLogger">Optional binary logger for MSBuild operations. The logger will not be disposed by this class.</param>
    public RunCommandSelector(
        string projectFilePath,
        bool isInteractive,
        MSBuildArgs msbuildArgs,
        FacadeLogger? binaryLogger = null)
    {
        _projectFilePath = projectFilePath;
        _globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs);
        _isInteractive = isInteractive;
        _msbuildArgs = msbuildArgs;
        _binaryLogger = binaryLogger;
    }

    /// <summary>
    /// Evaluates the project to determine if target framework selection is needed.
    /// If the project has multiple target frameworks and none was specified, prompts the user to select one.
    /// </summary>
    /// <param name="selectedFramework">The selected target framework, or null if not needed</param>
    /// <returns>True if we should continue, false if we should exit with error</returns>
    public bool TrySelectTargetFramework(out string? selectedFramework)
    {
        selectedFramework = null;

        // If a framework is already specified, no need to prompt
        if (_globalProperties.TryGetValue("TargetFramework", out var existingFramework) && !string.IsNullOrWhiteSpace(existingFramework))
        {
            return true;
        }

        // Evaluate the project to get TargetFrameworks
        if (!OpenProjectIfNeeded(out var projectInstance))
        {
            // Invalid project file, return true to continue for normal error handling
            return true;
        }
        string targetFrameworks = projectInstance.GetPropertyValue("TargetFrameworks");

        // If there's no TargetFrameworks property or only one framework, no selection needed
        if (string.IsNullOrWhiteSpace(targetFrameworks))
        {
            return true;
        }

        // parse the TargetFrameworks property and make sure to account for any additional whitespace
        // users may have added for formatting reasons.
        var frameworks = targetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return TrySelectTargetFramework(frameworks, _isInteractive, out selectedFramework);
    }

    /// <summary>
    /// Invalidates the loaded project with updated global properties.
    /// This is needed after framework selection to get the correct device list for that framework.
    /// </summary>
    public void InvalidateGlobalProperties(Dictionary<string, string> updatedProperties)
    {
        // Update our stored global properties
        foreach (var (key, value) in updatedProperties)
        {
            _globalProperties[key] = value;
        }

        // Dispose existing project to force re-evaluation
        _project = null;
        _collection?.Dispose();
        _collection = null;
        HasValidProject = false;
    }

    /// <summary>
    /// Opens the project if it hasn't been opened yet.
    /// </summary>
    private bool OpenProjectIfNeeded([NotNullWhen(true)] out ProjectInstance? projectInstance)
    {
        if (_project is not null)
        {
            // Create a fresh ProjectInstance for each build operation
            // to avoid accumulating state (existing item groups) from previous builds
            projectInstance = _project.CreateProjectInstance();
            HasValidProject = true;
            return true;
        }

        try
        {
            _collection = new ProjectCollection(
                globalProperties: _globalProperties,
                loggers: GetLoggers(),
                toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);
            _project = _collection.LoadProject(_projectFilePath);
            projectInstance = _project.CreateProjectInstance();
            HasValidProject = true;
            return true;
        }
        catch (InvalidProjectFileException)
        {
            // Invalid project file, return false
            projectInstance = null;
            HasValidProject = false;
            return false;
        }
    }

    public void Dispose()
    {
        // NOTE: _binaryLogger is not disposed here because it is *owned* by the caller
        _collection?.Dispose();
    }

    /// <summary>
    /// Handles target framework selection when given an array of frameworks.
    /// If there's only one framework, selects it automatically.
    /// If there are multiple frameworks, prompts the user (interactive) or shows an error (non-interactive).
    /// </summary>
    /// <param name="frameworks">Array of target frameworks to choose from</param>
    /// <param name="isInteractive">Whether we're running in interactive mode (can prompt user)</param>
    /// <param name="selectedFramework">The selected target framework, or null if selection was cancelled</param>
    /// <returns>True if we should continue, false if we should exit with error</returns>
    public static bool TrySelectTargetFramework(string[] frameworks, bool isInteractive, out string? selectedFramework)
    {
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
            selectedFramework = null;
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
                .Title($"{CyanMarkup}{Markup.Escape(CliCommandStrings.RunCommandSelectTargetFrameworkPrompt)}{EndMarkup}")
                .PageSize(10)
                .MoreChoicesText($"{GrayMarkup}({Markup.Escape(CliCommandStrings.RunCommandMoreFrameworksText)}){EndMarkup}")
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

    /// <summary>
    /// Represents a device item returned from the ComputeAvailableDevices MSBuild target.
    /// </summary>
    public record DeviceItem(string Id, string? Description, string? Type, string? Status, string? RuntimeIdentifier);

    /// <summary>
    /// Computes available devices by calling the ComputeAvailableDevices MSBuild target if it exists.
    /// </summary>
    /// <param name="noRestore">Whether restore should be skipped before computing devices</param>
    /// <param name="devices">List of available devices if the target exists, null otherwise</param>
    /// <param name="restoreWasPerformed">True if restore was performed, false otherwise</param>
    /// <returns>True if the target was found and executed, false otherwise</returns>
    public bool TryComputeAvailableDevices(bool noRestore, out List<DeviceItem>? devices, out bool restoreWasPerformed)
    {
        devices = null;
        restoreWasPerformed = false;

        if (!OpenProjectIfNeeded(out var projectInstance))
        {
            // Invalid project file, return false
            return false;
        }

        // Check if the ComputeAvailableDevices target exists
        if (!projectInstance.Targets.ContainsKey(Constants.ComputeAvailableDevices))
        {
            return false;
        }

        // If restore is allowed, run restore first so device computation sees the restored assets
        if (!noRestore)
        {
            // Run the Restore target
            var restoreResult = projectInstance.Build(
                targets: ["Restore"],
                loggers: GetLoggers(),
                remoteLoggers: null,
                out _);
            if (!restoreResult)
            {
                return false;
            }

            restoreWasPerformed = true;
        }

        // Build the target
        var buildResult = projectInstance.Build(
            targets: [Constants.ComputeAvailableDevices],
            loggers: GetLoggers(),
            remoteLoggers: null,
            out var targetOutputs);

        if (!buildResult)
        {
            return false;
        }

        // Get the Devices items from the target output
        if (!targetOutputs.TryGetValue(Constants.ComputeAvailableDevices, out var targetResult))
        {
            return false;
        }

        devices = new(targetResult.Items.Length);

        foreach (var item in targetResult.Items)
        {
            devices.Add(new DeviceItem(
                item.ItemSpec,
                item.GetMetadata("Description"),
                item.GetMetadata("Type"),
                item.GetMetadata("Status"),
                item.GetMetadata("RuntimeIdentifier")
            ));
        }

        return true;
    }

    /// <summary>
    /// Attempts to select a device for running the application.
    /// If devices are available and none was specified, prompts the user to select one (interactive mode)
    /// or shows an error (non-interactive mode).
    /// </summary>
    /// <param name="listDevices">Whether to list devices and exit</param>
    /// <param name="noRestore">Whether restore should be skipped</param>
    /// <param name="selectedDevice">The selected device, or null if not needed</param>
    /// <param name="runtimeIdentifier">The RuntimeIdentifier for the selected device, or null if not provided</param>
    /// <param name="restoreWasPerformed">True if restore was performed, false otherwise</param>
    /// <returns>True if we should continue, false if we should exit</returns>
    public bool TrySelectDevice(
        bool listDevices,
        bool noRestore,
        out string? selectedDevice,
        out string? runtimeIdentifier,
        out bool restoreWasPerformed)
    {
        selectedDevice = null;
        runtimeIdentifier = null;
        restoreWasPerformed = false;

        // Try to get available devices from the project
        bool targetExists = TryComputeAvailableDevices(noRestore, out var devices, out restoreWasPerformed);
        
        // If the target doesn't exist, continue without device selection
        if (!targetExists)
        {
            // No device support in this project
            return true;
        }

        // Target exists - check if we have devices
        if (devices is null || devices.Count == 0)
        {
            if (listDevices)
            {
                Reporter.Output.WriteLine(CliCommandStrings.RunCommandNoDevicesAvailable);
                return true;
            }

            // Target exists but no devices available - this is an error
            Reporter.Error.WriteLine(CliCommandStrings.RunCommandNoDevicesAvailable);
            return false;
        }

        // If listing devices, display them and exit
        if (listDevices)
        {
            Reporter.Output.WriteLine(CliCommandStrings.RunCommandAvailableDevices);
            Reporter.Output.WriteLine();

            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                var displayBuilder = new StringBuilder($"  {i + 1}. {device.Id}");

                if (!string.IsNullOrWhiteSpace(device.Description))
                {
                    displayBuilder.Append($" - {device.Description}");
                }

                if (!string.IsNullOrWhiteSpace(device.Type))
                {
                    displayBuilder.Append($" ({device.Type}");
                    if (!string.IsNullOrWhiteSpace(device.Status))
                    {
                        displayBuilder.Append($", {device.Status}");
                    }
                    displayBuilder.Append(')');
                }
                else if (!string.IsNullOrWhiteSpace(device.Status))
                {
                    displayBuilder.Append($" ({device.Status})");
                }

                Reporter.Output.WriteLine(displayBuilder.ToString());
            }

            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine($"{CliCommandStrings.RunCommandExampleText}: dotnet run --device {ArgumentEscaper.EscapeSingleArg(devices[0].Id)}");
            Reporter.Output.WriteLine();
            return true;
        }

        // If there's only one device, automatically select it (similar to single framework selection)
        if (devices.Count == 1)
        {
            selectedDevice = devices[0].Id;
            runtimeIdentifier = devices[0].RuntimeIdentifier;
            return true;
        }

        if (_isInteractive)
        {
            var deviceItem = PromptForDevice(devices);
            if (deviceItem is null)
            {
                return false;
            }

            selectedDevice = deviceItem.Id;
            runtimeIdentifier = deviceItem.RuntimeIdentifier;
            return true;
        }
        else
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyDevice, "--device"));
            Reporter.Error.WriteLine();
            Reporter.Error.WriteLine(CliCommandStrings.RunCommandAvailableDevices);
            Reporter.Error.WriteLine();

            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                var displayText = $"  {i + 1}. {device.Id}";

                if (!string.IsNullOrWhiteSpace(device.Description))
                {
                    displayText += $" - {device.Description}";
                }

                Reporter.Error.WriteLine(displayText);
            }

            Reporter.Error.WriteLine();
            Reporter.Error.WriteLine($"{CliCommandStrings.RunCommandExampleText}: dotnet run --device {ArgumentEscaper.EscapeSingleArg(devices[0].Id)}");
            Reporter.Error.WriteLine();
            return false;
        }
    }

    /// <summary>
    /// Prompts the user to select a device from the available options using Spectre.Console.
    /// </summary>
    private static DeviceItem? PromptForDevice(List<DeviceItem> devices)
    {
        List<(string Display, DeviceItem Device)> choices = new(devices.Count);
        foreach (var d in devices)
        {
            var display = d.Id;
            if (!string.IsNullOrWhiteSpace(d.Description))
            {
                display += $" - {d.Description}";
            }
            choices.Add((display, d));
        }

        try
        {
            var prompt = new SelectionPrompt<(string Display, DeviceItem Device)>()
                .Title($"{CyanMarkup}{Markup.Escape(CliCommandStrings.RunCommandSelectDevicePrompt)}{EndMarkup}")
                .PageSize(10)
                .MoreChoicesText($"{GrayMarkup}({Markup.Escape(CliCommandStrings.RunCommandMoreDevicesText)}){EndMarkup}")
                .AddChoices(choices)
                .UseConverter(choice => choice.Display)
                .EnableSearch()
                .SearchPlaceholderText(CliCommandStrings.RunCommandSearchPlaceholderText);

            var (Display, Device) = Spectre.Console.AnsiConsole.Prompt(prompt);
            return Device;
        }
        catch (Exception)
        {
            // If Spectre.Console fails (e.g., terminal doesn't support it), return null
            return null;
        }
    }

    /// <summary>
    /// Attempts to deploy to a device by calling the DeployToDevice MSBuild target if it exists.
    /// This reuses the already-loaded project instance for performance.
    /// </summary>
    /// <returns>True if deployment succeeded or was skipped (no target), false if deployment failed</returns>
    public bool TryDeployToDevice()
    {
        if (!OpenProjectIfNeeded(out var projectInstance))
        {
            // Invalid project file
            return false;
        }

        // Check if the DeployToDevice target exists in the project
        if (!projectInstance.Targets.ContainsKey(Constants.DeployToDevice))
        {
            // Target doesn't exist, skip deploy step
            return true;
        }

        // Build the DeployToDevice target
        var buildResult = projectInstance.Build(
            targets: [Constants.DeployToDevice],
            loggers: GetLoggers(),
            remoteLoggers: null,
            out _);

        return buildResult;
    }

    /// <summary>
    /// Gets the list of loggers to use for MSBuild operations.
    /// Creates a fresh console logger each time to avoid disposal issues when calling Build() multiple times.
    /// </summary>
    private IEnumerable<ILogger> GetLoggers()
    {
        if (_binaryLogger is not null)
            yield return _binaryLogger;
        yield return CommonRunHelpers.GetConsoleLogger(_msbuildArgs);
    }
}
