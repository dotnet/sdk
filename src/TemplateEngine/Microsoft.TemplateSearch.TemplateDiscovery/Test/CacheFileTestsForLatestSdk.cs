// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Test
{
    internal static class CacheFileTestsForLatestSdk
    {
        public static void RunTests(string cacheFileV2Path, string sdkVersion)
        {
            cacheFileV2Path = Path.GetFullPath(cacheFileV2Path);
            Console.WriteLine($"Running tests on .NET {sdkVersion} for: {cacheFileV2Path}.");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            UseSdkVersion(workingDirectory, sdkVersion, resolvedVersionPattern: string.Join('.', sdkVersion.Split('.', 3).Take(2)) + '.', rollForward: "latestFeature");
            CanSearch(workingDirectory, cacheFileV2Path);
            Console.WriteLine($"Tests succeeded.");
        }

        private static void UseSdkVersion(string workingDirectory, string requestedSdkVersion, string resolvedVersionPattern, string rollForward = "latestMinor", bool allowPrerelease = false)
        {
            CreateGlobalJson(workingDirectory, requestedSdkVersion, rollForward, allowPrerelease);

            new DotnetCommand(TestOutputLogger.Instance, "--version")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining(resolvedVersionPattern);
        }

        private static void CanSearchWhileInstantiating(string workingDirectory, string cacheFilePath)
        {
            var settingsPath = TestUtils.CreateTemporaryFolder();
            new DotnetCommand(TestOutputLogger.Instance, "new", "func", "--debug:custom-hive", settingsPath)
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Couldn't find an installed template that matches the input, searching online for one that does...")
                .And.HaveStdOutContaining("Template name \"Azure Functions\" (func) from author \"Microsoft\" in pack Microsoft.Azure.WebJobs.ProjectTemplates");
        }

        private static void CanSearch(string workingDirectory, string cacheFilePath)
        {
            var settingsPath = TestUtils.CreateTemporaryFolder();
            new DotnetCommand(TestOutputLogger.Instance, "new", "func", "--search", "--debug:custom-hive", settingsPath)
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Exception")
                .And.HaveStdOutContaining("Microsoft.Azure.Functions.Worker.ProjectTemplates");
        }

        private static void CreateGlobalJson(string directory, string sdkVersion, string rollForward = "latestMinor", bool allowPrerelease = false)
        {
            string prereleaseSection = allowPrerelease ? @", ""allowPrerelease"": ""true""" : string.Empty;
            string jsonContent = $@"{{ ""sdk"": {{ ""version"": ""{sdkVersion}"", ""rollForward"": ""{rollForward}"" {prereleaseSection}}} }}";
            File.WriteAllText(Path.Combine(directory, "global.json"), jsonContent);
        }
    }
}
