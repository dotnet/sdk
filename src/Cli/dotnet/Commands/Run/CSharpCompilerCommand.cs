// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CommandLine;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.NET.HostModel.AppHost;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Used to invoke C# compiler in some optimized paths of <c>dotnet run file.cs</c>.
/// </summary>
internal sealed partial class CSharpCompilerCommand
{
    [JsonSerializable(typeof(string))]
    private partial class CSharpCompilerCommandJsonSerializerContext : JsonSerializerContext;

    private static readonly SearchValues<char> s_additionalShouldSurroundWithQuotes = SearchValues.Create('=', ',');

    /// <summary>
    /// Options which denote paths and which might appear in the simple app compilation that we optimize for.
    /// </summary>
    private static readonly ImmutableArray<string> s_pathOptions =
    [
        "reference:",
        "analyzer:",
        "additionalfile:",
        "analyzerconfig:",
        "embed:",
        "resource:",
        "linkresource:",
        "ruleset:",
        "keyfile:",
        "link:",
    ];

    private static string SdkPath => field ??= PathUtility.EnsureNoTrailingDirectorySeparator(AppContext.BaseDirectory);
    private static string DotNetRootPath => field ??= Path.GetDirectoryName(Path.GetDirectoryName(SdkPath)!)!;
    private static string ClientDirectory => field ??= Path.Combine(SdkPath, "Roslyn", "bincore");
    private static string NuGetCachePath => field ??= SettingsUtility.GetGlobalPackagesFolder(Settings.LoadDefaultSettings(null));
    internal static string RuntimeVersion => field ??= ComputeRuntimeVersion();
    internal static string DefaultRuntimeVersion => field ??= ComputeDefaultRuntimeVersion();
    internal static string TargetFrameworkVersion => Product.TargetFrameworkVersion;
    internal static string TargetFramework => field ??= $"net{TargetFrameworkVersion}";

    public required string EntryPointFileFullPath { get; init; }
    public required string ArtifactsPath { get; init; }
    public required bool CanReuseAuxiliaryFiles { get; init; }

    public string BaseDirectory => field ??= Path.GetDirectoryName(EntryPointFileFullPath)!;
    internal string BaseDirectoryWithTrailingSeparator => field ??= BaseDirectory + Path.DirectorySeparatorChar;
    internal string FileName => field ??= Path.GetFileName(EntryPointFileFullPath);
    internal string FileNameWithoutExtension => field ??= Path.GetFileNameWithoutExtension(EntryPointFileFullPath);

    /// <summary>
    /// Compiler command line arguments to use. If empty, default arguments are used.
    /// These should be already properly escaped.
    /// </summary>
    public required ImmutableArray<string> CscArguments { get; init; }

    /// <summary>
    /// Path to the <c>bin/Program.dll</c> file. If specified,
    /// the compiled output (<c>obj/Program.dll</c>) will be copied to this location.
    /// </summary>
    public required string? BuildResultFile { get; init; }

