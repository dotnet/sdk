using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Spectre.Console;

using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install
{
    internal class EnvironmentVariableMockDotnetInstaller : IBootstrapperController
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
                latestAdminVersion = "10.0.0-preview.7";
            }
            return latestAdminVersion;
        }

        public void InstallSdks(string dotnetRoot, ProgressContext progressContext, IEnumerable<string> sdkVersions)
        {
            using (var httpClient = new HttpClient())
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
}
