// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateEngine.CommandUtils;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Test
{
    internal static class CacheFileTests
    {
        internal static void RunTests(string metadataPath, string legacyMetadataPath)
        {
            //3.1
            string sdkVersion = "3.1.400";
            Console.WriteLine($"Running tests on .NET {sdkVersion} for: {legacyMetadataPath}.");
            string workingDirectory = TestUtils.CreateTemporaryFolder(sdkVersion);
            UseSdkVersion(workingDirectory, requestedSdkVersion: sdkVersion, resolvedVersionPattern: "3.");
            CanSearchWhileInstantiating(workingDirectory, legacyMetadataPath);
            CanCheckUpdates(workingDirectory, legacyMetadataPath);
            CanUpdate(workingDirectory, legacyMetadataPath);

            //5.0
            sdkVersion = "5.0.100";
            Console.WriteLine($"Running tests on .NET {sdkVersion} for: {legacyMetadataPath}.");
            workingDirectory = TestUtils.CreateTemporaryFolder(sdkVersion);
            UseSdkVersion(workingDirectory, requestedSdkVersion: sdkVersion, resolvedVersionPattern: "5.0.", rollForward: "latestFeature");
            CanSearchWhileInstantiating(workingDirectory, legacyMetadataPath);
            CanCheckUpdates(workingDirectory, legacyMetadataPath);
            CanUpdate(workingDirectory, legacyMetadataPath);

            //5.0.400
            sdkVersion = "5.0.400";
            Console.WriteLine($"Running tests on .NET {sdkVersion} for: {legacyMetadataPath}.");
            workingDirectory = TestUtils.CreateTemporaryFolder(sdkVersion);
            UseSdkVersion(workingDirectory, requestedSdkVersion: sdkVersion, resolvedVersionPattern: "5.0.", rollForward: "latestFeature");
            CanSearch(workingDirectory, legacyMetadataPath);
            CanCheckUpdates(workingDirectory, legacyMetadataPath);
            CanUpdate(workingDirectory, legacyMetadataPath);

            //6.0.100
            sdkVersion = "6.0.100";
            workingDirectory = TestUtils.CreateTemporaryFolder(sdkVersion);
            UseSdkVersion(workingDirectory, requestedSdkVersion: "6.0.100", resolvedVersionPattern: "6.0.", rollForward: "latestFeature");
            Console.WriteLine($"Running tests on .NET {sdkVersion} for: {legacyMetadataPath}.");
            CanSearch(workingDirectory, legacyMetadataPath);
            Console.WriteLine($"Running tests on .NET {sdkVersion} for: {metadataPath}.");
            CanSearch(workingDirectory, metadataPath);

            //latest
            workingDirectory = TestUtils.CreateTemporaryFolder("latest");
            //print the version
            new DotnetCommand(TestOutputLogger.Instance, "--version")
                .WithoutTelemetry()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0);
            Console.WriteLine($"Running tests on latest .NET for: {legacyMetadataPath}.");
            CanSearch(workingDirectory, legacyMetadataPath);
            Console.WriteLine($"Running tests on latest .NET for: {metadataPath}.");
            CanSearch(workingDirectory, metadataPath);
        }

        private static void UseSdkVersion(string workingDirectory, string requestedSdkVersion, string resolvedVersionPattern, string rollForward = "latestMinor", bool allowPrerelease = false)
        {
            CreateGlobalJson(workingDirectory, requestedSdkVersion, rollForward, allowPrerelease);

            new DotnetCommand(TestOutputLogger.Instance, "--version")
                .WithoutTelemetry()
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
            new DotnetNewCommand(TestOutputLogger.Instance, "func", "--debug:custom-hive", settingsPath)
                .WithoutTelemetry()
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Couldn't find an installed template that matches the input, searching online for one that does...")
                .And.HaveStdOutContaining("Template name \"Azure Functions\" (func) from author \"Microsoft\" in pack Microsoft.Azure.WebJobs.ProjectTemplates");
        }

        private static void CanCheckUpdates(string workingDirectory, string cacheFilePath)
        {
            var settingsPath = TestUtils.CreateTemporaryFolder();
            new DotnetNewCommand(TestOutputLogger.Instance, "--install", "Microsoft.Azure.WebJobs.ItemTemplates::2.1.1785", "--debug:custom-hive", settingsPath)
              .WithoutTelemetry()
              .WithWorkingDirectory(workingDirectory)
              .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
              .Execute()
              .Should()
              .ExitWith(0)
              .And.NotHaveStdErr();

            new DotnetNewCommand(TestOutputLogger.Instance, "--update-check", "--debug:custom-hive", settingsPath)
                .WithoutTelemetry()
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Exception")
                .And.HaveStdOutContaining("Updates are available for the following:")
                .And.HaveStdOutContaining("Microsoft.Azure.WebJobs.ItemTemplates::2.1.1785");
        }

        private static void CanUpdate(string workingDirectory, string cacheFilePath)
        {
            var settingsPath = TestUtils.CreateTemporaryFolder();
            new DotnetNewCommand(TestOutputLogger.Instance, "--install", "Microsoft.Azure.WebJobs.ItemTemplates::2.1.1785", "--debug:custom-hive", settingsPath)
                .WithoutTelemetry()
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            new DotnetNewCommand(TestOutputLogger.Instance, "--update-apply", "--debug:custom-hive", settingsPath)
                .WithoutTelemetry()
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Exception")
                .And.HaveStdOutMatching("Update succeeded")
                .And.HaveStdOutContaining("Microsoft.Azure.WebJobs.ItemTemplates::2.1.1785");
        }

        private static void CanSearch(string workingDirectory, string cacheFilePath)
        {
            var settingsPath = TestUtils.CreateTemporaryFolder();
            new DotnetNewCommand(TestOutputLogger.Instance, "func", "--search", "--debug:custom-hive", settingsPath)
                .WithoutTelemetry()
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
