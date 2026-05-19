// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Tests
{
    /// <summary>
    /// The class contains the utils for unit and integration tests.
    /// Paths are resolved via <see cref="SdkTestContext"/> which handles both
    /// local (repo-rooted) and Helix (environment variable) environments.
    /// </summary>
    public abstract class TestBase
    {
        private static readonly Lazy<string> s_codeBaseRoot = new(() =>
            SdkTestContext.GetRepoRoot()
            ?? throw new InvalidOperationException(
                "Could not determine the repo root. Ensure .git exists in the directory tree or set the required DOTNET_SDK_TEST_* environment variables."));

        internal static string CodeBaseRoot => s_codeBaseRoot.Value;

        internal static string ShippingPackagesLocation
        {
            get
            {
                string? location = SdkTestContext.Current.ShippingPackagesDirectory;
                if (string.IsNullOrEmpty(location) || !Directory.Exists(location))
                {
                    throw new InvalidOperationException(
                        $"ShippingPackagesDirectory '{location}' does not exist. " +
                        "Set the DOTNET_SDK_ARTIFACTS_DIR environment variable or run from the repo root.");
                }
                return Path.GetFullPath(location);
            }
        }

        internal static string TemplateFeedLocation { get; } = SdkTestContext.Current.RepoTemplatePackages;

        internal static string ApprovalsDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Approvals");

        internal static string SnapshotsDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Snapshots");

        internal static string TestTemplatesLocation { get; } =
            Path.Combine(SdkTestContext.Current.TestAssetsDirectory, "TestPackages", "TemplateEngine", "test_templates");

        internal static string SampleTemplatesLocation
        {
            get
            {
                string? envSamplesDir = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_TEMPLATE_SAMPLES_DIR");
                if (!string.IsNullOrEmpty(envSamplesDir) && Directory.Exists(envSamplesDir))
                {
                    return envSamplesDir;
                }

                return Path.Combine(CodeBaseRoot, "documentation", "TemplateEngine", "Samples");
            }
        }

        internal static string TestTemplatePackagesLocation { get; } =
            Path.Combine(SdkTestContext.Current.TestAssetsDirectory, "TestPackages", "TemplateEngine", "nupkg_templates");

        internal static string TestPackageProjectPath { get; } =
            Path.Combine(SdkTestContext.Current.TestAssetsDirectory, "TestPackages", "TemplateEngine", "Microsoft.TemplateEngine.TestTemplates.csproj");

        internal static string PackTestTemplatesNuGetPackage(PackageManager packageManager)
        {
            return packageManager.PackNuGetPackage(TestPackageProjectPath);
        }

        internal static string GetTestTemplateLocation(string templateName)
        {
            string templateLocation = Path.Combine(TestTemplatesLocation, templateName);

            if (!Directory.Exists(templateLocation))
            {
                throw new Exception($"{templateLocation} does not exist");
            }
            return Path.GetFullPath(templateLocation);
        }

        /// <summary>
        /// Creates a NuGet.config in the specified directory with sources for both
        /// shipping packages and locally-built test packages.
        /// </summary>
        internal static void SetupNuGetConfigForPackagesLocation(string projectDirectory)
        {
            TestUtils.SetupNuGetConfigForPackagesLocation(
                projectDirectory,
                new[] { ShippingPackagesLocation, SdkTestContext.Current.TestPackages });
        }
    }
}
