// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public abstract class BaseTest
    {
        /// <summary>
        /// Gets a path to the folder with dotnet new test assets.
        /// </summary>
        public static string DotnetNewTestAssets { get; } = VerifyExists(Path.Combine(SdkTestContext.Current.TestAssetsDirectory, "TestPackages", "dotnet-new"));

        /// <summary>
        /// Gets a path to the folder with dotnet new test NuGet template packages.
        /// </summary>
        public static string DotnetNewTestPackagesBasePath { get; } = VerifyExists(Path.Combine(DotnetNewTestAssets, "nupkg_templates"));

        /// <summary>
        /// Gets a path to the folder with dotnet new test templates.
        /// </summary>
        public static string DotnetNewTestTemplatesBasePath { get; } = VerifyExists(Path.Combine(DotnetNewTestAssets, "test_templates"));

        /// <summary>
        /// Gets a path to the repo root folder (may be null when running in Helix).
        /// </summary>
        public static string? CodeBaseRoot { get; } = GetRepoRoot();

        /// <summary>
        /// Gets a path to the template packages maintained in the repo (/template_feed).
        /// </summary>
        public static string RepoTemplatePackages { get; } = GetTemplatePackagesDirectory();

        /// <summary>
        /// Gets a path to the test template with a <paramref name="templateName"/> name.
        /// </summary>
        public static string GetTestTemplateLocation(string templateName)
        {
            string templateLocation = Path.GetFullPath(Path.Combine(DotnetNewTestTemplatesBasePath, templateName));
            if (!Directory.Exists(templateLocation))
            {
                Assert.Fail($"The test template '{templateName}' does not exist.");
            }
            return templateLocation;
        }

        private static string VerifyExists(string folder)
        {
            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder))
            {
                Assert.Fail($"The folder '{folder}' does not exist.");
            }
            return folder;
        }

        private static string GetTemplatePackagesDirectory()
        {
            string? envDir = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_TEMPLATE_PACKAGES_DIRECTORY");
            if (!string.IsNullOrEmpty(envDir))
            {
                return VerifyExists(envDir);
            }
            if (CodeBaseRoot is null)
            {
                Assert.Fail("The repo root could not be determined and DOTNET_SDK_TEST_TEMPLATE_PACKAGES_DIRECTORY is not set.");
            }
            return VerifyExists(Path.Combine(CodeBaseRoot, "template_feed"));
        }

        private static string? GetRepoRoot()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(SdkTestContext.Current.TestAssetsDirectory, "..", ".."));
            if (!Directory.Exists(repoRoot) || !File.Exists(Path.Combine(repoRoot, "sdk.slnx")))
            {
                // Running in Helix or another environment where the full repo isn't available.
                return null;
            }
            return repoRoot;
        }
    }
}
