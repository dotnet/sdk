// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Tests
{
    /// <summary>
    /// The class contains the utils for unit and integration tests.
    /// </summary>
    public abstract class TestBase
    {
        internal static string CodeBaseRoot { get; } = GetCodeBaseRoot();

        internal static string ShippingPackagesLocation
        {
            get
            {
#if DEBUG
                string configuration = "Debug";
#elif RELEASE
                string configuration = "Release";
#else
                throw new NotSupportedException("The configuration is not supported");
#endif

                string packagesLocation = Path.Combine(CodeBaseRoot, "artifacts", "packages", configuration, "Shipping");

                if (!Directory.Exists(packagesLocation))
                {
                    throw new Exception($"{packagesLocation} does not exist");
                }
                return Path.GetFullPath(packagesLocation);
            }
        }

        internal static string TemplateFeedLocation { get; } = Path.Combine(CodeBaseRoot, "template_feed");

        internal static string TestTemplatesLocation { get; } = Path.Combine(CodeBaseRoot, "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates");

        internal static string SampleTemplatesLocation { get; } = Path.Combine(CodeBaseRoot, "dotnet-template-samples");

        internal static string TestTemplatePackagesLocation { get; } = Path.Combine(CodeBaseRoot, "test", "Microsoft.TemplateEngine.TestTemplates", "nupkg_templates");

        internal static string TestPackageProjectPath { get; } = Path.Combine(CodeBaseRoot, "test", "Microsoft.TemplateEngine.TestTemplates", "Microsoft.TemplateEngine.TestTemplates.csproj");

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

        private static string GetCodeBaseRoot()
        {
            string codebase = typeof(TestBase).Assembly.Location;
            string? codeBaseRoot = new FileInfo(codebase).Directory?.Parent?.Parent?.Parent?.Parent?.Parent?.FullName;

            if (string.IsNullOrEmpty(codeBaseRoot))
            {
                throw new InvalidOperationException("The codebase root was not found");
            }
            if (!File.Exists(Path.Combine(codeBaseRoot!, "Microsoft.TemplateEngine.sln")))
            {
                throw new InvalidOperationException("Microsoft.TemplateEngine.sln was not found in codebase root");
            }
            if (!Directory.Exists(Path.Combine(codeBaseRoot!, "test", "Microsoft.TemplateEngine.TestTemplates")))
            {
                throw new InvalidOperationException("Microsoft.TemplateEngine.TestTemplates was not found in test/");
            }
            return codeBaseRoot!;
        }
    }
}
