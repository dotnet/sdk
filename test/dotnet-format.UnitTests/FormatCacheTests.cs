// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.CodeAnalysis.Tools.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.NET.TestFramework;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    [TestClass]
    public class FormatCacheTests : SdkTest
    {
        [TestMethod]
        public void CacheIsLimitedToWhitespaceFolderMode()
        {
            using var fixture = new CacheFixture();
            foreach (var workspaceType in new[] { WorkspaceType.Project, WorkspaceType.Solution })
            {
                using var cache = FormatCache.Create(fixture.Options with { WorkspaceType = workspaceType }, NullLogger.Instance);
                Assert.IsNull(cache);
            }

            foreach (var category in new[] { FixCategory.None, FixCategory.CodeStyle, FixCategory.Analyzers, FixCategory.Whitespace | FixCategory.CodeStyle })
            {
                using var cache = FormatCache.Create(fixture.Options with { FixCategory = category }, NullLogger.Instance);
                Assert.IsNull(cache);
            }

            using var disabledCache = FormatCache.Create(fixture.Options with { NoCache = true }, NullLogger.Instance);
            Assert.IsNull(disabledCache);
            foreach (var include in new[] { "../other/File.cs", fixture.FilePath })
            {
                using var outsideCache = FormatCache.Create(fixture.Options with
                {
                    FileMatcher = SourceFileMatcher.CreateMatcher(new[] { include }, Array.Empty<string>()),
                }, NullLogger.Instance);
                Assert.IsNull(outsideCache);
            }
        }

        [TestMethod]
        public void RoundTripDetectsChangedAndMissingFiles()
        {
            using var fixture = new CacheFixture();
            fixture.Save();
            using var cache = fixture.Create();
            Assert.IsTrue(IsUpToDate(cache, fixture.FilePath));
            File.WriteAllText(fixture.FilePath, "class C{ }");
            Assert.IsFalse(IsUpToDate(cache, fixture.FilePath));
            File.Delete(fixture.FilePath);
            Assert.IsFalse(IsUpToDate(cache, fixture.FilePath));
        }

        [TestMethod]
        public void OptionsInvalidateCacheWithoutAmbiguousGlobConcatenation()
        {
            using var fixture = new CacheFixture();
            var original = fixture.Options with
            {
                FileMatcher = SourceFileMatcher.CreateMatcher(new[] { "a", "bc" }, Array.Empty<string>()),
            };
            fixture.Save(original);
            var alternatives = new[]
            {
                original with { IncludeGeneratedFiles = true },
                original with { CodeStyleSeverity = DiagnosticSeverity.Error },
                original with { AnalyzerSeverity = DiagnosticSeverity.Error },
                original with { Diagnostics = ImmutableHashSet.Create("IDE0001") },
                original with { ExcludeDiagnostics = ImmutableHashSet.Create("IDE0001") },
                original with { TargetFramework = "different-framework" },
                original with { FileMatcher = SourceFileMatcher.CreateMatcher(new[] { "ab", "c" }, Array.Empty<string>()) },
                original with { FileMatcher = SourceFileMatcher.CreateMatcher(new[] { "a" }, new[] { "bc" }) },
            };
            foreach (var options in alternatives)
            {
                using var cache = fixture.Create(options);
                Assert.IsFalse(IsUpToDate(cache, fixture.FilePath));
            }

            using var reorderedCache = fixture.Create(original with
            {
                FileMatcher = SourceFileMatcher.CreateMatcher(new[] { "bc", "a" }, Array.Empty<string>()),
            });
            Assert.IsTrue(IsUpToDate(reorderedCache, fixture.FilePath));
        }

        [TestMethod]
        public void AncestorAndNestedEditorConfigsInvalidateCache()
        {
            using var fixture = new CacheFixture();
            var paths = new[]
            {
                Path.Combine(fixture.RootPath, ".editorconfig"),
                Path.Combine(fixture.Options.WorkspaceFilePath, ".editorconfig"),
                Path.Combine(fixture.Options.WorkspaceFilePath, "node_modules", "nested", ".editorconfig"),
            };
            foreach (var path in paths)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                fixture.Save();
                File.WriteAllText(path, "[*.cs]\nindent_size = 2");
                using (var cache = fixture.Create())
                {
                    Assert.IsFalse(IsUpToDate(cache, fixture.FilePath), path);
                }

                fixture.Save();
                File.WriteAllText(path, "[*.cs]\nindent_size = 4");
                using (var cache = fixture.Create())
                {
                    Assert.IsFalse(IsUpToDate(cache, fixture.FilePath), path);
                }

                fixture.Save();
                File.Delete(path);
                using var removedConfigCache = fixture.Create();
                Assert.IsFalse(IsUpToDate(removedConfigCache, fixture.FilePath), path);
            }
        }

        [TestMethod]
        public void ConfigChangesDuringRunPreventHitsAndRecording()
        {
            using var fixture = new CacheFixture();
            fixture.Save();
            using (var cache = fixture.Create())
            {
                File.WriteAllText(Path.Combine(fixture.Options.WorkspaceFilePath, ".editorconfig"), "[*.cs]\nindent_size = 2");
                Assert.IsFalse(IsUpToDate(cache, fixture.FilePath));
                cache.RecordFormattedFiles(fixture.ExpectedFiles(), new[] { fixture.FilePath });
            }

            using var reloadedCache = fixture.Create();
            Assert.IsFalse(IsUpToDate(reloadedCache, fixture.FilePath));
        }

        [TestMethod]
        public void ConfigChangesAfterRecordingPreventPublication()
        {
            using var fixture = new CacheFixture();
            using (var cache = fixture.Create())
            {
                cache.RecordFormattedFiles(fixture.ExpectedFiles(), new[] { fixture.FilePath });
                File.WriteAllText(Path.Combine(fixture.Options.WorkspaceFilePath, ".editorconfig"), "[*.cs]\nindent_size = 2");
            }

            Assert.IsFalse(File.Exists(fixture.CacheFilePath));
        }

        [TestMethod]
        public void ConcurrentEditIsNotRecordedAsFormatted()
        {
            using var fixture = new CacheFixture();
            fixture.Save();
            using (var cache = fixture.Create())
            {
                // The formatting snapshot is unchanged, but an editor changed the disk file.
                File.WriteAllText(fixture.FilePath, "class C{ }");
                cache.RecordFormattedFiles(fixture.ExpectedFiles(), new[] { fixture.FilePath });
            }

            using var reloadedCache = fixture.Create();
            Assert.IsFalse(IsUpToDate(reloadedCache, fixture.FilePath));
        }

        [TestMethod]
        public void FailedSaveIsNotRecordedAsFormatted()
        {
            using var fixture = new CacheFixture();
            var expected = SourceText.From("class Changed { }", new UTF8Encoding(false));
            using (var cache = fixture.Create())
            {
                cache.RecordFormattedFiles(new[] { KeyValuePair.Create(fixture.FilePath, expected) }, new[] { fixture.FilePath });
            }

            using var reloadedCache = fixture.Create();
            Assert.IsFalse(IsUpToDate(reloadedCache, fixture.FilePath));
        }

        [TestMethod]
        public void EditAfterRecordingDoesNotBecomeACacheHit()
        {
            using var fixture = new CacheFixture();
            using (var cache = fixture.Create())
            {
                cache.RecordFormattedFiles(fixture.ExpectedFiles(), new[] { fixture.FilePath });
                File.WriteAllText(fixture.FilePath, "class C{ }");
            }

            using var reloadedCache = fixture.Create();
            Assert.IsFalse(IsUpToDate(reloadedCache, fixture.FilePath));
        }

        [TestMethod]
        public void TextWithoutKnownEncodingIsNotRecorded()
        {
            using var fixture = new CacheFixture();
            using (var cache = fixture.Create())
            {
                cache.RecordFormattedFiles(new[] { KeyValuePair.Create(fixture.FilePath, SourceText.From("class C { }")) }, new[] { fixture.FilePath });
            }

            using var reloadedCache = fixture.Create();
            Assert.IsFalse(IsUpToDate(reloadedCache, fixture.FilePath));
        }

        [TestMethod]
        public void RecordsExactEncodingAndPreamble()
        {
            using var fixture = new CacheFixture();
            foreach (var encoding in new Encoding[] { new UTF8Encoding(false), new UTF8Encoding(true), Encoding.Unicode })
            {
                var text = SourceText.From("class C { }\r\n", encoding);
                File.WriteAllText(fixture.FilePath, text.ToString(), encoding);
                using (var cache = fixture.Create())
                {
                    cache.RecordFormattedFiles(new[] { KeyValuePair.Create(fixture.FilePath, text) }, new[] { fixture.FilePath });
                }

                using var reloadedCache = fixture.Create();
                Assert.IsTrue(IsUpToDate(reloadedCache, fixture.FilePath), encoding.WebName);
            }
        }

        [TestMethod]
        public void VerificationMayReadButDoesNotWrite()
        {
            using var fixture = new CacheFixture();
            var verifyOptions = fixture.Options with { SaveFormattedFiles = false };
            using (var cache = fixture.Create(verifyOptions))
            {
                cache.RecordFormattedFiles(fixture.ExpectedFiles(), new[] { fixture.FilePath });
            }

            Assert.IsFalse(File.Exists(fixture.CacheFilePath));
            fixture.Save();
            var before = File.ReadAllBytes(fixture.CacheFilePath);
            using (var cache = fixture.Create(verifyOptions))
            {
                Assert.IsTrue(IsUpToDate(cache, fixture.FilePath));
                cache.RecordFormattedFiles(Array.Empty<KeyValuePair<string, SourceText>>(), Array.Empty<string>());
            }

            File.ReadAllBytes(fixture.CacheFilePath).Should().Equal(before);
        }

        [TestMethod]
        public void PrunesRemovedFilesAndRetainsSkippedFiles()
        {
            using var fixture = new CacheFixture();
            fixture.Save();
            using (var cache = fixture.Create())
            {
                cache.RecordFormattedFiles(Array.Empty<KeyValuePair<string, SourceText>>(), new[] { fixture.FilePath });
            }

            using (var cache = fixture.Create())
            {
                Assert.IsTrue(IsUpToDate(cache, fixture.FilePath));
                cache.RecordFormattedFiles(Array.Empty<KeyValuePair<string, SourceText>>(), Array.Empty<string>());
            }

            using var reloadedCache = fixture.Create();
            Assert.IsFalse(IsUpToDate(reloadedCache, fixture.FilePath));
        }

        [TestMethod]
        public void CorruptCacheIsIgnored()
        {
            using var fixture = new CacheFixture();
            fixture.Save();
            File.WriteAllText(fixture.CacheFilePath, "{ invalid json");
            using var cache = fixture.Create();
            Assert.IsFalse(IsUpToDate(cache, fixture.FilePath));
        }

        [TestMethod]
        public void WriterDoesNotReuseSharedTemporaryPath()
        {
            using var fixture = new CacheFixture();
            fixture.Save();
            var sharedTempPath = fixture.CacheFilePath + ".tmp";
            File.WriteAllText(sharedTempPath, "another writer");
            try
            {
                File.Delete(fixture.CacheFilePath);
                fixture.Save();
                Assert.AreEqual("another writer", File.ReadAllText(sharedTempPath));
                using var cache = fixture.Create();
                Assert.IsTrue(IsUpToDate(cache, fixture.FilePath));
                Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(fixture.CacheFilePath)!, Path.GetFileName(fixture.CacheFilePath) + ".*.tmp"));
            }
            finally
            {
                File.Delete(sharedTempPath);
            }
        }

        [TestMethod]
        public void DistinctCaseSensitivePathsAreNotAliasedAfterReload()
        {
            using var fixture = new CacheFixture();
            var otherPath = Path.Combine(fixture.Options.WorkspaceFilePath, "FILE.cs");
            if (File.Exists(otherPath))
            {
                // This filesystem does not support distinct names differing only in case.
                return;
            }

            File.WriteAllText(otherPath, "class C { }");
            using (var cache = fixture.Create())
            {
                cache.RecordFormattedFiles(fixture.ExpectedFiles(), new[] { fixture.FilePath, otherPath });
                Assert.IsTrue(IsUpToDate(cache, fixture.FilePath));
                Assert.IsFalse(IsUpToDate(cache, otherPath));
            }

            using var reloadedCache = fixture.Create();
            Assert.IsTrue(IsUpToDate(reloadedCache, fixture.FilePath));
            Assert.IsFalse(IsUpToDate(reloadedCache, otherPath));
        }

        [TestMethod]
        public async Task WarmFolderCacheSkipsUnchangedDocumentsWithoutChangingFileCount()
        {
            using var fixture = new CacheFixture();
            fixture.Save();
            using var workspace = FolderWorkspace.Create();
            workspace.OpenFolder(fixture.Options.WorkspaceFilePath, fixture.Options.FileMatcher);
            using var cache = fixture.Create();
            var (cachedCount, cachedDocuments) = await CodeFormatter.DetermineFormattableFilesAsync(
                workspace.CurrentSolution, null, fixture.Options, cache, NullLogger.Instance, TestContext.CancellationToken);
            var (uncachedCount, uncachedDocuments) = await CodeFormatter.DetermineFormattableFilesAsync(
                workspace.CurrentSolution, null, fixture.Options, null, NullLogger.Instance, TestContext.CancellationToken);

            Assert.AreEqual(1, cachedCount);
            Assert.AreEqual(uncachedCount, cachedCount);
            Assert.IsEmpty(cachedDocuments);
            Assert.HasCount(1, uncachedDocuments);

            File.WriteAllText(fixture.FilePath, "class C{ }");
            var (editedCount, editedDocuments) = await CodeFormatter.DetermineFormattableFilesAsync(
                workspace.CurrentSolution, null, fixture.Options, cache, NullLogger.Instance, TestContext.CancellationToken);
            Assert.AreEqual(cachedCount, editedCount);
            editedDocuments.Should().Equal(uncachedDocuments);
        }

        [TestMethod]
        public void SelectionBatchUsesOneConfigurationSnapshotAndRevalidatesNextBatch()
        {
            using var fixture = new CacheFixture();
            var otherPath = Path.Combine(fixture.Options.WorkspaceFilePath, "other.cs");
            File.Copy(fixture.FilePath, otherPath);
            using (var writer = fixture.Create())
            {
                writer.RecordFormattedFiles(
                    fixture.ExpectedFiles().Concat(new[] { KeyValuePair.Create(otherPath, SourceText.From("class C { }", new UTF8Encoding(false))) }),
                    new[] { fixture.FilePath, otherPath });
            }

            using var cache = fixture.Create();
            IEnumerable<string> FilesWithConcurrentConfigEdit()
            {
                yield return fixture.FilePath;
                File.WriteAllText(Path.Combine(fixture.Options.WorkspaceFilePath, ".editorconfig"), "[*.cs]\nindent_size = 2");
                yield return otherPath;
            }

            // The second candidate uses the batch's configuration snapshot, not another scan.
            cache.GetUpToDateFiles(FilesWithConcurrentConfigEdit()).Should().BeEquivalentTo(new[] { fixture.FilePath, otherPath });
            Assert.IsEmpty(cache.GetUpToDateFiles(new[] { fixture.FilePath }));
            cache.RecordFormattedFiles(fixture.ExpectedFiles(), new[] { fixture.FilePath });
            using var reloadedCache = fixture.Create();
            Assert.IsFalse(IsUpToDate(reloadedCache, fixture.FilePath));
        }

        private static bool IsUpToDate(FormatCache cache, string filePath)
            => cache.GetUpToDateFiles(new[] { filePath }).Contains(filePath);

        private sealed class CacheFixture : IDisposable
        {
            public string RootPath { get; } = Directory.CreateTempSubdirectory("format-cache-tests-").FullName;
            public string FilePath { get; }
            public string CacheFilePath { get; }
            public FormatOptions Options { get; }

            public CacheFixture()
            {
                var workspacePath = Directory.CreateDirectory(Path.Combine(RootPath, "workspace")).FullName;
                Options = FormatOptions.Instance with
                {
                    WorkspaceFilePath = workspacePath,
                    WorkspaceType = WorkspaceType.Folder,
                    FixCategory = FixCategory.Whitespace,
                };
                FilePath = Path.Combine(workspacePath, "file.cs");
                File.WriteAllText(FilePath, "class C { }", new UTF8Encoding(false));
                var workspaceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workspacePath)));
                CacheFilePath = Path.Combine(Path.GetTempPath(), "dotnet-format", workspaceHash + ".json");
            }

            public FormatCache Create(FormatOptions? options = null)
            {
                var cache = FormatCache.Create(options ?? Options, NullLogger.Instance);
                Assert.IsNotNull(cache);
                return cache;
            }

            public KeyValuePair<string, SourceText>[] ExpectedFiles()
                => new[] { KeyValuePair.Create(FilePath, SourceText.From("class C { }", new UTF8Encoding(false))) };

            public void Save(FormatOptions? options = null)
            {
                using var cache = Create(options);
                cache.RecordFormattedFiles(ExpectedFiles(), new[] { FilePath });
            }

            public void Dispose()
            {
                if (Directory.Exists(Path.GetDirectoryName(CacheFilePath)))
                {
                    File.Delete(CacheFilePath);
                }

                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
