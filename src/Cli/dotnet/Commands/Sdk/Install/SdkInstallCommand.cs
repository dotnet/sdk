// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Spectre.Console;

using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Install;

internal class SdkInstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string? _versionOrChannel = result.GetValue(SdkInstallCommandParser.ChannelArgument);
    private readonly string? _installPath = result.GetValue(SdkInstallCommandParser.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(SdkInstallCommandParser.SetDefaultInstallOption);
    private readonly bool? _updateGlobalJson = result.GetValue(SdkInstallCommandParser.UpdateGlobalJsonOption);
    private readonly bool _interactive = result.GetValue(SdkInstallCommandParser.InteractiveOption);



    public override int Execute()
    {
        //bool? updateGlobalJson = null;

        //var updateGlobalJsonOption = _parseResult.GetResult(SdkInstallCommandParser.UpdateGlobalJsonOption)!;
        //if (updateGlobalJsonOption.Implicit)
        //{

        //}

        //Reporter.Output.WriteLine($"Update global.json: {_updateGlobalJson}");

        string? globalJsonPath = FindGlobalJson();

        string? currentInstallPath;
        DefaultInstall defaultInstallState = GetDefaultInstallState(out currentInstallPath);

        string? resolvedInstallPath = null;

        string? installPathFromGlobalJson = null;
        if (globalJsonPath != null)
        {
            installPathFromGlobalJson = ResolveInstallPathFromGlobalJson(globalJsonPath);

            if (installPathFromGlobalJson != null && _installPath != null &&
                //  TODO: Is there a better way to compare paths that takes into account whether the file system is case-sensitive?
                !installPathFromGlobalJson.Equals(_installPath, StringComparison.OrdinalIgnoreCase))
            {
                //  TODO: Add parameter to override error
                Reporter.Error.WriteLine($"Error: The install path specified in global.json ({installPathFromGlobalJson}) does not match the install path provided ({_installPath}).");
                return 1;
            }

            resolvedInstallPath = installPathFromGlobalJson;
        }

        if (resolvedInstallPath == null)
        {
            resolvedInstallPath = _installPath;
        }

        if (resolvedInstallPath == null && defaultInstallState == DefaultInstall.User)
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
                        .DefaultValue(GetDefaultInstallPath()));
            }
            else
            {
                //  If no install path is specified, use the default install path
                resolvedInstallPath = GetDefaultInstallPath();
            }
        }

        string? channelFromGlobalJson = null;
        if (globalJsonPath != null)
        {
            channelFromGlobalJson = ResolveChannelFromGlobalJson(globalJsonPath);
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
            Console.WriteLine($".NET SDK {channelFromGlobalJson} will be installed since {globalJsonPath} specifies that version.");

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

                Console.WriteLine("Available supported channels: " + string.Join(' ', GetAvailableChannels()));
                Console.WriteLine("You can also specify a specific version (for example 9.0.304).");

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
                if (defaultInstallState == DefaultInstall.None)
                {
                    resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                        "Do you want to set this install path as the default dotnet install? This will update the PATH and DOTNET_ROOT environment variables.",
                        defaultValue: true);
                }
                else if (defaultInstallState == DefaultInstall.User)
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
                else if (defaultInstallState == DefaultInstall.Admin)
                {
                    Console.WriteLine($"You have an existing admin install of .NET in {currentInstallPath}. We can configure your system to use the new install of .NET " +
                        "in {resolvedInstallPath} instead. This would mean that the admin install of .NET would no longer be accessible from the PATH or from Visual Studio.");
                    Console.WriteLine("You can change this later with the \"dotnet defaultinstall\" command.");
                    resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                        "Do you want to set this install path as the default dotnet install? This will update the PATH and DOTNET_ROOT environment variables.",
                        defaultValue: true);
                }
                else if (defaultInstallState == DefaultInstall.Inconsistent)
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




        Console.WriteLine($"Installing .NET SDK '{resolvedChannel}' to '{resolvedInstallPath}'...");

        string downloadLink = "https://builds.dotnet.microsoft.com/dotnet/Sdk/9.0.303/dotnet-sdk-9.0.303-win-x64.exe";

        // Download the file to a temp path with progress
        string tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetFileName(downloadLink));
        using (var httpClient = new System.Net.Http.HttpClient())
        {
            SpectreAnsiConsole.Progress()
                .Start(ctx =>
                {
                    var task = ctx.AddTask($"Downloading {Path.GetFileName(downloadLink)}");
                    using (var response = httpClient.GetAsync(downloadLink, System.Net.Http.HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                    {
                        response.EnsureSuccessStatusCode();
                        var contentLength = response.Content.Headers.ContentLength ?? 0;
                        using (var stream = response.Content.ReadAsStream())
                        using (var fileStream = File.Create(tempFilePath))
                        {
                            var buffer = new byte[81920];
                            long totalRead = 0;
                            int read;
                            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                fileStream.Write(buffer, 0, read);
                                totalRead += read;
                                if (contentLength > 0)
                                {
                                    task.Value = (double)totalRead / contentLength * 100;
                                }
                            }
                            task.Value = 100;
                        }
                    }
                });
        }
        Console.WriteLine($"Downloaded to: {tempFilePath}");


        return 0;
    }


    string? FindGlobalJson()
    {
        //return null;
        return @"d:\git\dotnet-sdk\global.json";
    }

    string? ResolveInstallPathFromGlobalJson(string globalJsonPath)
    {
        return Env.GetEnvironmentVariable("DOTNET_TESTHOOK_GLOBALJSON_SDK_INSTALL_PATH");
    }

    string? ResolveChannelFromGlobalJson(string globalJsonPath)
    {
        //return null;
        //return "9.0";
        return Env.GetEnvironmentVariable("DOTNET_TESTHOOK_GLOBALJSON_SDK_CHANNEL");
    }

    string GetDefaultInstallPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dotnet");
    }

    List<string> GetAvailableChannels()
    {
        return ["latest", "preview", "10", "10.0.1xx", "9", "9.0.3xx", "9.0.2xx", "9.0.1xx"];
    }

    enum DefaultInstall
    {
        None,
        //  Inconsistent would be when the dotnet on the path doesn't match what DOTNET_ROOT is set to
        Inconsistent,
        Admin,
        User
    }

    DefaultInstall GetDefaultInstallState(out string? currentInstallPath)
    {
        currentInstallPath = null;
        return DefaultInstall.None;
    }

    bool IsElevated()
    {
        return false;
    }
}