    /// <param name="fallbackToNormalBuild">
    /// Whether the returned error code should not cause the build to fail but instead fallback to full MSBuild.
    /// </param>
    public int Execute(out bool fallbackToNormalBuild)
    {
        // Write .rsp file and other intermediate build outputs.
        PrepareAuxiliaryFiles(out string rspPath);

        // Ensure the compiler is launched with the correct dotnet.
        Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", new Muxer().MuxerPath);

        // Create a request for the compiler server
        // (this is much faster than starting a csc.dll process, especially on Windows).
        var buildRequest = BuildServerConnection.CreateBuildRequest(
            requestId: EntryPointFileFullPath,
            language: RequestLanguage.CSharpCompile,
            arguments: ["/noconfig", "/nologo", $"@{EscapeSingleArg(rspPath)}"],
            workingDirectory: BaseDirectory,
            tempDirectory: Path.GetTempPath(),
            keepAlive: null,
            libDirectory: null,
            compilerHash: GetCompilerCommitHash());

        // Get pipe name.
        var pipeName = BuildServerConnection.GetPipeName(clientDirectory: ClientDirectory);

        // Create logger.
        var logger = new CompilerServerLogger(
            identifier: $"dotnet run file {Environment.ProcessId}",
            loggingFilePath: null);

        // Send the request.
        var responseTask = BuildServerConnection.RunServerBuildRequestAsync(
            buildRequest,
            pipeName: pipeName,
            clientDirectory: ClientDirectory,
            logger,
            cancellationToken: default);

        // Process the response.
        var exitCode = ProcessBuildResponse(responseTask.Result, out fallbackToNormalBuild);

        // Copy from obj to bin only if the build succeeded.
        if (exitCode == 0 &&
            BuildResultFile != null &&
            CSharpCommandLineParser.Default.Parse(CscArguments, BaseDirectory, sdkDirectory: null) is { OutputFileName: { } outputFileName } parsedArgs)
        {
            var objFile = new FileInfo(parsedArgs.GetOutputFilePath(outputFileName));
            var binFile = new FileInfo(BuildResultFile);

            if (HaveMatchingSizeAndTimeStamp(objFile, binFile))
            {
                Reporter.Verbose.WriteLine($"Skipping copy of '{objFile}' to '{BuildResultFile}' because the files have matching size and timestamp.");
            }
            else
            {
                Reporter.Verbose.WriteLine($"Copying '{objFile}' to '{BuildResultFile}'.");
                File.Copy(objFile.FullName, binFile.FullName, overwrite: true);
            }
        }

        return exitCode;

        static string GetCompilerCommitHash()
        {
            return typeof(CSharpCompilation).Assembly.GetCustomAttributesData()
                .FirstOrDefault(attr => attr.AttributeType.FullName == "Microsoft.CodeAnalysis.CommitHashAttribute")?
                .ConstructorArguments
                .FirstOrDefault()
                .Value as string
                ?? throw new InvalidOperationException("Could not find compiler commit hash in the assembly attributes.");
        }

        static int ProcessBuildResponse(BuildResponse response, out bool fallbackToNormalBuild)
        {
            switch (response)
            {
                case CompletedBuildResponse completed:
                    Reporter.Verbose.WriteLine("Compiler server processed compilation.");

                    // Check if the compilation failed with CS0006 error (metadata file not found).
                    // This can happen when NuGet cache is cleared and referenced DLLs (e.g., analyzers or libraries) are missing.
                    if (completed.ReturnCode != 0 && completed.Output.Contains("error CS0006:", StringComparison.Ordinal))
                    {
                        Reporter.Verbose.WriteLine("CS0006 error detected in fast compilation path, falling back to full MSBuild.");
                        Reporter.Verbose.Write(completed.Output);
                        fallbackToNormalBuild = true;
                        return completed.ReturnCode;
                    }

                    Reporter.Output.Write(completed.Output);
                    fallbackToNormalBuild = false;
                    return completed.ReturnCode;

                case IncorrectHashBuildResponse:
                    Reporter.Error.WriteLine("Error: Compiler server reports a different hash version than the SDK.".Red());
                    fallbackToNormalBuild = false;
                    return 1;

                case null:
                    Reporter.Output.WriteLine("Warning: Could not launch the compiler server.".Yellow());
                    fallbackToNormalBuild = true;
                    return 1;

                default:
                    Reporter.Error.WriteLine($"Warning: Compiler server returned unexpected response: {response.GetType().Name}".Yellow());
                    fallbackToNormalBuild = true;
                    return 1;
            }
        }

        // Inspired by MSBuild: https://github.com/dotnet/msbuild/blob/a7a4d5af02be5aa6dc93a492d6d03056dc811388/src/Tasks/Copy.cs#L208
        static bool HaveMatchingSizeAndTimeStamp(FileInfo sourceFile, FileInfo destinationFile)
        {
            if (!destinationFile.Exists)
            {
                return false;
            }

            if (sourceFile.LastWriteTimeUtc != destinationFile.LastWriteTimeUtc)
            {
                return false;
            }

            if (sourceFile.Length != destinationFile.Length)
            {
                return false;
            }

            return true;
        }
    }

