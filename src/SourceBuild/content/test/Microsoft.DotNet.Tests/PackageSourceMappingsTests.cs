// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

        private const string NetSdkSupportingFeedName = "net-sdk-supporting-feed";
        private const string ArcadeSourceName = "source-built-arcade";
        private const string RuntimeSourceName = "source-built-runtime";
        private const string PrebuiltSourceName = "prebuilt";
        private const string PreviouslySourceBuiltSourceName = "previously-source-built";
        private const string ReferencePackagesSourceName = "reference-packages";

        private ITestOutputHelper OutputHelper { get; }

        public PackageSourceMappingsTests(ITestOutputHelper outputHelper)
        {
            OutputHelper = outputHelper;
        }

        // Build with mappings - online - no local sources
        [Fact]
        public void BuildWithMappingsNoLocalSources()
        {
            string[] sources = [NetSdkSupportingFeedName];
            RunTest("ub-mappings-nolocal.config", true, sources, customSources: [NetSdkSupportingFeedName]);
        }

        // Build with local sources - mappings and no mappings - online
        [Theory]
        [InlineData("ub-mappings.config")]
        [InlineData("ub-nomappings.config")]
        public void BuildWithLocalSources(string nugetConfigFilename)
        {
            string[] sources = [ArcadeSourceName, RuntimeSourceName, NetSdkSupportingFeedName];
            RunTest(nugetConfigFilename, true, sources, customSources: [NetSdkSupportingFeedName]);
        }

        // Source build tests - with and without mappings - online and offline
        [Theory]
        [InlineData("sb-mappings-online.config", true)]
        [InlineData("sb-mappings-offline.config", false)]
        [InlineData("sb-nomappings-online.config", true)]
        [InlineData("sb-nomappings-offline.config", false)]
        public void SourceBuildTests(string nugetConfigFilename, bool useOnlineFeeds)
        {
            string[] sources = [PrebuiltSourceName, PreviouslySourceBuiltSourceName, ReferencePackagesSourceName,
                                ArcadeSourceName, RuntimeSourceName];
            RunTest(nugetConfigFilename, useOnlineFeeds, sources, sourceBuild: true);
        }

        // Source build - SBRP repo - online and offline
        [Theory]
        [InlineData("sb-sbrp-online.config", true)]
        [InlineData("sb-sbrp-offline.config", false)]
        public void SourceBuildSbrpRepoTests(string nugetConfigFilename, bool useOnlineFeeds)
        {
            string[] sources = [PrebuiltSourceName, PreviouslySourceBuiltSourceName, ReferencePackagesSourceName];
            RunTest(nugetConfigFilename, useOnlineFeeds, sources, sourceBuild: true);
        }

        private static void RunTest(string nugetConfigFilename, bool useOnlineFeeds, string[] sources, string[]? customSources = null, bool sourceBuild = false)
        {
            string psmAssetsDir = Path.Combine(Directory.GetCurrentDirectory(), "assets", nameof(PackageSourceMappingsTests));
            string originalNugetConfig = Path.Combine(psmAssetsDir, "original", nugetConfigFilename);
            string expectedNugetConfig = Path.Combine(psmAssetsDir, "expected", nugetConfigFilename);

            string modifiedNugetConfig = Path.Combine(PackageSourceMappingsSetup.PackageSourceMappingsRoot, nugetConfigFilename);
            Directory.CreateDirectory(Path.GetDirectoryName(modifiedNugetConfig)!);
            File.Copy(originalNugetConfig, modifiedNugetConfig, true);
            UpdateNugetConfigTokens(modifiedNugetConfig);

            var task = new UpdateNuGetConfigPackageSourcesMappings()
            {
                SbrpCacheSourceName = "source-build-reference-package-cache",
                SbrpRepoSrcPath = TestSetup.SourceBuildReferencePackagesRepo,
                SourceBuiltSourceNamePrefix = "source-built-",
                NuGetConfigFile = modifiedNugetConfig,
                BuildWithOnlineFeeds = useOnlineFeeds,
                SourceBuildSources = sources,
                CustomSources = customSources
            };

            if (sourceBuild)
            {
                task.ReferencePackagesSourceName = ReferencePackagesSourceName;
                task.PreviouslySourceBuiltSourceName = PreviouslySourceBuiltSourceName;
                task.PrebuiltSourceName = PrebuiltSourceName;
            }

            task.Execute();

            TokenizeLocalNugetConfigFeeds(modifiedNugetConfig);

            string expectedNugetConfigContents = File.ReadAllText(expectedNugetConfig);
            string modifiedNugetConfigContents = File.ReadAllText(modifiedNugetConfig);
            Assert.Equal(expectedNugetConfigContents, modifiedNugetConfigContents);
        }

        private static void UpdateNugetConfigTokens(string nugetConfigFile)
        {
            ApplyLocalTokenSourceMappings(nugetConfigFile, updateTokens: true);
        }

        private static void TokenizeLocalNugetConfigFeeds(string nugetConfigFile)
        {
            ApplyLocalTokenSourceMappings(nugetConfigFile, tokenize: true);
        }

        private static void ApplyLocalTokenSourceMappings(string nugetConfigFile, bool updateTokens = false, bool tokenize = false)
        {
            if (updateTokens == tokenize)
            {
                throw new InvalidOperationException($"One and only one option should be true, '{nameof(updateTokens)}' or '{nameof(tokenize)}'");
            }

            string fileContents = File.ReadAllText(nugetConfigFile);
            foreach (KeyValuePair<string, string> kvp in TestSetup.LocalTokenSourceMappings)
            {
                fileContents = updateTokens
                    ? fileContents.Replace(kvp.Key, kvp.Value)
                    : fileContents.Replace(kvp.Value, kvp.Key);
            }
            File.WriteAllText(nugetConfigFile, fileContents);
        }

        internal class PackageSourceMappingsSetup
        {
            private static PackageSourceMappingsSetup? instance;
            private static readonly object myLock = new();

            public static readonly string PackageSourceMappingsRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            private Dictionary<string, string>? localTokenSourceMappings;
            private readonly string ArcadeSource = Path.Combine(PackageSourceMappingsRoot, "arcade");
            private readonly string RuntimeSource = Path.Combine(PackageSourceMappingsRoot, "runtime");
            private readonly string PreviouslySourceBuiltSource = Path.Combine(PackageSourceMappingsRoot, "previously-source-built");
            private readonly string ReferencePackagesSource = Path.Combine(PackageSourceMappingsRoot, "reference-packages");
            private readonly string PrebuiltSource = Path.Combine(PackageSourceMappingsRoot, "prebuilt");
            private readonly string SourceBuildReferencePackagesSource = Path.Combine(PackageSourceMappingsRoot, "source-build-reference-package-cache");

            public readonly string SourceBuildReferencePackagesRepo = Path.Combine(PackageSourceMappingsRoot, "sbrp");

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
                GenerateNuspecFile(SourceBuildReferencePackagesRepo, "SBRP.Repo.Package1", "1.0.0");
                GenerateNuspecFile(SourceBuildReferencePackagesRepo, "SBRP.Repo.Package2", "1.0.0");
                GenerateNuspecFile(SourceBuildReferencePackagesRepo, "SBRP.Repo.Package3", "1.0.0");
                GenerateNuspecFile(SourceBuildReferencePackagesRepo, "SBRP.Repo.Package4", "1.0.0");
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
