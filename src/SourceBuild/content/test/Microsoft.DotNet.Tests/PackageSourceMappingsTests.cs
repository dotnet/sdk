using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.DotNet.UnifiedBuild.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests
{
    [Trait("Category", "PackageSourceMappings")]
    public class PackageSourceMappingsTests
    {
        private static readonly PackageSourceMappingsSetup TestSetup = PackageSourceMappingsSetup.Instance;
        private ITestOutputHelper OutputHelper { get; }

        public PackageSourceMappingsTests(ITestOutputHelper outputHelper)
        {
            OutputHelper = outputHelper;
        }

        // Unified build - with existing mappings - online
        [Fact]
        public void UnifiedBuildWithMappings()
        {
            string[] sources = ["source-built-arcade", "source-built-runtime", "net-sdk-supporting-feed"];
            RunTest("ub-mappings.config", true, sources, customSources: ["net-sdk-supporting-feed"], sourceBuild: false);
        }

        // Unified build - with mappings - online - no local sources
        [Fact]
        public void UnifiedBuildWithMappingsNoLocalSources()
        {
            string[] sources = ["net-sdk-supporting-feed"];
            RunTest("ub-mappings-nolocal.config", true, sources, customSources: ["net-sdk-supporting-feed"], sourceBuild: false);
        }

        // Unified build - no mappings - online
        [Fact]
        public void UnifiedBuildNoMappings()
        {
            string[] sources = ["source-built-arcade", "source-built-runtime", "net-sdk-supporting-feed"];
            RunTest("ub.config", true, sources, customSources: ["net-sdk-supporting-feed"], sourceBuild: false);
        }

        // Source build tests - with and without mappings - online and offline
        [Theory]
        [InlineData("sb-online.config", true)]
        [InlineData("sb-offline.config", false)]
        [InlineData("sb-mappings-online.config", true)]
        [InlineData("sb-mappings-offline.config", false)]
        public void SourceBuildTests(string nugetConfigFilename, bool useOnlineFeeds)
        {
            string[] sources = ["prebuilt", "previously-source-built", "reference-packages",
                                "source-built-arcade", "source-built-runtime"];
            RunTest(nugetConfigFilename, useOnlineFeeds, sources);
        }

        // Source build - SBRP repo - online and offline
        [Theory]
        [InlineData("sb-sbrp-offline.config", false)]
        [InlineData("sb-sbrp-online.config", true)]
        public void SourceBuildSbrpRepoTests(string nugetConfigFilename, bool useOnlineFeeds)
        {
            string[] sources = ["prebuilt", "previously-source-built", "reference-packages"];
            RunTest(nugetConfigFilename, useOnlineFeeds, sources);
        }

        private static void RunTest(string nugetConfigFilename, bool useOnlineFeeds, string[] sources, string[]? customSources = null, bool sourceBuild = true)
        {
            string psmAssetsDir = Path.Combine(Directory.GetCurrentDirectory(), "assets", "PackageSourceMappingsTests");
            string originalNugetConfig = Path.Combine(psmAssetsDir, "original", nugetConfigFilename);
            string expectedNugetConfig = Path.Combine(psmAssetsDir, "expected", nugetConfigFilename);

            string modifiedNugetConfig = Path.Combine(PackageSourceMappingsSetup.PackageSourceMappingsRoot, nugetConfigFilename);
            Directory.CreateDirectory(Path.GetDirectoryName(modifiedNugetConfig)!);
            File.Copy(originalNugetConfig, modifiedNugetConfig, true);
            UpdateNugetConfigTokens(modifiedNugetConfig);

            var task = new UpdateNuGetConfigPackageSourcesMappings()
            {
                SbrpCacheSourceName = "source-build-reference-package-cache",
                SbrpRepoSrcPath = TestSetup.SourceBuildReferencePackagesRepoDir,
                SourceBuiltSourceNamePrefix = "source-built-",
                NuGetConfigFile = modifiedNugetConfig,
                BuildWithOnlineFeeds = useOnlineFeeds,
                SourceBuildSources = sources,
                CustomSources = customSources
            };

            if (sourceBuild)
            {
                task.ReferencePackagesSourceName = "reference-packages";
                task.PreviouslySourceBuiltSourceName = "previously-source-built";
                task.PrebuiltSourceName = "prebuilt";
            }

            task.Execute();

            TokenizeLocalNugetConfigFeeds(modifiedNugetConfig);

            string expectedNugetConfigContents = File.ReadAllText(expectedNugetConfig);
            string modifiedNugetConfigContents = File.ReadAllText(modifiedNugetConfig);
            Assert.Equal(expectedNugetConfigContents, modifiedNugetConfigContents);
        }

        private static void UpdateNugetConfigTokens(string nugetConfigFile)
        {
            string fileContents = File.ReadAllText(nugetConfigFile);
            foreach (KeyValuePair<string, string> kvp in TestSetup.LocalTokenSourceMappings)
            {
                fileContents = fileContents.Replace(kvp.Key, kvp.Value);
            }
            File.WriteAllText(nugetConfigFile, fileContents);
        }

        private static void TokenizeLocalNugetConfigFeeds(string nugetConfigFile)
        {
            string fileContents = File.ReadAllText(nugetConfigFile);
            foreach (KeyValuePair<string, string> kvp in TestSetup.LocalTokenSourceMappings)
            {
                fileContents = fileContents.Replace(kvp.Value, kvp.Key);
            }
            File.WriteAllText(nugetConfigFile, fileContents);
        }

        internal class PackageSourceMappingsSetup
        {
            private static PackageSourceMappingsSetup? instance;
            private static readonly object myLock = new();
            private Dictionary<string, string>? localTokenSourceMappings;

            public static readonly string PackageSourceMappingsRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            private readonly string ArcadeSource = Path.Combine(PackageSourceMappingsRoot, "arcade");
            private readonly string RuntimeSource = Path.Combine(PackageSourceMappingsRoot, "runtime");
            private readonly string PreviouslySourceBuiltSource = Path.Combine(PackageSourceMappingsRoot, "previously-source-built");
            private readonly string ReferencePackagesSource = Path.Combine(PackageSourceMappingsRoot, "reference-packages");
            private readonly string PrebuiltSource = Path.Combine(PackageSourceMappingsRoot, "prebuilt");
            private readonly string SourceBuildReferencePackagesSource = Path.Combine(PackageSourceMappingsRoot, "source-build-reference-package-cache");

            public readonly string SourceBuildReferencePackagesRepoDir = Path.Combine(PackageSourceMappingsRoot, "sbrpDir");

            public Dictionary<string, string> LocalTokenSourceMappings
            {
                get
                {
                    localTokenSourceMappings ??= new Dictionary<string, string>
                        {
                            ["%arcade%"] = ArcadeSource,
                            ["%runtime%"] = RuntimeSource,
                            ["%previously-source-built%"] = PreviouslySourceBuiltSource,
                            ["%reference-packages%"] = ReferencePackagesSource,
                            ["%prebuilt%"] = PrebuiltSource,
                            ["%source-build-reference-package-cache%"] = SourceBuildReferencePackagesSource
                        };

                    return localTokenSourceMappings;
                }
            }

            public static PackageSourceMappingsSetup Instance
            {
                get
                {
                    lock (myLock)
                    {
                        instance ??= new PackageSourceMappingsSetup();
                    }

                    return instance;
                }
            }

            private PackageSourceMappingsSetup()
            {
                // Create the root directory
                Directory.CreateDirectory(PackageSourceMappingsRoot);

                // Generate Arcade nuget packages
                GenerateNuGetPackage(ArcadeSource, "Arcade.Package1", "1.0.0");
                GenerateNuGetPackage(ArcadeSource, "Arcade.Package2", "1.0.0");

                // Generate Runtime nuget packages
                GenerateNuGetPackage(RuntimeSource, "Runtime.Package1", "1.0.0");
                GenerateNuGetPackage(RuntimeSource, "Runtime.Package2", "1.0.0");

                // Generate SBRP nuget packages
                GenerateNuGetPackage(SourceBuildReferencePackagesSource, "SBRP.Package1", "1.0.0");
                GenerateNuGetPackage(SourceBuildReferencePackagesSource, "SBRP.Package2", "1.0.0");

                // Generate previously-source-built packages
                GenerateNuGetPackage(PreviouslySourceBuiltSource, "PSB.Package1", "1.0.0");
                GenerateNuGetPackage(PreviouslySourceBuiltSource, "PSB.Package2", "1.0.0");

                // Generate reference packages
                GenerateNuGetPackage(ReferencePackagesSource, "Reference.Package1", "1.0.0");
                GenerateNuGetPackage(ReferencePackagesSource, "Reference.Package2", "1.0.0");

                // Generate prebuilt packages
                GenerateNuGetPackage(PrebuiltSource, "Prebuilt.Package", "1.0.0");

                // Generate SBRP repo files - nuspecs
                GenerateNuspecFile(SourceBuildReferencePackagesRepoDir, "SBRP.Repo.Package1", "1.0.0");
                GenerateNuspecFile(SourceBuildReferencePackagesRepoDir, "SBRP.Repo.Package2", "1.0.0");
                GenerateNuspecFile(SourceBuildReferencePackagesRepoDir, "SBRP.Repo.Package3", "1.0.0");
                GenerateNuspecFile(SourceBuildReferencePackagesRepoDir, "SBRP.Repo.Package4", "1.0.0");
            }

            private static void GenerateNuGetPackage(string folder, string name, string version)
            {
                string nuspecPath = GenerateNuspecFile(folder, name, version);
                string packagePath = Path.ChangeExtension(nuspecPath, ".nupkg");

                using FileStream stream = File.OpenWrite(packagePath);
                using ZipArchive zipArchive = new(stream, ZipArchiveMode.Create);
                ZipArchiveEntry entry = zipArchive.CreateEntryFromFile(nuspecPath, Path.GetFileName(nuspecPath));
                File.Delete(nuspecPath);
            }

            private static string GenerateNuspecFile(string folder, string name, string version)
            {
                Directory.CreateDirectory(folder);

                var ns = XNamespace.Get("http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd");
                var doc = new XDocument(new XDeclaration("1.0", "utf-8", null));
                var root =
                    new XElement(ns + "package",
                        new XElement(ns + "metadata",
                            new XElement(ns + "id", name),
                            new XElement(ns + "version", version)
                        )
                    );
                doc.Add(root);

                string nuspecPath = Path.Combine(folder, $"{name}.{version}.nuspec");
                doc.Save(nuspecPath);
                return nuspecPath;
            }
        }
    }
}
