// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class DiscoverStaticWebAssetsTest
    {
        private readonly Func<string, string, (FileInfo file, long fileLength, DateTimeOffset lastWriteTimeUtc)> _testResolveFileDetails =
            (string identity, string originalItemSpec) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero));

        [Fact]
        public void DiscoversMatchingAssetsBasedOnPattern()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TestResolveFileDetails = _testResolveFileDetails,
                CandidateAssets =
                [
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"))
                ],
                RelativePathPattern = "wwwroot\\**",
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true, $"Errors: {Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errorMessages)}");
            task.Assets.Length.Should().Be(1);
            var asset = task.Assets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().Be("MyProject");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().Be("Discovered");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be("_content/Path");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().Be("candidate.js");
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().Be("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().Be(Path.Combine("wwwroot", "candidate.js"));
        }

        [Theory]
        [InlineData("index.js", "index#[.{fingerprint}]?.js", "")]
        [InlineData("css/site.css", "css/site#[.{fingerprint}]!.css", "#[.{fingerprint}]!")]
        public void FingerprintsContentWhenEnabled(string file, string expectedRelativePath, string expression)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TestResolveFileDetails = _testResolveFileDetails,
                CandidateAssets =
                [
                    CreateCandidate(Path.Combine("wwwroot", file))
                ],
                RelativePathPattern = "wwwroot\\**",
                FingerprintCandidates = true,
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };
            if (!string.IsNullOrEmpty(expression))
            {
                task.FingerprintPatterns = [new TaskItem("CssFile", new Dictionary<string, string> { ["Pattern"] = "*.css", ["Expression"] = expression })];
            }

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true, $"Errors: {Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errorMessages)}");
            task.Assets.Length.Should().Be(1);
            var asset = task.Assets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", file)));
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().Be("MyProject");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().Be("Discovered");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be("_content/Path");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().Be(expectedRelativePath);
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().Be("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().Be(Path.Combine("wwwroot", file));
        }

        [Theory]
        [InlineData("index.js")]
        [InlineData("css/site.js")]
        public void DoesNotFingerprintsContentWhenNotEnabled(string candidate)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TestResolveFileDetails = _testResolveFileDetails,
                CandidateAssets =
                [
                    CreateCandidate(Path.Combine("wwwroot", candidate.Replace('/', Path.DirectorySeparatorChar)))
                ],
                RelativePathPattern = "wwwroot\\**",
                FingerprintCandidates = false,
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true, $"Errors: {Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errorMessages)}");
            task.Assets.Length.Should().Be(1);
            var asset = task.Assets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", candidate)));
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().Be("MyProject");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().Be("Discovered");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be("_content/Path");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().Be(candidate);
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().Be("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().Be(Path.Combine("wwwroot", Path.Combine(candidate.Split('/'))));
        }

        [Theory]
        [InlineData("candidate.lib.module.js", "candidate#[.{fingerprint}]?.lib.module.js", "")]
        [InlineData("library.candidate.lib.module.js", "library.candidate#[.{fingerprint}]!.lib.module.js", "#[.{fingerprint}]!")]
        public void FingerprintsContentUsingPatternsWhenMoreThanOneExtension(string fileName, string expectedRelativePath, string expression)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TestResolveFileDetails = _testResolveFileDetails,
                CandidateAssets =
                [
                    CreateCandidate(Path.Combine("wwwroot", fileName))
                ],
                FingerprintPatterns = [new TaskItem("JsModule", new Dictionary<string, string> { ["Pattern"] = "*.lib.module.js", ["Expression"] = expression })],
                FingerprintCandidates = true,
                RelativePathPattern = "wwwroot\\**",
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true, $"Errors: {Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errorMessages)}");
            task.Assets.Length.Should().Be(1);
            var asset = task.Assets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", fileName)));
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().Be("MyProject");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().Be("Discovered");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be("_content/Path");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().Be(expectedRelativePath);
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().Be("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().Be(Path.Combine("wwwroot", fileName));
        }

        [Fact]
        public void RespectsItemRelativePathWhenExplicitlySpecified()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TestResolveFileDetails = _testResolveFileDetails,
                CandidateAssets =
                [
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), relativePath: "subdir/candidate.js")
                ],
                RelativePathPattern = "wwwroot\\**",
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true, $"Errors: {Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errorMessages)}");
            task.Assets.Length.Should().Be(1);
            var asset = task.Assets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().Be("MyProject");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().Be("Discovered");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be("_content/Path");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().Be("subdir/candidate.js");
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().Be("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().Be(Path.Combine("wwwroot", "candidate.js"));
        }

        [Fact]
        public void UsesTargetPathWhenFound()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TestResolveFileDetails = _testResolveFileDetails,
                CandidateAssets =
                [
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), targetPath: Path.Combine("wwwroot", "subdir", "candidate.publish.js"))
                ],
                RelativePathPattern = "wwwroot\\**",
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true, $"Errors: {Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errorMessages)}");
            task.Assets.Length.Should().Be(1);
            var asset = task.Assets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().Be("MyProject");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().Be("Discovered");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be("_content/Path");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().Be("subdir/candidate.publish.js");
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().Be("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().Be(Path.Combine("wwwroot", "candidate.js"));
        }

        [Fact]
        public void UsesLinkPathWhenFound()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TestResolveFileDetails = _testResolveFileDetails,
                CandidateAssets =
                [
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), link: Path.Combine("wwwroot", "subdir", "candidate.link.js"))
                ],
                RelativePathPattern = "wwwroot\\**",
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true, $"Errors: {Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errorMessages)}");
            task.Assets.Length.Should().Be(1);
            var asset = task.Assets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().Be("MyProject");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().Be("Discovered");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be("_content/Path");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().Be("subdir/candidate.link.js");
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().Be("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().Be(Path.Combine("wwwroot", "candidate.js"));
        }

        [Fact]
        public void AutomaticallyDetectsAssetKindWhenMultipleAssetsTargetTheSameRelativePath()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TestResolveFileDetails = _testResolveFileDetails,
                CandidateAssets =
                [
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), copyToPublishDirectory: "Never"),
                    CreateCandidate(Path.Combine("wwwroot", "candidate.publish.js"), relativePath: "candidate.js")
                ],
                RelativePathPattern = "wwwroot\\**",
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true, $"Errors: {Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errorMessages)}");
            task.Assets.Length.Should().Be(2);
            var buildAsset = task.Assets.Single(a => a.ItemSpec == Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            var publishAsset = task.Assets.Single(a => a.ItemSpec == Path.GetFullPath(Path.Combine("wwwroot", "candidate.publish.js")));
            buildAsset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            buildAsset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("Build");
            buildAsset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            buildAsset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("Never");

            publishAsset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.publish.js")));
            publishAsset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("Publish");
            publishAsset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            publishAsset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
        }

        [Theory]
        [InlineData("Never", "Never", "Build", "Never", "Never", "Build")]
        [InlineData("PreserveNewest", "PreserveNewest", "All", "PreserveNewest", "PreserveNewest", "All")]
        [InlineData("Always", "Always", "All", "Always", "Always", "All")]
        [InlineData("Never", "Always", "All", "Never", "Always", "All")]
        [InlineData("Always", "Never", "Build", "Always", "Never", "Build")]
        public void FailsDiscoveringAssetsWhenThereIsAConflict(
            string copyToOutputDirectoryFirst,
            string copyToPublishDirectoryFirst,
            string firstKind,
            string copyToOutputDirectorySecond,
            string copyToPublishDirectorySecond,
            string secondKind)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TestResolveFileDetails = _testResolveFileDetails,
                CandidateAssets =
                [
                    CreateCandidate(
                        Path.Combine("wwwroot","candidate.js"),
                        copyToOutputDirectory: copyToOutputDirectoryFirst,
                        copyToPublishDirectory: copyToPublishDirectoryFirst),

                    CreateCandidate(
                        Path.Combine("wwwroot","candidate.publish.js"),
                        relativePath: "candidate.js",
                        copyToOutputDirectory: copyToOutputDirectorySecond,
                        copyToPublishDirectory: copyToPublishDirectorySecond)
                ],
                RelativePathPattern = "wwwroot\\**",
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(false);
            errorMessages.Count.Should().Be(1);
            errorMessages[0].Should().Be($@"Two assets found targeting the same path with incompatible asset kinds: 
'{Path.GetFullPath(Path.Combine("wwwroot", "candidate.js"))}' with kind '{firstKind}'
'{Path.GetFullPath(Path.Combine("wwwroot", "candidate.publish.js"))}' with kind '{secondKind}'
for path 'candidate.js'");
        }

        [Theory]
        [InlineData("\\_content\\Path\\", "_content/Path")]
        [InlineData("\\_content\\Path", "_content/Path")]
        [InlineData("_content\\Path", "_content/Path")]
        [InlineData("/_content/Path/", "_content/Path")]
        [InlineData("/_content/Path", "_content/Path")]
        [InlineData("_content/Path", "_content/Path")]
        [InlineData("\\_content/Path\\", "_content/Path")]
        [InlineData("/_content\\Path/", "_content/Path")]
        [InlineData("", "/")]
        [InlineData("/", "/")]
        [InlineData("\\", "/")]
        public void NormalizesBasePath(string givenPath, string expectedPath)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TestResolveFileDetails = _testResolveFileDetails,
                CandidateAssets =
                [
                    CreateCandidate("wwwroot\\candidate.js")
                ],
                RelativePathPattern = "wwwroot\\**",
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = givenPath
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true, $"Errors: {Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errorMessages)}");
            task.Assets.Length.Should().Be(1);
            var asset = task.Assets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be(expectedPath);
        }

        public static TheoryData<string, string> NormalizesContentRootData
        {
            get
            {
                var currentPath = Path.GetFullPath(".");
                var result = new TheoryData<string, string>
                {
                    { "wwwroot", Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar },
                    { currentPath + Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar + "subdir", Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar },
                    { currentPath + Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar + "subdir" + Path.DirectorySeparatorChar, Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar },
                    { currentPath + Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar + "subdir" + Path.AltDirectorySeparatorChar, Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar },
                    { currentPath + Path.AltDirectorySeparatorChar + "wwwroot" + Path.AltDirectorySeparatorChar + "subdir", Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar },
                    { currentPath + Path.DirectorySeparatorChar + "wwwroot" + Path.AltDirectorySeparatorChar + "subdir", Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar },
                    { currentPath + Path.AltDirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar + "subdir", Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar }
                };
                return result;
            }
        }

        [Theory]
        [MemberData(nameof(NormalizesContentRootData))]
        public void NormalizesContentRoot(string contentRoot, string expected)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TestResolveFileDetails = _testResolveFileDetails,
                CandidateAssets =
                [
                    CreateCandidate("wwwroot\\candidate.js")
                ],
                RelativePathPattern = "wwwroot\\**",
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = contentRoot,
                BasePath = "base"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true, $"Errors: {Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errorMessages)}");
            task.Assets.Length.Should().Be(1);
            var asset = task.Assets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(expected);
        }

        [Fact]
        public void DefineStaticWebAssetsCache_UpToDate()
        {
            // Arrange
            var (cache, inputHashes) = SetupCache([], []);
            // Assert
            cache.Update([], [], [], inputHashes);

            // Assert
            Assert.True(cache.IsUpToDate());
        }

        [Fact]
        public void DefineStaticWebAssetsCache_UpToDate_WithAssets()
        {
            // Arrange
            var (cache, inputHashes) = SetupCache(["input1"], ["input1"]);

            // Act
            cache.Update([], [], [], inputHashes);

            // Assert
            Assert.True(cache.IsUpToDate());
        }

        [Theory]
        [InlineData(UpdatedHash.GlobalProperties)]
        [InlineData(UpdatedHash.FingerprintPatterns)]
        [InlineData(UpdatedHash.Overrides)]
        public void DefineStaticWebAssetsCache_Recomputes_All_WhenPropertiesChange(UpdatedHash updated)
        {
            // Arrange
            var (cache, inputHashes) = SetupCache(["input1", "input2"], ["input1", "input2"]);

            // Act
            switch (updated)
            {
                case UpdatedHash.GlobalProperties:
                    cache.Update([1], [], [], inputHashes);
                    break;
                case UpdatedHash.FingerprintPatterns:
                    cache.Update([], [1], [], inputHashes);
                    break;
                case UpdatedHash.Overrides:
                    cache.Update([], [], [1], inputHashes);
                    break;
            }

            Assert.False(cache.IsUpToDate());
            Assert.Same(inputHashes, cache.OutOfDateInputs());
        }

        [Fact]
        public void DefineStaticWebAssetsCache_PartialUpdate_WhenOnlySome_InputsChange()
        {
            // Arrange
            var (cache, inputHashes) = SetupCache(["input1"], ["input2"], appendCachedToInputHashes: true);
            var cachedAsset = cache.CachedAssets.Values.Single();

            // Act
            cache.Update([], [], [], inputHashes);

            // Assert
            Assert.False(cache.IsUpToDate());
            Assert.NotSame(inputHashes, cache.OutOfDateInputs());
            var input1 = Assert.Single(cache.OutOfDateInputs());
            var ouput = cache.GetComputedOutputs();
            var input2 = Assert.Single(ouput.Assets);
        }

        [Fact]
        public void DefineStaticWebAssetsCache_PartialUpdate_NewAssetsCanBeAddedToTheCache()
        {
            // Arrange
            var (cache, inputHashes) = SetupCache(["input1"], ["input2"], appendCachedToInputHashes: true);
            cache.Update([], [], [], inputHashes);

            // Act
            var newAssetItem = inputHashes["input1"];
            var newAsset = new StaticWebAsset { Identity = newAssetItem.ItemSpec };
            cache.AppendAsset("input1", newAsset, newAssetItem);

            // Assert
            Assert.False(cache.IsUpToDate());
            Assert.NotSame(inputHashes, cache.OutOfDateInputs());
            var input1 = Assert.Single(cache.OutOfDateInputs());
            Assert.Contains("input1", cache.CachedAssets.Keys);

            var ouput = cache.GetComputedOutputs();
            Assert.Equal(2, ouput.Assets.Count);
            Assert.Equal("input2", ouput.Assets[0].ItemSpec);
            Assert.Equal("input1", ouput.Assets[1].ItemSpec);
        }

        [Fact]
        public void DefineStaticWebAssetsCache_CanRoundtripManifest()
        {
            var manifestPath = Path.Combine(Environment.CurrentDirectory, "CanRoundtripManifest.json");
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
            try
            {
                var (cache, inputHashes) = SetupCache([], [], appendCachedToInputHashes: true, manifestPath: manifestPath);

                var cachedAsset = CreateCandidate(Path.Combine(Environment.CurrentDirectory, "Input2.txt"), "Input2.txt");
                cache.InputHashes = ["input2"];
                cache.CachedAssets["input2"] = new StaticWebAsset { Identity = cachedAsset.ItemSpec, RelativePath = "Input2.txt" };
                inputHashes["input2"] = cachedAsset;

                var newAsset = CreateCandidate(Path.Combine(Environment.CurrentDirectory, "Input1.txt"), "Input1.txt");
                inputHashes["input1"] = newAsset;

                cache.Update([], [], [], inputHashes);
                cache.AppendAsset("input1", new StaticWebAsset { Identity = newAsset.ItemSpec, RelativePath = "Input1.txt" }, newAsset);
                cache.WriteCacheManifest();

                var otherManifest = DefineStaticWebAssets.DefineStaticWebAssetsCache.ReadOrCreateCache(CreateLogger(), manifestPath);
                Assert.Equal(cache.InputHashes, otherManifest.InputHashes);
                Assert.Equal(cache.CachedAssets.Count, otherManifest.CachedAssets.Count);
                Assert.Equal(cache.CachedAssets["input2"].Identity, otherManifest.CachedAssets["input2"].Identity);
                Assert.Equal(cache.CachedAssets["input2"].RelativePath, otherManifest.CachedAssets["input2"].RelativePath);
                Assert.Equal(cache.CachedAssets["input1"].Identity, otherManifest.CachedAssets["input1"].Identity);
                Assert.Equal(cache.CachedAssets["input1"].RelativePath, otherManifest.CachedAssets["input1"].RelativePath);
            }
            finally
            {
                File.Delete(manifestPath);
            }
        }
        private static TaskLoggingHelper CreateLogger()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));
            var loggingHelper = new TaskLoggingHelper(buildEngine.Object, "DefineStaticWebAssets");
            return loggingHelper;
        }

        private (DefineStaticWebAssets.DefineStaticWebAssetsCache cache, Dictionary<string, ITaskItem> inputHashes) SetupCache(
            string[] newAssets,
            string[] cached,
            bool appendCachedToInputHashes = false,
            string manifestPath = null)
        {
            var loggingHelper = CreateLogger();
            var cache = DefineStaticWebAssets.DefineStaticWebAssetsCache.ReadOrCreateCache(loggingHelper, manifestPath);
            cache.InputHashes = [.. cached];
            cache.CachedAssets = cached.ToDictionary(c => c, c => new StaticWebAsset { Identity = c });

            return (cache, newAssets.Concat(appendCachedToInputHashes ? cached : []).ToDictionary(c => c, c => new TaskItem(c) as ITaskItem));
        }

        public enum UpdatedHash
        {
            GlobalProperties,
            FingerprintPatterns,
            Overrides
        }

        private static ITaskItem CreateCandidate(
            string itemSpec,
            string relativePath = null,
            string targetPath = null,
            string link = null,
            string copyToOutputDirectory = null,
            string copyToPublishDirectory = null)
        {
            return new TaskItem(itemSpec, new Dictionary<string, string>
            {
                ["RelativePath"] = relativePath ?? "",
                ["TargetPath"] = targetPath ?? "",
                ["Link"] = link ?? "",
                ["CopyToOutputDirectory"] = copyToOutputDirectory ?? "",
                ["CopyToPublishDirectory"] = copyToPublishDirectory ?? "",
                // Add these to avoid accessing the disk to compute them
                ["Integrity"] = "integrity",
                ["Fingerprint"] = "fingerprint",
                ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                ["FileLength"] = "10",
            });
        }
    }
}
