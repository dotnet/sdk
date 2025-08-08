// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CommandLine;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.NET.HostModel.AppHost;
using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Used to invoke C# compiler in some optimized paths of <c>dotnet run file.cs</c>.
/// </summary>
internal sealed partial class CSharpCompilerCommand
{
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
    internal static string RuntimeVersion => field ??= RuntimeInformation.FrameworkDescription.Split(' ').Last();
    private static string TargetFrameworkVersion => Product.TargetFrameworkVersion;

    public required string EntryPointFileFullPath { get; init; }
    public required string ArtifactsPath { get; init; }
    public required bool CanReuseAuxiliaryFiles { get; init; }

    /// <param name="fallbackToNormalBuild">
    /// Whether the returned error code should not cause the build to fail but instead fallback to full MSBuild.
    /// </param>
    public int Execute(out bool fallbackToNormalBuild)
    {
        // Write .rsp file and other intermediate build outputs.
        PrepareAuxiliaryFiles(out string rspPath);

        // Create a request for the compiler server
        // (this is much faster than starting a csc.dll process, especially on Windows).
        var buildRequest = BuildServerConnection.CreateBuildRequest(
            requestId: EntryPointFileFullPath,
            language: RequestLanguage.CSharpCompile,
            arguments: ["/noconfig", "/nologo", $"@{EscapeSingleArg(rspPath)}"],
            workingDirectory: Environment.CurrentDirectory,
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
        return ProcessBuildResponse(responseTask.Result, out fallbackToNormalBuild);

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
    }

    private void PrepareAuxiliaryFiles(out string rspPath)
    {
        Reporter.Verbose.WriteLine(CanReuseAuxiliaryFiles
            ? "CSC auxiliary files can be reused."
            : "CSC auxiliary files can NOT be reused.");

        string fileDirectory = Path.GetDirectoryName(EntryPointFileFullPath) ?? string.Empty;
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(EntryPointFileFullPath);

        // Note that Release builds won't go through this optimized code path because `-c Release` translates to global property `Configuration=Release`
        // and customizing global properties triggers a full MSBuild run.
        string objDir = Path.Join(ArtifactsPath, "obj", "debug");
        Directory.CreateDirectory(objDir);
        string binDir = Path.Join(ArtifactsPath, "bin", "debug");
        Directory.CreateDirectory(binDir);

        string assemblyAttributes = Path.Join(objDir, $".NETCoreApp,Version=v{TargetFrameworkVersion}.AssemblyAttributes.cs");
        if (ShouldEmit(assemblyAttributes))
        {
            File.WriteAllText(assemblyAttributes, /* lang=C#-test */ $"""
                // <autogenerated />
                using System;
                using System.Reflection;
                [assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v{TargetFrameworkVersion}", FrameworkDisplayName = ".NET {TargetFrameworkVersion}")]

                """);
        }

        string globalUsings = Path.Join(objDir, $"{fileNameWithoutExtension}.GlobalUsings.g.cs");
        if (ShouldEmit(globalUsings))
        {
            File.WriteAllText(globalUsings, /* lang=C#-test */ """
                // <auto-generated/>
                global using System;
                global using System.Collections.Generic;
                global using System.IO;
                global using System.Linq;
                global using System.Net.Http;
                global using System.Threading;
                global using System.Threading.Tasks;

                """);
        }

        string assemblyInfo = Path.Join(objDir, $"{fileNameWithoutExtension}.AssemblyInfo.cs");
        if (ShouldEmit(assemblyInfo))
        {
            File.WriteAllText(assemblyInfo, /* lang=C#-test */ $"""
                //------------------------------------------------------------------------------
                // <auto-generated>
                //     This code was generated by a tool.
                //
                //     Changes to this file may cause incorrect behavior and will be lost if
                //     the code is regenerated.
                // </auto-generated>
                //------------------------------------------------------------------------------

                using System;
                using System.Reflection;

                [assembly: System.Reflection.AssemblyCompanyAttribute("{fileNameWithoutExtension}")]
                [assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
                [assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
                [assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0")]
                [assembly: System.Reflection.AssemblyProductAttribute("{fileNameWithoutExtension}")]
                [assembly: System.Reflection.AssemblyTitleAttribute("{fileNameWithoutExtension}")]
                [assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

                // Generated by the MSBuild WriteCodeFragment class.


                """);
        }

        string editorconfig = Path.Join(objDir, $"{fileNameWithoutExtension}.GeneratedMSBuildEditorConfig.editorconfig");
        if (ShouldEmit(editorconfig))
        {
            File.WriteAllText(editorconfig, $"""
                is_global = true
                build_property.EnableAotAnalyzer = true
                build_property.EnableSingleFileAnalyzer = true
                build_property.EnableTrimAnalyzer = true
                build_property.IncludeAllContentForSelfExtract = 
                build_property.TargetFramework = net{TargetFrameworkVersion}
                build_property.TargetFrameworkIdentifier = .NETCoreApp
                build_property.TargetFrameworkVersion = v{TargetFrameworkVersion}
                build_property.TargetPlatformMinVersion = 
                build_property.UsingMicrosoftNETSdkWeb = 
                build_property.ProjectTypeGuids = 
                build_property.InvariantGlobalization = 
                build_property.PlatformNeutralAssembly = 
                build_property.EnforceExtendedAnalyzerRules = 
                build_property._SupportedPlatformList = Linux,macOS,Windows
                build_property.RootNamespace = {fileNameWithoutExtension}
                build_property.ProjectDir = {fileDirectory}{Path.DirectorySeparatorChar}
                build_property.EnableComHosting = 
                build_property.EnableGeneratedComInterfaceComImportInterop = false
                build_property.EffectiveAnalysisLevelStyle = {TargetFrameworkVersion}
                build_property.EnableCodeStyleSeverity = 

                """);
        }

        var apphostTarget = Path.Join(binDir, $"{fileNameWithoutExtension}{FileNameSuffixes.CurrentPlatform.Exe}");
        if (ShouldEmit(apphostTarget))
        {
            var rid = RuntimeInformation.RuntimeIdentifier;
            var apphostSource = Path.Join(SdkPath, "..", "..", "packs", $"Microsoft.NETCore.App.Host.{rid}", RuntimeVersion, "runtimes", rid, "native", $"apphost{FileNameSuffixes.CurrentPlatform.Exe}");
            HostWriter.CreateAppHost(
                appHostSourceFilePath: apphostSource,
                appHostDestinationFilePath: apphostTarget,
                appBinaryFilePath: $"{fileNameWithoutExtension}.dll",
                enableMacOSCodeSign: OperatingSystem.IsMacOS());
        }

        var runtimeConfig = Path.Join(binDir, $"{fileNameWithoutExtension}{FileNameSuffixes.RuntimeConfigJson}");
        if (ShouldEmit(runtimeConfig))
        {
            File.WriteAllText(runtimeConfig, $$"""
                {
                  "runtimeOptions": {
                    "tfm": "net{{TargetFrameworkVersion}}",
                    "framework": {
                      "name": "Microsoft.NETCore.App",
                      "version": {{JsonSerializer.Serialize(RuntimeVersion)}}
                    },
                    "configProperties": {
                      "EntryPointFilePath": {{JsonSerializer.Serialize(EntryPointFileFullPath)}},
                      "EntryPointFileDirectoryPath": {{JsonSerializer.Serialize(fileDirectory)}},
                      "Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability": true,
                      "System.ComponentModel.DefaultValueAttribute.IsSupported": false,
                      "System.ComponentModel.Design.IDesignerHost.IsSupported": false,
                      "System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization": false,
                      "System.ComponentModel.TypeDescriptor.IsComObjectDescriptorSupported": false,
                      "System.Data.DataSet.XmlSerializationIsSupported": false,
                      "System.Diagnostics.Tracing.EventSource.IsSupported": false,
                      "System.Linq.Enumerable.IsSizeOptimized": true,
                      "System.Net.SocketsHttpHandler.Http3Support": false,
                      "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
                      "System.Resources.ResourceManager.AllowCustomResourceTypes": false,
                      "System.Resources.UseSystemResourceKeys": false,
                      "System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported": false,
                      "System.Runtime.InteropServices.BuiltInComInterop.IsSupported": false,
                      "System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting": false,
                      "System.Runtime.InteropServices.EnableCppCLIHostActivation": false,
                      "System.Runtime.InteropServices.Marshalling.EnableGeneratedComInterfaceComImportInterop": false,
                      "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false,
                      "System.StartupHookProvider.IsSupported": false,
                      "System.Text.Encoding.EnableUnsafeUTF7Encoding": false,
                      "System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault": false,
                      "System.Threading.Thread.EnableAutoreleasePool": false,
                      "System.Linq.Expressions.CanEmitObjectArrayDelegate": false
                    }
                  }
                }
                """);
        }

        rspPath = Path.Join(ArtifactsPath, "csc.rsp");
        if (ShouldEmit(rspPath))
        {
            IEnumerable<string> args = GetCscArguments(
                fileNameWithoutExtension: fileNameWithoutExtension,
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
            return arg[..(colonIndex + 1)] + EscapeCore(arg[(colonIndex + 1)..]);
        }

        return EscapeCore(arg);

        static string EscapeCore(string arg)
        {
            return ArgumentEscaper.EscapeSingleArg(arg, additionalShouldSurroundWithQuotes: static (string arg) =>
            {
                return arg.ContainsAny(s_additionalShouldSurroundWithQuotes);
            });
        }
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
}
