// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks;

/// <summary>
/// The custom MSBuild task that invokes the 'cswinrtgen' tool.
/// </summary>
public sealed class RunCsWinRTGenerator : ToolTask
{
    /// <summary>
    /// Gets or sets the paths to assembly files that are reference assemblies, representing
    /// the entire surface area for compilation. These assemblies are the full set of assemblies
    /// that will contribute to the interop .dll being generated.
    /// </summary>
    [Required]
    public ITaskItem[]? ReferenceAssemblyPaths { get; set; }

    /// <summary>
    /// Gets or sets the path to the output assembly that was produced by the build (for the current project).
    /// </summary>
    /// <remarks>
    /// This property is an array, but it should only ever receive a single item.
    /// </remarks>
    [Required]
    public ITaskItem[]? OutputAssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets the directory where the generated interop assembly will be placed.
    /// </summary>
    [Required]
    public string? InteropAssemblyDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory where the debug repro will be produced.
    /// </summary>
    /// <remarks>If not set, no debug repro will be produced.</remarks>
    public string? DebugReproDirectory { get; set; }

    /// <summary>
    /// Gets or sets the tools directory where the 'cswinrtgen' tool is located.
    /// </summary>
    [Required]
    public string? CsWinRTToolsDirectory { get; set; }

    /// <summary>
    /// Gets or sets the architecture of 'cswinrtgen' to use.
    /// </summary>
    /// <remarks>
    /// If not set, the architecture will be determined based on the current process architecture.
    /// </remarks>
    public string? CsWinRTToolsArchitecture { get; set; }

    /// <summary>
    /// Gets or sets whether to use <c>Windows.UI.Xaml</c> projections.
    /// </summary>
    /// <remarks>If not set, it will default to <see langword="false"/> (i.e. using <c>Microsoft.UI.Xaml</c> projections).</remarks>
    public bool UseWindowsUIXamlProjections { get; set; } = false;

    /// <summary>
    /// Gets whether to validate the assembly version of <c>WinRT.Runtime.dll</c>, to ensure it matches the generator.
    /// </summary>
    public bool ValidateWinRTRuntimeAssemblyVersion { get; set; } = true;

    /// <summary>
    /// Gets whether to validate that any references to <c>WinRT.Runtime.dll</c> version 2 are present across any assemblies.
    /// </summary>
    public bool ValidateWinRTRuntimeDllVersion2References { get; set; } = true;

    /// <summary>
    /// Gets whether to enable incremental generation (i.e. with a cache file on disk saving the full set of types to generate).
    /// </summary>
    public bool EnableIncrementalGeneration { get; set; } = true;

