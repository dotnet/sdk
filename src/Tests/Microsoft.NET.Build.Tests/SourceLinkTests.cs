// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class SourceLinkTests : SdkTest
    {
        private static readonly Guid s_embeddedSourceKindGuid = new("0E8A571B-6926-466E-B4AD-8AB04611F5FE");

        public SourceLinkTests(ITestOutputHelper log)
            : base(log)
        {
        }

        private void CreateGitFiles(string repoDir, string originUrl, string commitSha = "1200000000000000000000000000000000000000")
        {
            var gitDir = Path.Combine(repoDir, ".git");
            var headsDir = Path.Combine(gitDir, "refs", "heads");

            Directory.CreateDirectory(gitDir);
            File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/master");
            Directory.CreateDirectory(headsDir);

            if (commitSha != null)
            {
                File.WriteAllText(Path.Combine(headsDir, "master"), commitSha);
            }

            if (originUrl != null)
            {
                File.WriteAllText(Path.Combine(gitDir, "config"), $"""
                [remote "origin"]
                    url = {originUrl}
                """);
            }

            File.WriteAllText(Path.Combine(repoDir, ".gitignore"), """
                [Bb]in/
                [Oo]bj/
                """);
        }

        private unsafe void ValidatePdb(string pdbPath, bool expectedEmbeddedSources)
        {
            // Validates that *.AssemblyAttributes.cs file is embedded in the PDB.

            var pdb = File.ReadAllBytes(pdbPath);
            fixed (byte* pdbPtr = pdb)
            {
                var mdReader = new MetadataReader(pdbPtr, pdb.Length);
                var attrDocHandle = mdReader.Documents.Single(h => mdReader.GetString(mdReader.GetDocument(h).Name).EndsWith(".AssemblyAttributes.cs"));
                var cdis = mdReader.GetCustomDebugInformation(attrDocHandle);

                Assert.Equal(expectedEmbeddedSources, cdis.Any(h => mdReader.GetGuid(mdReader.GetCustomDebugInformation(h).Kind) == s_embeddedSourceKindGuid));
            }
        }

        [Fact]
        public void WithNoGitMetadata()
        {
            // We need to copy the test project to a directory outside of the SDK repo,
            // otherwise we would find .git directory in the SDK repo root.

            var testAsset = _testAssetsManager
                .CopyTestAsset("SourceLinkTestApp", testDestinationDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var intermediateDir = buildCommand.GetIntermediateDirectory();
            intermediateDir.Should().NotHaveFile("SourceLinkTestApp.sourcelink.json");
        }

        /// <summary>
        /// When creating a new repository locally we want the build to work and not report warnings even before the remote is set.
        /// </summary>
        [Fact]
        public void WithNoRemoteNoCommit()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("SourceLinkTestApp")
                .WithSource();

            CreateGitFiles(testAsset.Path, originUrl: null, commitSha: null);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().NotHaveStdOutContaining("warning");

            var intermediateDir = buildCommand.GetIntermediateDirectory();
            intermediateDir.Should().NotHaveFile("SourceLinkTestApp.sourcelink.json");
        }

        /// <summary>
        /// When creating a new repository locally we want the build to work and not report warnings even before the remote is set.
        /// </summary>
        [Fact]
        public void WithNoRemote()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("SourceLinkTestApp")
                .WithSource();

            CreateGitFiles(testAsset.Path, originUrl: null);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().NotHaveStdOutContaining("warning");

            var intermediateDir = buildCommand.GetIntermediateDirectory();
            intermediateDir.Should().NotHaveFile("SourceLinkTestApp.sourcelink.json");
        }

        [Fact]
        public void WithRemoteOrigin_UnknownDomain()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("SourceLinkTestApp")
                .WithSource();

            CreateGitFiles(testAsset.Path, originUrl: "https://contoso.com");

            var buildCommand = new BuildCommand(testAsset)
            {
                WorkingDirectory = testAsset.Path
            };

            var result = buildCommand.Execute().Should().Pass();

            var intermediateDir = buildCommand.GetIntermediateDirectory();
            intermediateDir.Should().NotHaveFile("SourceLinkTestApp.sourcelink.json");
        }

        [Theory]
        [InlineData("https://github.com/org/repo", "https://raw.githubusercontent.com/org/repo/1200000000000000000000000000000000000000/*", true)]
        [InlineData("https://github.com/org/repo", "https://raw.githubusercontent.com/org/repo/1200000000000000000000000000000000000000/*", false)]
        [InlineData("https://gitlab.com/org/repo", "https://gitlab.com/org/repo/-/raw/1200000000000000000000000000000000000000/*")]
        [InlineData("https://bitbucket.org/org/repo", "https://api.bitbucket.org/2.0/repositories/org/repo/src/1200000000000000000000000000000000000000/*")]
        [InlineData("https://test.visualstudio.com/org/_git/repo", "https://test.visualstudio.com/org/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=1200000000000000000000000000000000000000&path=/*")]
        public void WithRemoteOrigin_KnownDomain(string origin, string expectedLink, bool multitarget = false)
        {
            string targetFrameworks = null;

            var testAsset = _testAssetsManager
                .CopyTestAsset("SourceLinkTestApp", identifier: origin)
                .WithSource()
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;
                    var tfmNode = p.Root.Descendants().Single(e => e.Name.LocalName == "TargetFramework");

                    targetFrameworks = tfmNode.Value;
                    if (multitarget)
                    {
                        tfmNode.Name = ns + "TargetFrameworks";
                        targetFrameworks += ";netstandard2.0";
                        tfmNode.Value = targetFrameworks;
                    }
                });

            Assert.NotNull(targetFrameworks);

            CreateGitFiles(testAsset.Path, origin);

            var buildCommand = new BuildCommand(testAsset)
            {
                WorkingDirectory = testAsset.Path
            };

            var result = buildCommand.Execute().Should().Pass();

            foreach (var targetFramework in targetFrameworks.Split(';'))
            {
                var intermediateDir = buildCommand.GetIntermediateDirectory(targetFramework: targetFramework);
                var sourceLinkFilePath = Path.Combine(intermediateDir.FullName, "SourceLinkTestApp.sourcelink.json");
                var actualContent = File.ReadAllText(sourceLinkFilePath, Encoding.UTF8);
                var expectedPattern = Path.Combine(testAsset.Path, "*").Replace("\\", "\\\\");

                Assert.Equal($$$"""{"documents":{"{{{expectedPattern}}}":"{{{expectedLink}}}"}}""", actualContent);

                ValidatePdb(Path.Combine(intermediateDir.FullName, "SourceLinkTestApp.pdb"), expectedEmbeddedSources: true);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SuppressImplicitGitSourceLink_SetExplicitly(bool multitarget)
        {
            string targetFrameworks = null;

            var testAsset = _testAssetsManager
                .CopyTestAsset("SourceLinkTestApp")
                .WithSource()
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;
                    var tfmNode = p.Root.Descendants().Single(e => e.Name.LocalName == "TargetFramework");

                    targetFrameworks = tfmNode.Value;
                    if (multitarget)
                    {
                        tfmNode.Name = ns + "TargetFrameworks";
                        targetFrameworks += ";netstandard2.0";
                        tfmNode.Value = targetFrameworks;
                    }

                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    p.Root.Add(propertyGroup);

                    propertyGroup.Add(new XElement(ns + "SuppressImplicitGitSourceLink", "true"));
                });

            Assert.NotNull(targetFrameworks);

            CreateGitFiles(testAsset.Path, "https://github.com/org/repo");

            var buildCommand = new BuildCommand(testAsset)
            {
                WorkingDirectory = testAsset.Path
            };

            var result = buildCommand.Execute().Should().Pass();

            foreach (var targetFramework in targetFrameworks.Split(';'))
            {
                var intermediateDir = buildCommand.GetIntermediateDirectory(targetFramework: targetFramework);
                intermediateDir.Should().NotHaveFile("SourceLinkTestApp.sourcelink.json");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SuppressImplicitGitSourceLink_ExplicitPackage(bool multitarget)
        {
            string targetFrameworks = null;

            var testAsset = _testAssetsManager
                .CopyTestAsset("SourceLinkTestApp")
                .WithSource()
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;
                    var tfmNode = p.Root.Descendants().Single(e => e.Name.LocalName == "TargetFramework");

                    targetFrameworks = tfmNode.Value;
                    if (multitarget)
                    {
                        tfmNode.Name = ns + "TargetFrameworks";
                        targetFrameworks += ";netstandard2.0";
                        tfmNode.Value = targetFrameworks;
                    }

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "PackageReference",
                                    new XAttribute("Include", "Microsoft.SourceLink.GitHub"),
                                    new XAttribute("Version", "1.0.0")));
                });

            Assert.NotNull(targetFrameworks);

            CreateGitFiles(testAsset.Path, "https://github.com/org/repo");

            var buildCommand = new BuildCommand(testAsset)
            {
                WorkingDirectory = testAsset.Path
            };

            var result = buildCommand.Execute().Should().Pass();

            foreach (var targetFramework in targetFrameworks.Split(';'))
            {
                var intermediateDir = buildCommand.GetIntermediateDirectory(targetFramework: targetFramework);
                intermediateDir.Should().HaveFile("SourceLinkTestApp.sourcelink.json");

                // EmbedUntrackedSources is not set by default by SourceLink v1.0.0 package:
                ValidatePdb(Path.Combine(intermediateDir.FullName, "SourceLinkTestApp.pdb"), expectedEmbeddedSources: false);
            }
        }
    }
}
