// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class DotnetInstallManager : IDotnetInstallManager
{
    private readonly IEnvironmentProvider _environmentProvider;

    public DotnetInstallManager(IEnvironmentProvider? environmentProvider = null)
    {
        _environmentProvider = environmentProvider ?? new EnvironmentProvider();
    }

    public DotnetInstallRootConfiguration? GetConfiguredInstallType()
    {
        string? foundDotnet = _environmentProvider.GetCommandPath("dotnet");
        if (string.IsNullOrEmpty(foundDotnet))
        {
            return null;
        }

        var currentInstallRoot = new DotnetInstallRoot(Path.GetDirectoryName(foundDotnet)!, InstallerUtilities.GetDefaultInstallArchitecture());

        // Use InstallRootManager to determine if the install is fully configured
        if (OperatingSystem.IsWindows())
        {
            var installRootManager = new InstallRootManager(this);

            // Check if user install root is fully configured
            var userChanges = installRootManager.GetUserInstallRootChanges();
            if (!userChanges.NeedsChange() && DotnetupUtilities.PathsEqual(currentInstallRoot.Path, userChanges.UserDotnetPath))
            {
                return new(currentInstallRoot, InstallType.User, IsFullyConfigured: true);
            }

            // Check if admin install root is fully configured
            var adminChanges = installRootManager.GetAdminInstallRootChanges();
            if (!adminChanges.NeedsChange())
            {
                return new(currentInstallRoot, InstallType.Admin, IsFullyConfigured: true);
            }

            // Not fully configured, but PATH resolves to dotnet
            // Determine type based on location using registry-based detection
            var programFilesDotnetPaths = WindowsPathHelper.GetProgramFilesDotnetPaths();
            bool isAdminPath = programFilesDotnetPaths.Any(path =>
                currentInstallRoot.Path.StartsWith(path, StringComparison.OrdinalIgnoreCase));

            return new(currentInstallRoot, isAdminPath ? InstallType.Admin : InstallType.User, IsFullyConfigured: false);
        }
        else
        {
            // For non-Windows platforms, determine based on path location
            bool isAdminInstall = InstallExecutor.IsAdminInstallPath(currentInstallRoot.Path);

            // For now, we consider it fully configured if it's on PATH
            return new(currentInstallRoot, isAdminInstall ? InstallType.Admin : InstallType.User, IsFullyConfigured: true);
        }
    }

    public string GetDefaultDotnetInstallPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dotnet");
    }

    public GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory)
    {
        string? directory = initialDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            string globalJsonPath = Path.Combine(directory, "global.json");
            if (File.Exists(globalJsonPath))
            {
                using var stream = GlobalJsonFileHelper.OpenAsUtf8Stream(globalJsonPath);
                var contents = JsonSerializer.Deserialize(
                    stream,
                    GlobalJsonContentsJsonContext.Default.GlobalJsonContents);
                return new GlobalJsonInfo
                {
                    GlobalJsonPath = globalJsonPath,
                    GlobalJsonContents = contents
                };
            }
            var parent = Directory.GetParent(directory);
            if (parent == null)
            {
                break;
            }

            directory = parent.FullName;
        }
        return new GlobalJsonInfo();
    }

    public string? GetLatestInstalledAdminVersion()
    {
        // TODO: Implement this
        return null;
    }

    public void InstallSdks(DotnetInstallRoot dotnetRoot, ProgressContext progressContext, IEnumerable<string> sdkVersions)
    {
        foreach (var channelVersion in sdkVersions)
        {
            InstallSDK(dotnetRoot, new UpdateChannel(channelVersion));
        }
    }

    private static void InstallSDK(DotnetInstallRoot dotnetRoot, UpdateChannel channel)
    {
        DotnetInstallRequest request = new DotnetInstallRequest(
            dotnetRoot,
            channel,
            InstallComponent.SDK,
            new InstallRequestOptions()
        );

        InstallResult installResult = InstallerOrchestratorSingleton.Instance.Install(request);
        Spectre.Console.AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]Installed .NET SDK {installResult.Install.Version}, available via {installResult.Install.InstallRoot}[/]");
    }

    public void UpdateGlobalJson(string globalJsonPath, string? sdkVersion = null)
    {
        if (sdkVersion is null)
        {
            return;
        }

        if (!File.Exists(globalJsonPath))
        {
            return;
        }

        var (fileText, encoding) = GlobalJsonFileHelper.ReadFileWithEncodingDetection(globalJsonPath);
        var updatedText = ReplaceGlobalJsonSdkVersion(fileText, sdkVersion);
        if (updatedText is not null)
        {
            File.WriteAllText(globalJsonPath, updatedText, encoding);
        }
    }

    /// <summary>
    /// Replaces the "version" value inside the "sdk" section of a global.json string,
    /// preserving all existing formatting. Uses Utf8JsonReader to find exact token positions.
    /// </summary>
    internal static string? ReplaceGlobalJsonSdkVersion(string jsonText, string newVersion)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(jsonText);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        int depth = 0;
        bool inSdkObject = false;
        bool foundVersionProperty = false;
        bool nextValueIsSdk = false;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    if (nextValueIsSdk)
                    {
                        inSdkObject = true;
                        nextValueIsSdk = false;
                    }
                    foundVersionProperty = false;
                    depth++;
                    break;

                case JsonTokenType.EndObject:
                    if (inSdkObject && depth == 2)
                    {
                        inSdkObject = false;
                    }
                    foundVersionProperty = false;
                    depth--;
                    break;

                case JsonTokenType.PropertyName:
                    if (depth == 1 && reader.ValueTextEquals("sdk"u8))
                    {
                        nextValueIsSdk = true;
                    }
                    else if (inSdkObject && depth == 2 && reader.ValueTextEquals("version"u8))
                    {
                        foundVersionProperty = true;
                    }
                    break;

                case JsonTokenType.String:
                    nextValueIsSdk = false;
                    if (foundVersionProperty)
                    {
                        // Found the version value token — splice in the new version at its byte position.
                        // TokenStartIndex is a UTF-8 byte offset, so splice on the byte array
                        // rather than on the C# string to handle non-ASCII characters correctly.
                        int tokenStart = (int)reader.TokenStartIndex;
                        int tokenLength = (int)reader.BytesConsumed - tokenStart;
                        byte[] replacementBytes = System.Text.Encoding.UTF8.GetBytes($"\"{newVersion}\"");

                        byte[] result = new byte[bytes.Length - tokenLength + replacementBytes.Length];
                        bytes.AsSpan(0, tokenStart).CopyTo(result);
                        replacementBytes.CopyTo(result.AsSpan(tokenStart));
                        bytes.AsSpan(tokenStart + tokenLength).CopyTo(result.AsSpan(tokenStart + replacementBytes.Length));

                        return System.Text.Encoding.UTF8.GetString(result);
                    }
                    break;

                default:
                    nextValueIsSdk = false;
                    foundVersionProperty = false;
                    break;
            }
        }

        return null;
    }

    public void ConfigureInstallType(InstallType installType, string? dotnetRoot = null)
    {
        if (OperatingSystem.IsWindows())
        {
            // On Windows, use InstallRootManager for proper configuration
            var installRootManager = new InstallRootManager(this);

            switch (installType)
            {
                case InstallType.User:
                    if (string.IsNullOrEmpty(dotnetRoot))
                    {
                        throw new ArgumentNullException(nameof(dotnetRoot));
                    }

                    var userChanges = installRootManager.GetUserInstallRootChanges();
                    bool succeeded = InstallRootManager.ApplyUserInstallRoot(
                        userChanges,
                        AnsiConsole.WriteLine,
                        msg => AnsiConsole.MarkupLine($"[red]{msg}[/]"));

                    if (!succeeded)
                    {
                        throw new InvalidOperationException("Failed to configure user install root.");
                    }
                    break;

                case InstallType.Admin:
                    var adminChanges = installRootManager.GetAdminInstallRootChanges();
                    bool adminSucceeded = InstallRootManager.ApplyAdminInstallRoot(
                        adminChanges,
                        AnsiConsole.WriteLine,
                        msg => AnsiConsole.MarkupLine($"[red]{msg}[/]"));

                    if (!adminSucceeded)
                    {
                        throw new InvalidOperationException("Failed to configure admin install root.");
                    }
                    break;

                default:
                    throw new ArgumentException($"Unknown install type: {installType}", nameof(installType));
            }
        }
        else
        {
            // Non-Windows platforms: use the simpler PATH-based approach
            // Get current PATH
            var path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
            var pathEntries = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
            string exeName = "dotnet";
            // Remove only actual dotnet installation folders from PATH
            pathEntries = [.. pathEntries.Where(p => !File.Exists(Path.Combine(p, exeName)))];

            switch (installType)
            {
                case InstallType.User:
                    if (string.IsNullOrEmpty(dotnetRoot))
                    {
                        throw new ArgumentNullException(nameof(dotnetRoot));
                    }
                    // Add dotnetRoot to PATH
                    pathEntries.Insert(0, dotnetRoot);
                    // Set DOTNET_ROOT
                    Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetRoot, EnvironmentVariableTarget.User);
                    break;
                case InstallType.Admin:
                    if (string.IsNullOrEmpty(dotnetRoot))
                    {
                        throw new ArgumentNullException(nameof(dotnetRoot));
                    }
                    // Add dotnetRoot to PATH
                    pathEntries.Insert(0, dotnetRoot);
                    // Unset DOTNET_ROOT
                    Environment.SetEnvironmentVariable("DOTNET_ROOT", null, EnvironmentVariableTarget.User);
                    break;
                default:
                    throw new ArgumentException($"Unknown install type: {installType}", nameof(installType));
            }
            // Update PATH
            var newPath = string.Join(Path.PathSeparator, pathEntries);
            Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
        }
    }
}