    internal static string WriteCscRspFile(string artifactsPath, ImmutableArray<string> cscArguments)
    {
        string rspPath = GetCscRspPath(artifactsPath);
        File.WriteAllLines(rspPath, cscArguments);
        return rspPath;
    }

    private static string GetCscRspPath(string artifactsPath) => Path.Join(artifactsPath, "csc.rsp");

    private void PrepareAuxiliaryFiles(out string rspPath)
    {
        if (!CscArguments.IsDefaultOrEmpty)
        {
            rspPath = WriteCscRspFile(ArtifactsPath, CscArguments);
            return;
        }

        rspPath = GetCscRspPath(ArtifactsPath);

        // Note that Release builds won't go through this optimized code path because `-c Release` translates to global property `Configuration=Release`
        // and customizing global properties triggers a full MSBuild run.
        string objDir = Path.Join(ArtifactsPath, "obj", "debug");
        Directory.CreateDirectory(objDir);
        string binDir = Path.Join(ArtifactsPath, "bin", "debug");
        Directory.CreateDirectory(binDir);

        string assemblyAttributes = Path.Join(objDir, $".NETCoreApp,Version=v{TargetFrameworkVersion}.AssemblyAttributes.cs");
        if (ShouldEmit(assemblyAttributes))
        {
            File.WriteAllText(assemblyAttributes, GetAssemblyAttributesContent());
        }

        string globalUsings = Path.Join(objDir, $"{FileName}.GlobalUsings.g.cs");
        if (ShouldEmit(globalUsings))
        {
            File.WriteAllText(globalUsings, GetGlobalUsingsContent());
        }

        string assemblyInfo = Path.Join(objDir, $"{FileName}.AssemblyInfo.cs");
        if (ShouldEmit(assemblyInfo))
        {
            File.WriteAllText(assemblyInfo, GetAssemblyInfoContent());
        }

        string editorconfig = Path.Join(objDir, $"{FileName}.GeneratedMSBuildEditorConfig.editorconfig");
        if (ShouldEmit(editorconfig))
        {
            File.WriteAllText(editorconfig, GetGeneratedMSBuildEditorConfigContent());
        }

        var apphostTarget = Path.Join(binDir, $"{FileNameWithoutExtension}{FileNameSuffixes.CurrentPlatform.Exe}");
        if (ShouldEmit(apphostTarget))
        {
            var rid = RuntimeInformation.RuntimeIdentifier;
            var apphostSource = Path.Join(SdkPath, "..", "..", "packs", $"Microsoft.NETCore.App.Host.{rid}", RuntimeVersion, "runtimes", rid, "native", $"apphost{FileNameSuffixes.CurrentPlatform.Exe}");
            HostWriter.CreateAppHost(
                appHostSourceFilePath: apphostSource,
                appHostDestinationFilePath: apphostTarget,
                appBinaryFilePath: $"{FileNameWithoutExtension}.dll",
                enableMacOSCodeSign: OperatingSystem.IsMacOS());
        }

        var runtimeConfig = Path.Join(binDir, $"{FileNameWithoutExtension}{FileNameSuffixes.RuntimeConfigJson}");
        if (ShouldEmit(runtimeConfig))
        {
            File.WriteAllText(runtimeConfig, GetRuntimeConfigContent());
        }

        if (ShouldEmit(rspPath))
        {
            IEnumerable<string> args = GetCscArguments(
                objDir: objDir,
                binDir: binDir);

            File.WriteAllLines(rspPath, args.Select(EscapeSingleArg));
        }

        bool ShouldEmit(string file)
        {
            if (!CanReuseAuxiliaryFiles)
            {
                return true;
            }

            if (!File.Exists(file))
            {
                Reporter.Verbose.WriteLine($"Generating CSC auxiliary file because it does not exist: {file}");
                return true;
            }

            return false;
        }
    }

    private static string EscapeSingleArg(string arg)
    {
        if (IsPathOption(arg, out var colonIndex))
        {
            return arg[..(colonIndex + 1)] + EscapePathArgument(arg[(colonIndex + 1)..]);
        }

        return EscapePathArgument(arg);
    }

