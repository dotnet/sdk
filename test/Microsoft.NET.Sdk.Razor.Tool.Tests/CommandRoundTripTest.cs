// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.CodeAnalysis;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    /// <summary>
    /// Verifies that the discover command produces JSON that the generate command can consume.
    /// </summary>
    public class CommandRoundTripTest
    {
        [Fact]
        public void DiscoverThenGenerate_ManifestIsConsumedByGenerateCommand()
        {
            // Arrange - find framework ref assemblies
            var runtimeAssemblyDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var dotnetRoot = Path.GetFullPath(Path.Combine(runtimeAssemblyDir, "..", "..", ".."));

            var runtimeRefDir = GetLatestRefPackDirectory(dotnetRoot, "Microsoft.NETCore.App.Ref");
            var aspnetRefDir = GetLatestRefPackDirectory(dotnetRoot, "Microsoft.AspNetCore.App.Ref");

            Assert.True(Directory.Exists(runtimeRefDir), $"Runtime ref pack not found at {runtimeRefDir}");
            Assert.True(Directory.Exists(aspnetRefDir), $"ASP.NET ref pack not found at {aspnetRefDir}");

            var assemblies = Directory.GetFiles(runtimeRefDir, "*.dll")
                .Concat(Directory.GetFiles(aspnetRefDir, "*.dll"))
                .ToArray();

            var tempDir = Path.Combine(Path.GetTempPath(), "RazorDiscoverGenerateTest", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Run the discover command to produce a manifest
                var manifestPath = Path.Combine(tempDir, "manifest.json");
                var discoverExitCode = RunDiscoverCommand(assemblies, tempDir, manifestPath);
                Assert.True(discoverExitCode == 0, $"Discover command failed with exit code {discoverExitCode}");
                Assert.True(File.Exists(manifestPath), "Manifest file was not created");

                // Check that the AnchorTagHelper was discovered
                var manifestFile = File.ReadAllText(manifestPath);
                Assert.Contains("Microsoft.AspNetCore.Mvc.TagHelpers.AnchorTagHelper", manifestFile);

                // Create a simple .cshtml file that uses the anchor tag helper from the manifest
                var cshtmlPath = Path.Combine(tempDir, "TestView.cshtml");
                File.WriteAllText(cshtmlPath, """
                    @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
                    <a asp-action="Index" asp-controller="Home">Home</a>
                    """);

                var outputPath = Path.Combine(tempDir, "TestView.cshtml.g.cs");

                // Run the generate command with the manifest
                var errorWriter = new StringWriter();
                var application = CreateApplication(errorWriter: errorWriter);

                var args = new List<string>
                {
                    "generate",
                    "-s", cshtmlPath,
                    "-o", outputPath,
                    "-r", "TestView.cshtml",
                    "-k", "mvc",
                    "-p", tempDir,
                    "-t", manifestPath,
                    "-v", "Latest",
                    "-c", "MVC-3.0",
                };

                var generateExitCode = application.Execute(args.ToArray());

                // Make sure the generate command succeeded and produced output
                Assert.True(generateExitCode == 0, $"Generate command failed with exit code {generateExitCode}. Error: {errorWriter}");
                Assert.True(File.Exists(outputPath), "Generated C# file was not created");

                var generatedCode = File.ReadAllText(outputPath);
                Assert.NotEmpty(generatedCode);

                // The generated code should reference the AnchorTagHelper since we used asp-action/asp-controller
                Assert.Contains("global::Microsoft.AspNetCore.Mvc.TagHelpers.AnchorTagHelper", generatedCode);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        private static Application CreateApplication(TextWriter outputWriter = null, TextWriter errorWriter = null)
        {
            var checker = new Mock<ExtensionDependencyChecker>();
            checker.Setup(c => c.Check(It.IsAny<IEnumerable<string>>())).Returns(true);

            return new Application(
                CancellationToken.None,
                Mock.Of<ExtensionAssemblyLoader>(),
                checker.Object,
                (path, properties) => MetadataReference.CreateFromFile(path, properties),
                outputWriter ?? new StringWriter(),
                errorWriter ?? new StringWriter());
        }

        private static int RunDiscoverCommand(string[] assemblies, string projectDir, string manifestPath)
        {
            var application = CreateApplication();

            var args = new List<string> { "discover" };
            args.AddRange(assemblies);
            args.AddRange(["-o", Path.GetFileName(manifestPath), "-p", projectDir, "-v", "Latest", "-c", "MVC-3.0"]);

            return application.Execute(args.ToArray());
        }

        private static string GetLatestRefPackDirectory(string dotnetRoot, string packName)
        {
            var packsDir = Path.Combine(dotnetRoot, "packs", packName);
            if (!Directory.Exists(packsDir))
            {
                return packsDir;
            }

            var latestVersion = Directory.GetDirectories(packsDir)
                .OrderByDescending(d => d)
                .FirstOrDefault();

            if (latestVersion == null)
            {
                return packsDir;
            }

            var refDir = Path.Combine(latestVersion, "ref");
            if (!Directory.Exists(refDir))
            {
                return refDir;
            }

            // Get the TFM-specific subdirectory (e.g., net11.0)
            return Directory.GetDirectories(refDir)
                .OrderByDescending(d => d)
                .FirstOrDefault() ?? refDir;
        }
    }
}
