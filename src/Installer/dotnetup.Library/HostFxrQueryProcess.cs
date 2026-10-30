// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>Queries hostfxr in a short-lived copy of dotnetup so installation files are not locked by the parent.</summary>
internal static class HostFxrQueryProcess
{
    internal const string Command = "--internal-query-hostfxr";
    internal const int EnvironmentInfoUnavailableExitCode = 2;

    public static NetEnvironmentInfo GetInfo(string installRoot) =>
        GetInfo(installRoot, DotnetupProcessInfo.ExecutablePath
            ?? throw new InvalidOperationException("Could not locate the dotnetup executable."));

    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || args[0] != Command)
        {
            return false;
        }

        try
        {
            if (args.Length != 2 || !Path.IsPathFullyQualified(args[1]))
            {
                throw new ArgumentException("The hostfxr query requires one absolute install root.", nameof(args));
            }

            var info = HostFxrWrapper.getInfo(args[1]);
            var result = new HostFxrQueryResult(
                info.SdkInfo.Select(sdk => new HostFxrSdkInfo(sdk.Version.ToString(), sdk.Path)).ToArray(),
                info.RuntimeInfo.Select(runtime => new HostFxrRuntimeInfo(runtime.Name, runtime.Version.ToString(), runtime.Path)).ToArray());
            JsonSerializer.Serialize(Console.OpenStandardOutput(), result, HostFxrQueryJsonContext.Default.HostFxrQueryResult);
        }
        catch (HostFxrEnvironmentInfoUnavailableException ex)
        {
            Console.Error.WriteLine(ex.Message);
            exitCode = EnvironmentInfoUnavailableExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Hostfxr query failed: {ex.Message}");
            exitCode = 1;
        }

        return true;
    }

    internal static NetEnvironmentInfo GetInfo(string installRoot, string executablePath)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            // Hostfxr SDK discovery honors global.json relative to the working directory.
            WorkingDirectory = Path.GetFullPath(installRoot),
        };
        startInfo.ArgumentList.Add(Command);
        startInfo.ArgumentList.Add(Path.GetFullPath(installRoot));
        startInfo.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";

        ProcessTextOutput output;
        try
        {
            output = Process.RunAndCaptureText(startInfo, timeout: TimeSpan.FromSeconds(60));
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException("The hostfxr query did not complete within 60 seconds.", ex);
        }

        if (output.ExitStatus.Canceled)
        {
            throw new TimeoutException("The hostfxr query did not complete within 60 seconds.");
        }

        if (output.ExitStatus.ExitCode == EnvironmentInfoUnavailableExitCode)
        {
            throw new HostFxrEnvironmentInfoUnavailableException();
        }

        if (output.ExitStatus.ExitCode != 0)
        {
            throw new InvalidOperationException($"Hostfxr query exited with code {output.ExitStatus.ExitCode}: {output.StandardError}");
        }

        var result = JsonSerializer.Deserialize(output.StandardOutput, HostFxrQueryJsonContext.Default.HostFxrQueryResult)
            ?? throw new InvalidDataException("The hostfxr query returned no environment information.");
        return new NetEnvironmentInfo(
            result.RuntimeInfo.Select(runtime => new NetRuntimeInfo(runtime.Name, runtime.Version, runtime.Path)).ToArray(),
            result.SdkInfo.Select(sdk => new NetSdkInfo(sdk.Version, sdk.Path)).ToArray());
    }
}

internal sealed record HostFxrQueryResult(HostFxrSdkInfo[] SdkInfo, HostFxrRuntimeInfo[] RuntimeInfo);
internal sealed record HostFxrSdkInfo(string Version, string Path);
internal sealed record HostFxrRuntimeInfo(string Name, string Version, string Path);

[JsonSourceGenerationOptions(RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(HostFxrQueryResult))]
internal partial class HostFxrQueryJsonContext : JsonSerializerContext { }