    internal static string EscapePathArgument(string arg)
    {
        return ArgumentEscaper.EscapeSingleArg(arg, additionalShouldSurroundWithQuotes: static (string arg) =>
        {
            return arg.ContainsAny(s_additionalShouldSurroundWithQuotes);
        });
    }

    public static bool IsPathOption(string arg, out int colonIndex)
    {
        if (!arg.StartsWith('/'))
        {
            colonIndex = -1;
            return false;
        }

        var span = arg.AsSpan(start: 1);
        foreach (var optionName in s_pathOptions)
        {
            Debug.Assert(!optionName.StartsWith('/') && optionName.EndsWith(':'));

            if (span.StartsWith(optionName, StringComparison.OrdinalIgnoreCase))
            {
                colonIndex = optionName.Length;
                return true;
            }
        }

        colonIndex = -1;
        return false;
    }

    private static string ComputeRuntimeVersion()
    {
        var executingRuntimeVersion = RuntimeInformation.FrameworkDescription.Split(' ').Last();
        var executingRuntimeMajorVersion = executingRuntimeVersion.Split('.').First();
        var tfmMajorVersion = TargetFrameworkVersion.Split('.').First();

        // If the target framework is still net10.0 while the runtime is already 11.0.x, we need to force-use 10.0.x runtime.
        if (tfmMajorVersion != executingRuntimeMajorVersion)
        {
            return tfmMajorVersion + ".0.0";
        }

        // Otherwise, we can use the current runtime.
        return executingRuntimeVersion;
    }

    /// <summary>
    /// See <c>GenerateDefaultRuntimeFrameworkVersion</c>.
    /// </summary>
    private static string ComputeDefaultRuntimeVersion()
    {
        if (NuGetVersion.TryParse(RuntimeVersion, out var version))
        {
            return version.IsPrerelease && version.Patch == 0 ?
                RuntimeVersion :
                new NuGetVersion(version.Major, version.Minor, 0).ToFullString();
        }

        return RuntimeVersion;
    }

    /// <summary>
    /// Reads the <c>FrameworkList.xml</c> from the current targeting pack and yields one
    /// <c>/reference:</c> argument per managed assembly listed there.
    /// </summary>
    private IEnumerable<string> GetFrameworkReferenceArguments()
        => GetFrameworkArguments(type: "Managed", language: null, argPrefix: "/reference:");

    /// <summary>
    /// Reads the <c>FrameworkList.xml</c> from the current targeting pack and yields one
    /// <c>/analyzer:</c> argument per C# analyzer assembly listed there.
    /// </summary>
    private IEnumerable<string> GetFrameworkAnalyzerArguments()
        => GetFrameworkArguments(type: "Analyzer", language: "cs", argPrefix: "/analyzer:");

    /// <summary>
    /// Reads the <c>FrameworkList.xml</c> from the current targeting pack and yields one
    /// compiler argument per matching assembly listed there.
    /// </summary>
    private IEnumerable<string> GetFrameworkArguments(string type, string? language, string argPrefix)
    {
        var packRoot = Path.Join(DotNetRootPath, "packs", "Microsoft.NETCore.App.Ref", RuntimeVersion);
        var frameworkListPath = Path.Join(packRoot, "data", "FrameworkList.xml");
        if (!File.Exists(frameworkListPath))
        {
            throw new InvalidOperationException($"FrameworkList.xml not found at '{frameworkListPath}'. The SDK installation may be corrupted.");
        }

        var frameworkList = XDocument.Load(frameworkListPath);
        foreach (var file in frameworkList.Root?.Elements("File") ?? [])
        {
            if (file.Attribute("Type")?.Value.Equals(type, StringComparison.OrdinalIgnoreCase) != true)
            {
                continue;
            }

            if (language is not null && file.Attribute("Language")?.Value.Equals(language, StringComparison.OrdinalIgnoreCase) != true)
            {
                continue;
            }

            var filePath = file.Attribute("Path")?.Value;
            if (string.IsNullOrEmpty(filePath))
            {
                continue;
            }

            yield return $"{argPrefix}{Path.Join(packRoot, filePath)}";
        }
    }
}
