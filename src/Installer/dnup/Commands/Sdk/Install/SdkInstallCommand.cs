// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Net.Http;
using Microsoft.Deployment.DotNet.Releases;
using Spectre.Console;


using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

internal class SdkInstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string? _versionOrChannel = result.GetValue(SdkInstallCommandParser.ChannelArgument);
    private readonly string? _installPath = result.GetValue(SdkInstallCommandParser.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(SdkInstallCommandParser.SetDefaultInstallOption);
    private readonly bool? _updateGlobalJson = result.GetValue(SdkInstallCommandParser.UpdateGlobalJsonOption);
    private readonly bool _interactive = result.GetValue(SdkInstallCommandParser.InteractiveOption);

    private readonly IBootstrapperController _dotnetInstaller = new EnvironmentVariableMockDotnetInstaller();
    private readonly IReleaseInfoProvider _releaseInfoProvider = new EnvironmentVariableMockReleaseInfoProvider();

    public override int Execute()
    {
        //bool? updateGlobalJson = null;

        //var updateGlobalJsonOption = _parseResult.GetResult(SdkInstallCommandParser.UpdateGlobalJsonOption)!;
        //if (updateGlobalJsonOption.Implicit)
        //{

        //}

        //Reporter.Output.WriteLine($"Update global.json: {_updateGlobalJson}");

        var globalJsonInfo = _dotnetInstaller.GetGlobalJsonInfo(Environment.CurrentDirectory);

        string? currentInstallPath;
        InstallType defaultInstallState = _dotnetInstaller.GetConfiguredInstallType(out currentInstallPath);

        string? resolvedInstallPath = null;

        string? installPathFromGlobalJson = null;
        if (globalJsonInfo?.GlobalJsonPath != null)
        {
            installPathFromGlobalJson = globalJsonInfo.SdkPath;

            if (installPathFromGlobalJson != null && _installPath != null &&
                //  TODO: Is there a better way to compare paths that takes into account whether the file system is case-sensitive?
                !installPathFromGlobalJson.Equals(_installPath, StringComparison.OrdinalIgnoreCase))
            {
                //  TODO: Add parameter to override error
                Console.Error.WriteLine($"Error: The install path specified in global.json ({installPathFromGlobalJson}) does not match the install path provided ({_installPath}).");
                return 1;
            }

            resolvedInstallPath = installPathFromGlobalJson;
        }

        if (resolvedInstallPath == null)
        {
            resolvedInstallPath = _installPath;
        }

        if (resolvedInstallPath == null && defaultInstallState == InstallType.User)
        {
            //  If a user installation is already set up, we don't need to prompt for the install path
            resolvedInstallPath = currentInstallPath;
        }

        if (resolvedInstallPath == null)
        {
            if (_interactive)
            {
                resolvedInstallPath = SpectreAnsiConsole.Prompt(
                    new TextPrompt<string>("Where should we install the .NET SDK to?)")
                        .DefaultValue(_dotnetInstaller.GetDefaultDotnetInstallPath()));
            }
            else
            {
                //  If no install path is specified, use the default install path
                resolvedInstallPath = _dotnetInstaller.GetDefaultDotnetInstallPath();
            }
        }

        string? channelFromGlobalJson = null;
        if (globalJsonInfo?.GlobalJsonPath != null)
        {
            channelFromGlobalJson = ResolveChannelFromGlobalJson(globalJsonInfo.GlobalJsonPath);
        }

        bool? resolvedUpdateGlobalJson = null;

        if (channelFromGlobalJson != null && _versionOrChannel != null &&
            //  TODO: Should channel comparison be case-sensitive?
            !channelFromGlobalJson.Equals(_versionOrChannel, StringComparison.OrdinalIgnoreCase))
        {
            if (_interactive && _updateGlobalJson == null)
            {
                resolvedUpdateGlobalJson = SpectreAnsiConsole.Confirm(
                    $"The channel specified in global.json ({channelFromGlobalJson}) does not match the channel specified ({_versionOrChannel}). Do you want to update global.json to match the specified channel?",
                    defaultValue: true);
            }
        }

        string? resolvedChannel = null;

        if (channelFromGlobalJson != null)
        {
            SpectreAnsiConsole.WriteLine($".NET SDK {channelFromGlobalJson} will be installed since {globalJsonInfo?.GlobalJsonPath} specifies that version.");

            resolvedChannel = channelFromGlobalJson;
        }
        else if (_versionOrChannel != null)
        {
            resolvedChannel = _versionOrChannel;
        }
        else
        {
            if (_interactive)
            {

                SpectreAnsiConsole.WriteLine("Available supported channels: " + string.Join(' ', _releaseInfoProvider.GetAvailableChannels()));
                SpectreAnsiConsole.WriteLine("You can also specify a specific version (for example 9.0.304).");

                resolvedChannel = SpectreAnsiConsole.Prompt(
                    new TextPrompt<string>("Which channel of the .NET SDK do you want to install?")
                        .DefaultValue("latest"));
            }
            else
            {
                resolvedChannel = "latest"; // Default to latest if no channel is specified
            }
        }

        bool? resolvedSetDefaultInstall = _setDefaultInstall;

        if (resolvedSetDefaultInstall == null)
        {
            //  If global.json specified an install path, we don't prompt for setting the default install path (since you probably don't want to do that for a repo-local path)
            if (_interactive && installPathFromGlobalJson == null)
            {
                if (defaultInstallState == InstallType.None)
                {
                    resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                        $"Do you want to set the install path ({resolvedInstallPath}) as the default dotnet install? This will update the PATH and DOTNET_ROOT environment variables.",
                        defaultValue: true);
                }
                else if (defaultInstallState == InstallType.User)
                {
                    //  Another case where we need to compare paths and the comparison may or may not need to be case-sensitive
                    if (resolvedInstallPath.Equals(currentInstallPath, StringComparison.OrdinalIgnoreCase))
                    {
                        //  No need to prompt here, the default install is already set up.
                    }
                    else
                    {
                        resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                            $"The default dotnet install is currently set to {currentInstallPath}.  Do you want to change it to {resolvedInstallPath}?",
                            defaultValue: false);
                    }
                }
                else if (defaultInstallState == InstallType.Admin)
                {
                    SpectreAnsiConsole.WriteLine($"You have an existing admin install of .NET in {currentInstallPath}. We can configure your system to use the new install of .NET " +
                        $"in {resolvedInstallPath} instead. This would mean that the admin install of .NET would no longer be accessible from the PATH or from Visual Studio.");
                    SpectreAnsiConsole.WriteLine("You can change this later with the \"dotnet defaultinstall\" command.");
                    resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                        $"Do you want to set the user install path ({resolvedInstallPath}) as the default dotnet install? This will update the PATH and DOTNET_ROOT environment variables.",
                        defaultValue: true);
                }
                else if (defaultInstallState == InstallType.Inconsistent)
                {
                    //  TODO: Figure out what to do here
                    resolvedSetDefaultInstall = false;
                }
            }
            else
            {
                resolvedSetDefaultInstall = false; // Default to not setting the default install path if not specified
            }
        }

        List<string> additionalVersionsToInstall = new();

        var resolvedChannelVersion = _releaseInfoProvider.GetLatestVersion(resolvedChannel);

        if (resolvedSetDefaultInstall == true && defaultInstallState == InstallType.Admin)
        {
            if (_interactive)
            {
                var latestAdminVersion = _dotnetInstaller.GetLatestInstalledAdminVersion();
                if (latestAdminVersion != null && new ReleaseVersion(resolvedChannelVersion) < new ReleaseVersion(latestAdminVersion))
                {
                    SpectreAnsiConsole.WriteLine($"Since the admin installs of the .NET SDK will no longer be accessible, we recommend installing the latest admin installed " +
                        $"version ({latestAdminVersion}) to the new user install location.  This will make sure this version of the .NET SDK continues to be used for projects that don't specify a .NET SDK version in global.json.");

                    if (SpectreAnsiConsole.Confirm($"Also install .NET SDK {latestAdminVersion}?",
                        defaultValue: true))
                    {
                        additionalVersionsToInstall.Add(latestAdminVersion);
                    }
                }
            }
            else
            {
                //  TODO: Add command-linen option for installing admin versions locally
            }
        }


        //  TODO: Implement transaction / rollback?
        //  TODO: Use Mutex to avoid concurrent installs?


        SpectreAnsiConsole.MarkupInterpolated($"Installing .NET SDK [blue]{resolvedChannelVersion}[/] to [blue]{resolvedInstallPath}[/]...");

        SpectreAnsiConsole.Progress()
            .Start(ctx =>
            {
                _dotnetInstaller.InstallSdks(resolvedInstallPath, ctx, new[] { resolvedChannelVersion }.Concat(additionalVersionsToInstall));
            });

        if (resolvedSetDefaultInstall == true)
        {
            _dotnetInstaller.ConfigureInstallType(InstallType.User, resolvedInstallPath);
        }

        if (resolvedUpdateGlobalJson == true)
        {
            _dotnetInstaller.UpdateGlobalJson(globalJsonInfo!.GlobalJsonPath!, resolvedChannelVersion, globalJsonInfo.AllowPrerelease, globalJsonInfo.RollForward);
        }


        SpectreAnsiConsole.WriteLine($"Complete!");


        return 0;
    }



    string? ResolveChannelFromGlobalJson(string globalJsonPath)
    {
        //return null;
        //return "9.0";
        return Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_GLOBALJSON_SDK_CHANNEL");
    }

    bool IsElevated()
    {
        return false;
    }

    class EnvironmentVariableMockDotnetInstaller : IBootstrapperController
    {
        public GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory)
        {
            return new GlobalJsonInfo
            {
                GlobalJsonPath = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_GLOBALJSON_PATH"),
                GlobalJsonContents = null // Set to null for test mock; update as needed for tests
            };
        }

        public string GetDefaultDotnetInstallPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dotnet");
        }

        public InstallType GetConfiguredInstallType(out string? currentInstallPath)
        {
            var testHookDefaultInstall = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_DEFAULT_INSTALL");
            InstallType returnValue = InstallType.None;
            if (!Enum.TryParse<InstallType>(testHookDefaultInstall, out returnValue))
            {
                returnValue = InstallType.None;
            }
            currentInstallPath = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_CURRENT_INSTALL_PATH");
            return returnValue;
        }

        public string? GetLatestInstalledAdminVersion()
        {
            var latestAdminVersion = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_LATEST_ADMIN_VERSION");
            if (string.IsNullOrEmpty(latestAdminVersion))
            {
                latestAdminVersion = "10.0.203";
            }
            return latestAdminVersion;
        }

        public void InstallSdks(string dotnetRoot, ProgressContext progressContext, IEnumerable<string> sdkVersions)
        {
            //var task = progressContext.AddTask($"Downloading .NET SDK {resolvedChannelVersion}");
            using (var httpClient = new System.Net.Http.HttpClient())
            {
                List<Action> downloads = sdkVersions.Select(version =>
                {
                    string downloadLink = "https://builds.dotnet.microsoft.com/dotnet/Sdk/9.0.303/dotnet-sdk-9.0.303-win-x64.exe";
                    var task = progressContext.AddTask($"Downloading .NET SDK {version}");
                    return (Action)(() =>
                    {
                        Download(downloadLink, httpClient, task);
                    });
                }).ToList();


                foreach (var download in downloads)
                {
                    download();
                }
            }
        }

        void Download(string url, HttpClient httpClient, ProgressTask task)
        {
            //string tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetFileName(url));
            //using (var response = httpClient.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
            //{
            //    response.EnsureSuccessStatusCode();
            //    var contentLength = response.Content.Headers.ContentLength ?? 0;
            //    using (var stream = response.Content.ReadAsStream())
            //    using (var fileStream = File.Create(tempFilePath))
            //    {
            //        var buffer = new byte[81920];
            //        long totalRead = 0;
            //        int read;
            //        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            //        {
            //            fileStream.Write(buffer, 0, read);
            //            totalRead += read;
            //            if (contentLength > 0)
            //            {
            //                task.Value = (double)totalRead / contentLength * 100;
            //            }
            //        }
            //        task.Value = 100;
            //    }
            //}

            for (int i = 0; i < 100; i++)
            {
                task.Increment(1);
                Thread.Sleep(20); // Simulate some work
            }
            task.Value = 100;
        }

        public void UpdateGlobalJson(string globalJsonPath, string? sdkVersion = null, bool? allowPrerelease = null, string? rollForward = null)
        {
            SpectreAnsiConsole.WriteLine($"Updating {globalJsonPath} to SDK version {sdkVersion} (AllowPrerelease={allowPrerelease}, RollForward={rollForward})");
        }
        public void ConfigureInstallType(InstallType installType, string? dotnetRoot = null)
        {
            SpectreAnsiConsole.WriteLine($"Configuring install type to {installType} (dotnetRoot={dotnetRoot})");
        }
    }

    class EnvironmentVariableMockReleaseInfoProvider : IReleaseInfoProvider
    {
        public List<string> GetAvailableChannels()
        {
            var channels = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_AVAILABLE_CHANNELS");
            if (string.IsNullOrEmpty(channels))
            {
                return ["latest", "preview", "10", "10.0.1xx", "10.0.2xx", "9", "9.0.3xx", "9.0.2xx", "9.0.1xx"];
            }
            return channels.Split(',').ToList();
        }
        public string GetLatestVersion(string channel)
        {
            if (channel == "preview")
            {
                return "11.0.100-preview.1.42424";
            }
            else if (channel == "latest" || channel == "10" || channel == "10.0.2xx")
            {
                return "10.0.203";
            }
            else if (channel == "10.0.1xx")
            {
                return "10.0.106";
            }
            else if (channel == "9" || channel == "9.0.3xx")
            {
                return "9.0.309";
            }
            else if (channel == "9.0.2xx")
            {
                return "9.0.212";
            }
            else if (channel == "9.0.1xx")
            {
                return "9.0.115";
            }

            return channel;
        }
    }
}