    /// <summary>
    /// Gets whether to treat warnings coming from 'cswinrtgen' as errors (regardless of the global 'TreatWarningsAsErrors' setting).
    /// </summary>
    public bool TreatWarningsAsErrors { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of parallel tasks to use for execution.
    /// </summary>
    /// <remarks>If not set, the default will match the number of available processor cores.</remarks>
    public int MaxDegreesOfParallelism { get; set; } = -1;

    /// <summary>
    /// Gets or sets additional arguments to pass to the tool.
    /// </summary>
    public ITaskItem[]? AdditionalArguments { get; set; }

    /// <inheritdoc/>
    protected override string ToolName => "cswinrtgen.exe";

    /// <summary>
    /// Gets the effective item spec for the output assembly.
    /// </summary>
    private string EffectiveOutputAssemblyItemSpec => OutputAssemblyPath![0].ItemSpec;

    /// <inheritdoc/>
#if NET10_0_OR_GREATER
    [MemberNotNullWhen(true, nameof(ReferenceAssemblyPaths))]
    [MemberNotNullWhen(true, nameof(OutputAssemblyPath))]
    [MemberNotNullWhen(true, nameof(InteropAssemblyDirectory))]
    [MemberNotNullWhen(true, nameof(CsWinRTToolsDirectory))]
#endif
    protected override bool ValidateParameters()
    {
        if (!base.ValidateParameters())
        {
            return false;
        }

        if (ReferenceAssemblyPaths is not { Length: > 0 })
        {
            Log.LogWarning("Invalid 'ReferenceAssemblyPaths' input(s).");

            return false;
        }

        if (OutputAssemblyPath is not { Length: 1 })
        {
            Log.LogWarning("Invalid 'OutputAssemblyPath' input.");

            return false;
        }

        if (InteropAssemblyDirectory is null || !Directory.Exists(InteropAssemblyDirectory))
        {
            Log.LogWarning("Generated assembly directory '{0}' is invalid or does not exist.", InteropAssemblyDirectory);

            return false;
        }

        if (DebugReproDirectory is not null && !Directory.Exists(DebugReproDirectory))
        {
            Log.LogWarning("Debug repro directory '{0}' is invalid or does not exist.", DebugReproDirectory);

            return false;
        }

        if (CsWinRTToolsDirectory is null || !Directory.Exists(CsWinRTToolsDirectory))
        {
            Log.LogWarning("Tools directory '{0}' is invalid or does not exist.", CsWinRTToolsDirectory);

            return false;
        }

        if (CsWinRTToolsArchitecture is not null &&
            !CsWinRTToolsArchitecture.Equals("x86", StringComparison.OrdinalIgnoreCase) &&
            !CsWinRTToolsArchitecture.Equals("x64", StringComparison.OrdinalIgnoreCase) &&
            !CsWinRTToolsArchitecture.Equals("arm64", StringComparison.OrdinalIgnoreCase) &&
            !CsWinRTToolsArchitecture.Equals("AnyCPU", StringComparison.OrdinalIgnoreCase))
        {
            Log.LogWarning("Tools architecture '{0}' is invalid (it must be 'x86', 'x64', 'arm64', or 'AnyCPU').", CsWinRTToolsArchitecture);

            return false;
        }

        // The degrees of parallelism matches the semantics of the 'MaxDegreesOfParallelism' property of 'Parallel.For'. That is, it must either be exactly '-1', which is a special
        // value meaning "use as many parallel threads as the runtime deems appropriate", or it must be set to a positive integer, to explicitly control the number of threads.
        // See: https://learn.microsoft.com/dotnet/api/system.threading.tasks.paralleloptions.maxdegreeofparallelism#system-threading-tasks-paralleloptions-maxdegreeofparallelism.
        if (MaxDegreesOfParallelism is not (-1 or > 0))
        {
            Log.LogWarning("Invalid 'MaxDegreesOfParallelism' value. It must be '-1' or greater than '0' (but was '{0}').", MaxDegreesOfParallelism);

            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    [SuppressMessage("Style", "IDE0072", Justification = "We always use 'x86' as a fallback for all other CPU architectures.")]
    protected override string GenerateFullPathToTool()
    {
        string? effectiveArchitecture = CsWinRTToolsArchitecture;

        // Special case for when 'AnyCPU' is specified (mostly for testing scenarios).
        // We just reuse the exact input directory and assume the architecture matches.
        // This makes it easy to run the task against a local build of 'cswinrtgen'.
        if (effectiveArchitecture?.Equals("AnyCPU", StringComparison.OrdinalIgnoreCase) is true)
        {
            return Path.Combine(CsWinRTToolsDirectory!, ToolName);
        }

        // If the architecture is not specified, determine it based on the current process architecture
        effectiveArchitecture ??= RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => "x86"
        };

        // The tool is inside an architecture-specific subfolder, as it's a native binary
        string architectureDirectory = $"win-{effectiveArchitecture}";

        return Path.Combine(CsWinRTToolsDirectory!, architectureDirectory, ToolName);
    }

    /// <inheritdoc/>
    protected override string GenerateResponseFileCommands()
    {
        StringBuilder args = new();

        IEnumerable<string> referenceAssemblyPaths = ReferenceAssemblyPaths!.Select(static path => path.ItemSpec);
        string referenceAssemblyPathsArg = string.Join(",", referenceAssemblyPaths);

        AppendResponseFileCommand(args, "--reference-assembly-paths", referenceAssemblyPathsArg);
        AppendResponseFileCommand(args, "--output-assembly-path", EffectiveOutputAssemblyItemSpec);
        AppendResponseFileCommand(args, "--generated-assembly-directory", InteropAssemblyDirectory!);
        AppendResponseFileOptionalCommand(args, "--debug-repro-directory", DebugReproDirectory);
        AppendResponseFileCommand(args, "--use-windows-ui-xaml-projections", UseWindowsUIXamlProjections.ToString());
        AppendResponseFileCommand(args, "--validate-winrt-runtime-assembly-version", ValidateWinRTRuntimeAssemblyVersion.ToString());
        AppendResponseFileCommand(args, "--validate-winrt-runtime-dll-version-2-references", ValidateWinRTRuntimeDllVersion2References.ToString());
        AppendResponseFileCommand(args, "--enable-incremental-generation", EnableIncrementalGeneration.ToString());
        AppendResponseFileCommand(args, "--treat-warnings-as-errors", TreatWarningsAsErrors.ToString());
        AppendResponseFileCommand(args, "--max-degrees-of-parallelism", MaxDegreesOfParallelism.ToString());

        // Add any additional arguments that are not statically known
        foreach (ITaskItem additionalArgument in AdditionalArguments ?? [])
        {
            _ = args.AppendLine(additionalArgument.ItemSpec);
        }

        return args.ToString();
    }

    /// <summary>
    /// Appends a command line argument to the response file arguments, with the right format.
    /// </summary>
    /// <param name="args">The command line arguments being built.</param>
    /// <param name="commandName">The command name to append.</param>
    /// <param name="commandValue">The command value to append.</param>
    private static void AppendResponseFileCommand(StringBuilder args, string commandName, string commandValue)
    {
        _ = args.Append($"{commandName} ").AppendLine(commandValue);
    }

    /// <summary>
    /// Appends an optional command line argument to the response file arguments, with the right format.
    /// </summary>
    /// <param name="args">The command line arguments being built.</param>
    /// <param name="commandName">The command name to append.</param>
    /// <param name="commandValue">The optional command value to append.</param>
    /// <remarks>This method will not append the command if <paramref name="commandValue"/> is <see langword="null"/>.</remarks>
    private static void AppendResponseFileOptionalCommand(StringBuilder args, string commandName, string? commandValue)
    {
        if (commandValue is not null)
        {
            AppendResponseFileCommand(args, commandName, commandValue);
        }
    }
}
