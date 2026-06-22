// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection;
using Microsoft.Build.Framework;
using static Microsoft.NET.Build.Tasks.ResolvePackageAssets;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolvePackageAssetsTask
    {
        [Fact]
        public void ItHashesAllParameters()
        {
            IEnumerable<PropertyInfo> inputProperties;

            var task = InitializeTask(out inputProperties);

            byte[] oldHash;
            try
            {
                oldHash = task.HashSettings();
            }
            catch (ArgumentNullException)
            {
                Assert.Fail("HashSettings is likely not correctly handling null value of one or more optional task parameters");

                throw; // unreachable
            }

            foreach (var property in inputProperties)
            {
                switch (property.PropertyType)
                {
                    case var t when t == typeof(bool):
                        property.SetValue(task, true);
                        break;

                    case var t when t == typeof(string):
                        property.SetValue(task, property.Name);
                        break;

                    case var t when t == typeof(ITaskItem[]):
                        property.SetValue(task, new[] { new MockTaskItem() { ItemSpec = property.Name } });
                        break;

                    default:
                        Assert.Fail($"{property.Name} is not a bool or string or ITaskItem[]. Update the test code to handle that.");
                        throw null; // unreachable
                }

                byte[] newHash = task.HashSettings();
                newHash.Should().NotBeEquivalentTo(
                    oldHash,
                    because: $"{property.Name} should be included in hash.");

                oldHash = newHash;
            }
        }

        [Fact]
        public void ItDoesNotHashDesignTimeBuild()
        {
            var task = InitializeTask(out _);

            task.DesignTimeBuild = false;

            byte[] oldHash = task.HashSettings();

            task.DesignTimeBuild = true;

            byte[] newHash = task.HashSettings();

            newHash.Should().BeEquivalentTo(oldHash,
                because: $"{nameof(task.DesignTimeBuild)} should not be included in hash.");
        }

        [Fact]
        public void It_reads_analyzer_assets_from_lock_file_when_enabled()
        {
            ExecuteAnalyzerAssetsTest(
                restoreEnableAnalyzerAssets: true,
                includeAnalyzerAssetsGroup: true,
                assert: (analyzers, expectedAnalyzerPath) =>
                {
                    analyzers.Should().Equal(expectedAnalyzerPath);
                });
        }

        [Fact]
        public void It_does_not_fall_back_to_package_file_scanning_when_analyzer_assets_are_enabled()
        {
            ExecuteAnalyzerAssetsTest(
                restoreEnableAnalyzerAssets: true,
                includeAnalyzerAssetsGroup: false,
                assert: (analyzers, expectedAnalyzerPath) =>
                {
                    analyzers.Should().BeEmpty();
                });
        }

        [Fact]
        public void It_preserves_package_file_analyzer_scanning_when_analyzer_assets_are_disabled()
        {
            ExecuteAnalyzerAssetsTest(
                restoreEnableAnalyzerAssets: false,
                includeAnalyzerAssetsGroup: false,
                assert: (analyzers, expectedAnalyzerPath) =>
                {
                    analyzers.Should().Equal(expectedAnalyzerPath);
                });
        }

        [Fact]
        public void It_applies_language_selection_to_analyzer_assets_from_the_group()
        {
            // NuGet lists analyzers for every language in the group; the SDK selects the language-appropriate
            // ones, so a C# project must not pick up the VB analyzer.
            string[] selected = ResolveGroupAnalyzers(
                projectLanguage: "C#",
                compilerApiVersion: null,
                analyzerPaths: new[]
                {
                    "analyzers/dotnet/cs/CSharpAnalyzer.dll",
                    "analyzers/dotnet/vb/VisualBasicAnalyzer.dll",
                });

            selected.Should().Equal("analyzers/dotnet/cs/CSharpAnalyzer.dll");
        }

        [Fact]
        public void It_applies_compiler_version_selection_to_analyzer_assets_from_the_group()
        {
            // The group lists every compiler-version variant; the SDK picks the highest version that is still
            // applicable to the current compiler (roslyn3.9 -> roslyn3.8, never roslyn4.0).
            string[] selected = ResolveGroupAnalyzers(
                projectLanguage: "C#",
                compilerApiVersion: "roslyn3.9",
                analyzerPaths: new[]
                {
                    "analyzers/dotnet/roslyn3.8/cs/OldAnalyzer.dll",
                    "analyzers/dotnet/roslyn4.0/cs/NewAnalyzer.dll",
                });

            selected.Should().Equal("analyzers/dotnet/roslyn3.8/cs/OldAnalyzer.dll");
        }

        [Fact]
        public void It_ignores_analyzer_metadata_and_uses_legacy_scanning_when_feature_flag_is_disabled()
        {
            // An F# analyzer is excluded by the metadata-based selection for a C# project (its codeLanguage is
            // "fs"), but the legacy path-based scan includes it (it is not a VB analyzer). This asserts that the
            // metadata-based selection only applies when the feature flag is enabled.
            string[] expected = new[] { "analyzers/dotnet/fs/FSharpAnalyzer.dll" };

            string[] selectedWithFlag = ResolveGroupAnalyzers(
                projectLanguage: "C#",
                compilerApiVersion: null,
                analyzerPaths: expected,
                restoreEnableAnalyzerAssets: true);

            // Feature flag on: metadata-based selection excludes the F# analyzer for a C# project.
            selectedWithFlag.Should().BeEmpty();

            string[] selectedWithoutFlag = ResolveGroupAnalyzers(
                projectLanguage: "C#",
                compilerApiVersion: null,
                analyzerPaths: expected,
                restoreEnableAnalyzerAssets: false);

            // Feature flag off: legacy path scanning is used and the F# analyzer is included.
            selectedWithoutFlag.Should().Equal(expected);
        }

        [Fact]
        public void It_honors_the_restore_decision_from_the_assets_file_over_the_msbuild_property()
        {
            // The TFM gate (.NET 11+) lives in NuGet.targets, which is not imported during build, so the raw
            // MSBuild property can be true for a project that restore actually gated off. The SDK must honor the
            // value restore persisted into the assets file (here: disabled, no analyzer group) and fall back to
            // legacy package-file scanning instead of silently dropping all analyzers.
            ExecuteAnalyzerAssetsTest(
                restoreEnableAnalyzerAssets: false,         // assets file: restore gated the feature off
                includeAnalyzerAssetsGroup: false,
                restoreEnableAnalyzerAssetsTaskProperty: true, // raw MSBuild property (ungated) is true
                assert: (analyzers, expectedAnalyzerPath) =>
                {
                    analyzers.Should().Equal(expectedAnalyzerPath);
                });
        }

        [Fact]
        public void It_does_not_error_on_duplicate_package_names()
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            var assetsContent = @"{
  `version`: 3,
  `targets`: {
    `net5.0`: {
      `Humanizer.Core/2.8.25`: {
        `type`: `package`
      },
      `Humanizer.Core/2.8.26`: {
        `type`: `package`
      }
    }
  },
  `project`: {
    `version`: `1.0.0`,
    `frameworks`: {
      `net5.0`: {
        `targetAlias`: `net5.0`
      }
    },
    `restore`: {
      `frameworks`: {
        `net5.0`: {
          `targetAlias`: `net5.0`
        }
      }
    }
  }
}".Replace('`', '"');
            File.WriteAllText(projectAssetsJsonPath, assetsContent);

            var task = InitializeTask(out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.TargetFramework = "net5.0";
            new CacheWriter(task); // Should not error
        }

        [Fact]
        public void It_applies_language_agnostic_analyzer_assets_to_every_language()
        {
            // An analyzer with no language segment (codeLanguage "any") applies to projects of any language.
            string[] paths =
            {
                "analyzers/dotnet/NeutralAnalyzer.dll",
                "analyzers/dotnet/cs/CSharpAnalyzer.dll",
                "analyzers/dotnet/vb/VisualBasicAnalyzer.dll",
            };

            ResolveGroupAnalyzers("C#", compilerApiVersion: null, analyzerPaths: paths)
                .Should().Equal("analyzers/dotnet/NeutralAnalyzer.dll", "analyzers/dotnet/cs/CSharpAnalyzer.dll");

            ResolveGroupAnalyzers("VB", compilerApiVersion: null, analyzerPaths: paths)
                .Should().Equal("analyzers/dotnet/NeutralAnalyzer.dll", "analyzers/dotnet/vb/VisualBasicAnalyzer.dll");
        }

        [Fact]
        public void It_applies_language_selection_for_fsharp_projects()
        {
            // F# projects select fs and language-agnostic analyzers, but not cs/vb analyzers.
            string[] selected = ResolveGroupAnalyzers(
                projectLanguage: "F#",
                compilerApiVersion: null,
                analyzerPaths: new[]
                {
                    "analyzers/dotnet/NeutralAnalyzer.dll",
                    "analyzers/dotnet/cs/CSharpAnalyzer.dll",
                    "analyzers/dotnet/fs/FSharpAnalyzer.dll",
                });

            selected.Should().Equal(
                "analyzers/dotnet/NeutralAnalyzer.dll",
                "analyzers/dotnet/fs/FSharpAnalyzer.dll");
        }

        [Fact]
        public void It_applies_every_compiler_version_variant_when_the_project_compiler_version_is_unknown()
        {
            // F# projects (and others) have no resolved compiler API version. With an unknown project compiler
            // version, every compiler-version variant is treated as version-agnostic and applied.
            string[] selected = ResolveGroupAnalyzers(
                projectLanguage: "C#",
                compilerApiVersion: null,
                analyzerPaths: new[]
                {
                    "analyzers/dotnet/roslyn3.8/cs/OldAnalyzer.dll",
                    "analyzers/dotnet/roslyn4.0/cs/NewAnalyzer.dll",
                });

            selected.Should().Equal(
                "analyzers/dotnet/roslyn3.8/cs/OldAnalyzer.dll",
                "analyzers/dotnet/roslyn4.0/cs/NewAnalyzer.dll");
        }

        [Fact]
        public void It_applies_all_analyzers_sharing_the_highest_applicable_compiler_version()
        {
            // When several analyzers share the highest applicable compiler version, all of them are applied.
            string[] selected = ResolveGroupAnalyzers(
                projectLanguage: "C#",
                compilerApiVersion: "roslyn3.9",
                analyzerPaths: new[]
                {
                    "analyzers/dotnet/roslyn3.8/cs/FirstAnalyzer.dll",
                    "analyzers/dotnet/roslyn3.8/cs/SecondAnalyzer.dll",
                    "analyzers/dotnet/roslyn4.0/cs/TooNewAnalyzer.dll",
                });

            selected.Should().Equal(
                "analyzers/dotnet/roslyn3.8/cs/FirstAnalyzer.dll",
                "analyzers/dotnet/roslyn3.8/cs/SecondAnalyzer.dll");
        }

        [Fact]
        public void It_ignores_placeholder_analyzer_assets_in_the_group()
        {
            // A '_._' placeholder (an analyzer excluded by restore, e.g. via PrivateAssets) is ignored and never
            // surfaced as an analyzer.
            string[] selected = ResolveGroupAnalyzers(
                projectLanguage: "C#",
                compilerApiVersion: null,
                analyzerPaths: new[]
                {
                    "analyzers/dotnet/cs/CSharpAnalyzer.dll",
                    "analyzers/dotnet/cs/_._",
                });

            selected.Should().Equal("analyzers/dotnet/cs/CSharpAnalyzer.dll");
        }

        [Fact]
        public void It_reads_analyzer_assets_for_the_current_target_framework_in_a_multi_targeted_project()
        {
            // In a multi-targeted project the analyzers group is written per target framework. The task must read
            // the group for the target framework it is building (here net9.0), not another target's group.
            string testRoot = Path.Combine(Path.GetTempPath(), "rpa-analyzers-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);

            try
            {
                string objDir = Path.Combine(testRoot, "obj");
                string packagesDir = Path.Combine(testRoot, "packages");
                Directory.CreateDirectory(objDir);

                string packageDirectory = Path.Combine(packagesDir, AnalyzerPackageName.ToLowerInvariant(), AnalyzerPackageVersion);
                foreach (string relativePath in new[] { "analyzers/dotnet/cs/Net8Analyzer.dll", "analyzers/dotnet/cs/Net9Analyzer.dll" })
                {
                    string fullPath = Path.Combine(packageDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    File.WriteAllText(fullPath, string.Empty);
                }

                File.WriteAllText(
                    Path.Combine(packageDirectory, $"{AnalyzerPackageName.ToLowerInvariant()}.{AnalyzerPackageVersion}.nupkg.sha512"),
                    "abc123");
                File.WriteAllText(
                    Path.Combine(packageDirectory, $"{AnalyzerPackageName.ToLowerInvariant()}.nuspec"),
                    $"<package><metadata><id>{AnalyzerPackageName}</id><version>{AnalyzerPackageVersion}</version></metadata></package>");

                string projectPath = Path.Combine(testRoot, "test.csproj");
                string projectAssetsJsonPath = Path.Combine(objDir, "project.assets.json");
                File.WriteAllText(
                    projectAssetsJsonPath,
                    CreateMultiTargetedAnalyzerAssetsJson(projectPath, packagesDir, objDir));

                var task = new ResolvePackageAssets
                {
                    BuildEngine = new MockBuildEngine(),
                    ProjectAssetsCacheFile = Path.Combine(objDir, "project.assets.cache"),
                    ProjectAssetsFile = projectAssetsJsonPath,
                    ProjectPath = projectPath,
                    TargetFramework = "net9.0",
                    ProjectLanguage = "C#",
                    DotNetAppHostExecutableNameWithoutExtension = "apphost",
                    DefaultImplicitPackages = "Microsoft.NETCore.App",
                    DisablePackageAssetsCache = true,
                    RestoreEnableAnalyzerAssets = true,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(testRoot)
                };

                task.Execute().Should().BeTrue();

                string packagePrefix = packageDirectory + Path.DirectorySeparatorChar;
                task.Analyzers
                    .Select(a => a.ItemSpec.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase)
                        ? a.ItemSpec.Substring(packagePrefix.Length).Replace(Path.DirectorySeparatorChar, '/')
                        : a.ItemSpec)
                    .Should().Equal("analyzers/dotnet/cs/Net9Analyzer.dll");
            }
            finally
            {
                try { Directory.Delete(testRoot, true); } catch { }
            }
        }

        private static string CreateMultiTargetedAnalyzerAssetsJson(string projectPath, string packagesPath, string outputPath)
        {
            return $$"""
                {
                  "version": 5,
                  "targets": {
                    "net8.0": {
                      "{{AnalyzerPackageName}}/{{AnalyzerPackageVersion}}": {
                        "type": "package",
                        "analyzers": {
                          "analyzers/dotnet/cs/Net8Analyzer.dll": { "codeLanguage": "cs" }
                        }
                      }
                    },
                    "net9.0": {
                      "{{AnalyzerPackageName}}/{{AnalyzerPackageVersion}}": {
                        "type": "package",
                        "analyzers": {
                          "analyzers/dotnet/cs/Net9Analyzer.dll": { "codeLanguage": "cs" }
                        }
                      }
                    }
                  },
                  "libraries": {
                    "{{AnalyzerPackageName}}/{{AnalyzerPackageVersion}}": {
                      "sha512": "abc123",
                      "type": "package",
                      "path": "{{AnalyzerPackageName.ToLowerInvariant()}}/{{AnalyzerPackageVersion}}",
                      "files": [
                        "analyzers/dotnet/cs/Net8Analyzer.dll",
                        "analyzers/dotnet/cs/Net9Analyzer.dll",
                        "{{AnalyzerPackageName.ToLowerInvariant()}}.{{AnalyzerPackageVersion}}.nupkg.sha512",
                        "{{AnalyzerPackageName.ToLowerInvariant()}}.nuspec"
                      ]
                    }
                  },
                  "projectFileDependencyGroups": {
                    "net8.0": ["{{AnalyzerPackageName}} >= {{AnalyzerPackageVersion}}"],
                    "net9.0": ["{{AnalyzerPackageName}} >= {{AnalyzerPackageVersion}}"]
                  },
                  "packageFolders": { "{{JsonEscape(packagesPath)}}": {} },
                  "project": {
                    "version": "1.0.0",
                    "restore": {
                      "restoreEnableAnalyzerAssets": true,
                      "projectUniqueName": "test",
                      "projectName": "test",
                      "projectPath": "{{JsonEscape(projectPath)}}",
                      "packagesPath": "{{JsonEscape(packagesPath)}}",
                      "outputPath": "{{JsonEscape(outputPath)}}",
                      "projectStyle": "PackageReference",
                      "frameworks": {
                        "net8.0": { "targetAlias": "net8.0" },
                        "net9.0": { "targetAlias": "net9.0" }
                      }
                    },
                    "frameworks": {
                      "net8.0": { "targetAlias": "net8.0" },
                      "net9.0": { "targetAlias": "net9.0" }
                    }
                  }
                }
                """;
        }

        private const string AnalyzerPackageName = "Analyzer.Package";
        private const string AnalyzerPackageVersion = "1.0.0";
        private const string AnalyzerAssetPath = "analyzers/dotnet/cs/Analyzer.Package.dll";

        private static void ExecuteAnalyzerAssetsTest(
            bool restoreEnableAnalyzerAssets,
            bool includeAnalyzerAssetsGroup,
            Action<string[], string> assert,
            bool? restoreEnableAnalyzerAssetsTaskProperty = null)
        {
            string testRoot = Path.Combine(Path.GetTempPath(), "rpa-analyzers-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);

            try
            {
                string objDir = Path.Combine(testRoot, "obj");
                string packagesDir = Path.Combine(testRoot, "packages");
                Directory.CreateDirectory(objDir);
                CreateAnalyzerPackage(packagesDir);

                string projectPath = Path.Combine(testRoot, "test.csproj");
                string projectAssetsJsonPath = Path.Combine(objDir, "project.assets.json");
                File.WriteAllText(
                    projectAssetsJsonPath,
                    CreateAnalyzerAssetsJson(projectPath, packagesDir, objDir, includeAnalyzerAssetsGroup, restoreEnableAnalyzerAssets));

                var task = InitializeAnalyzerAssetsTask(
                    testRoot,
                    objDir,
                    projectAssetsJsonPath,
                    projectPath,
                    restoreEnableAnalyzerAssetsTaskProperty ?? restoreEnableAnalyzerAssets);

                task.Execute().Should().BeTrue();

                string expectedAnalyzerPath = Path.Combine(
                    packagesDir,
                    AnalyzerPackageName.ToLowerInvariant(),
                    AnalyzerPackageVersion,
                    AnalyzerAssetPath.Replace('/', Path.DirectorySeparatorChar));

                assert(task.Analyzers.Select(a => a.ItemSpec).ToArray(), expectedAnalyzerPath);
            }
            finally
            {
                try { Directory.Delete(testRoot, true); } catch { }
            }
        }

        private static ResolvePackageAssets InitializeAnalyzerAssetsTask(
            string testRoot,
            string objDir,
            string projectAssetsJsonPath,
            string projectPath,
            bool restoreEnableAnalyzerAssets)
        {
            return new ResolvePackageAssets
            {
                BuildEngine = new MockBuildEngine(),
                ProjectAssetsCacheFile = Path.Combine(objDir, "project.assets.cache"),
                ProjectAssetsFile = projectAssetsJsonPath,
                ProjectPath = projectPath,
                TargetFramework = "net8.0",
                ProjectLanguage = "C#",
                DotNetAppHostExecutableNameWithoutExtension = "apphost",
                DefaultImplicitPackages = "Microsoft.NETCore.App",
                DisablePackageAssetsCache = true,
                RestoreEnableAnalyzerAssets = restoreEnableAnalyzerAssets,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(testRoot)
            };
        }

        private static void CreateAnalyzerPackage(string packagesDir)
        {
            string packageDirectory = Path.Combine(
                packagesDir,
                AnalyzerPackageName.ToLowerInvariant(),
                AnalyzerPackageVersion);

            string analyzerPath = Path.Combine(
                packageDirectory,
                AnalyzerAssetPath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(analyzerPath));
            File.WriteAllText(analyzerPath, string.Empty);
            File.WriteAllText(
                Path.Combine(packageDirectory, $"{AnalyzerPackageName.ToLowerInvariant()}.{AnalyzerPackageVersion}.nupkg.sha512"),
                "abc123");
            File.WriteAllText(
                Path.Combine(packageDirectory, $"{AnalyzerPackageName.ToLowerInvariant()}.nuspec"),
                $"<package><metadata><id>{AnalyzerPackageName}</id><version>{AnalyzerPackageVersion}</version></metadata></package>");
        }

        private static string CreateAnalyzerAssetsJson(
            string projectPath,
            string packagesPath,
            string outputPath,
            bool includeAnalyzerAssetsGroup,
            bool restoreEnableAnalyzerAssets)
        {
            string analyzerAssetsGroup = includeAnalyzerAssetsGroup
                ? $@",
                    ""analyzers"": {{
                      ""{AnalyzerAssetPath}"": {{}}
                    }}"
                : "";

            string restoreEnableAnalyzerAssetsMetadata = restoreEnableAnalyzerAssets
                ? @"
                      ""restoreEnableAnalyzerAssets"": true,"
                : "";

            return $$"""
                {
                  "version": 5,
                  "targets": {
                    "net8.0": {
                      "{{AnalyzerPackageName}}/{{AnalyzerPackageVersion}}": {
                        "type": "package"{{analyzerAssetsGroup}}
                      }
                    }
                  },
                  "libraries": {
                    "{{AnalyzerPackageName}}/{{AnalyzerPackageVersion}}": {
                      "sha512": "abc123",
                      "type": "package",
                      "path": "{{AnalyzerPackageName.ToLowerInvariant()}}/{{AnalyzerPackageVersion}}",
                      "files": [
                        "{{AnalyzerAssetPath}}",
                        "{{AnalyzerPackageName.ToLowerInvariant()}}.{{AnalyzerPackageVersion}}.nupkg.sha512",
                        "{{AnalyzerPackageName.ToLowerInvariant()}}.nuspec"
                      ]
                    }
                  },
                  "projectFileDependencyGroups": { "net8.0": ["{{AnalyzerPackageName}} >= {{AnalyzerPackageVersion}}"] },
                  "packageFolders": { "{{JsonEscape(packagesPath)}}": {} },
                  "project": {
                    "version": "1.0.0",
                    "restore": {{{restoreEnableAnalyzerAssetsMetadata}}
                      "projectUniqueName": "test",
                      "projectName": "test",
                      "projectPath": "{{JsonEscape(projectPath)}}",
                      "packagesPath": "{{JsonEscape(packagesPath)}}",
                      "outputPath": "{{JsonEscape(outputPath)}}",
                      "projectStyle": "PackageReference",
                      "frameworks": {
                        "net8.0": { "targetAlias": "net8.0" }
                      }
                    },
                    "frameworks": { "net8.0": { "targetAlias": "net8.0" } }
                  }
                }
                """;
        }

        private static string JsonEscape(string value) => value.Replace(@"\", @"\\");

        // Derives the analyzer selection metadata from the asset path the same way NuGet restore does,
        // so the hand-written assets file matches what restore would produce.
        private static string AnalyzerMetadataJson(string path)
        {
            // '_._' placeholders are written without metadata, matching NuGet restore output.
            if (path.EndsWith("_._", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            string codeLanguage = "any";
            string compilerApiVersion = null;

            string[] segments = path.Split('/');
            for (int i = 0; i < segments.Length - 1; i++)
            {
                string segment = segments[i];
                if (segment is "cs" or "vb" or "fs")
                {
                    codeLanguage = segment;
                }
                else if (compilerApiVersion == null
                    && segment.StartsWith("roslyn", StringComparison.OrdinalIgnoreCase)
                    && segment.Length > "roslyn".Length
                    && char.IsDigit(segment["roslyn".Length]))
                {
                    compilerApiVersion = segment;
                }
            }

            string metadata = $@"""codeLanguage"": ""{codeLanguage}""";
            if (compilerApiVersion != null)
            {
                metadata += $@", ""compilerApiVersion"": ""{compilerApiVersion}""";
            }

            return metadata;
        }

        private static string[] ResolveGroupAnalyzers(
            string projectLanguage,
            string compilerApiVersion,
            string[] analyzerPaths,
            bool restoreEnableAnalyzerAssets = true)
        {
            string testRoot = Path.Combine(Path.GetTempPath(), "rpa-analyzers-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);

            try
            {
                string objDir = Path.Combine(testRoot, "obj");
                string packagesDir = Path.Combine(testRoot, "packages");
                Directory.CreateDirectory(objDir);

                string packageDirectory = Path.Combine(
                    packagesDir,
                    AnalyzerPackageName.ToLowerInvariant(),
                    AnalyzerPackageVersion);

                foreach (string relativePath in analyzerPaths)
                {
                    string fullPath = Path.Combine(packageDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    File.WriteAllText(fullPath, string.Empty);
                }

                File.WriteAllText(
                    Path.Combine(packageDirectory, $"{AnalyzerPackageName.ToLowerInvariant()}.{AnalyzerPackageVersion}.nupkg.sha512"),
                    "abc123");
                File.WriteAllText(
                    Path.Combine(packageDirectory, $"{AnalyzerPackageName.ToLowerInvariant()}.nuspec"),
                    $"<package><metadata><id>{AnalyzerPackageName}</id><version>{AnalyzerPackageVersion}</version></metadata></package>");

                string projectPath = Path.Combine(testRoot, "test.csproj");
                string projectAssetsJsonPath = Path.Combine(objDir, "project.assets.json");
                File.WriteAllText(
                    projectAssetsJsonPath,
                    CreateMultiAnalyzerAssetsJson(projectPath, packagesDir, objDir, analyzerPaths, restoreEnableAnalyzerAssets));

                var task = new ResolvePackageAssets
                {
                    BuildEngine = new MockBuildEngine(),
                    ProjectAssetsCacheFile = Path.Combine(objDir, "project.assets.cache"),
                    ProjectAssetsFile = projectAssetsJsonPath,
                    ProjectPath = projectPath,
                    TargetFramework = "net8.0",
                    ProjectLanguage = projectLanguage,
                    CompilerApiVersion = compilerApiVersion,
                    DotNetAppHostExecutableNameWithoutExtension = "apphost",
                    DefaultImplicitPackages = "Microsoft.NETCore.App",
                    DisablePackageAssetsCache = true,
                    RestoreEnableAnalyzerAssets = restoreEnableAnalyzerAssets,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(testRoot)
                };

                task.Execute().Should().BeTrue();

                string packagePrefix = packageDirectory + Path.DirectorySeparatorChar;
                return task.Analyzers
                    .Select(analyzer => analyzer.ItemSpec)
                    .Select(itemSpec => itemSpec.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase)
                        ? itemSpec.Substring(packagePrefix.Length).Replace(Path.DirectorySeparatorChar, '/')
                        : itemSpec)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
            }
            finally
            {
                try { Directory.Delete(testRoot, true); } catch { }
            }
        }

        private static string CreateMultiAnalyzerAssetsJson(
            string projectPath,
            string packagesPath,
            string outputPath,
            string[] analyzerPaths,
            bool restoreEnableAnalyzerAssets)
        {
            string groupEntries = string.Join(
                ",\r\n                      ",
                analyzerPaths.Select(path => $@"""{path}"": {{ {AnalyzerMetadataJson(path)} }}"));

            string restoreEnableAnalyzerAssetsMetadata = restoreEnableAnalyzerAssets
                ? @"
                      ""restoreEnableAnalyzerAssets"": true,"
                : "";

            string filesArray = string.Join(
                ",\r\n                        ",
                analyzerPaths
                    .Concat(new[]
                    {
                        $"{AnalyzerPackageName.ToLowerInvariant()}.{AnalyzerPackageVersion}.nupkg.sha512",
                        $"{AnalyzerPackageName.ToLowerInvariant()}.nuspec"
                    })
                    .Select(file => $@"""{file}"""));

            return $$"""
                {
                  "version": 5,
                  "targets": {
                    "net8.0": {
                      "{{AnalyzerPackageName}}/{{AnalyzerPackageVersion}}": {
                        "type": "package",
                        "analyzers": {
                          {{groupEntries}}
                        }
                      }
                    }
                  },
                  "libraries": {
                    "{{AnalyzerPackageName}}/{{AnalyzerPackageVersion}}": {
                      "sha512": "abc123",
                      "type": "package",
                      "path": "{{AnalyzerPackageName.ToLowerInvariant()}}/{{AnalyzerPackageVersion}}",
                      "files": [
                        {{filesArray}}
                      ]
                    }
                  },
                  "projectFileDependencyGroups": { "net8.0": ["{{AnalyzerPackageName}} >= {{AnalyzerPackageVersion}}"] },
                  "packageFolders": { "{{JsonEscape(packagesPath)}}": {} },
                  "project": {
                    "version": "1.0.0",
                    "restore": {{{restoreEnableAnalyzerAssetsMetadata}}
                      "projectUniqueName": "test",
                      "projectName": "test",
                      "projectPath": "{{JsonEscape(projectPath)}}",
                      "packagesPath": "{{JsonEscape(packagesPath)}}",
                      "outputPath": "{{JsonEscape(outputPath)}}",
                      "projectStyle": "PackageReference",
                      "frameworks": {
                        "net8.0": { "targetAlias": "net8.0" }
                      }
                    },
                    "frameworks": { "net8.0": { "targetAlias": "net8.0" } }
                  }
                }
                """;
        }

        private static string AssetsFileWithInvalidLocale(string tfm, string locale) => @"
{
  `version`: 3,
  `targets`: {
    `{tfm}`: {
      `JavaScriptEngineSwitcher.Core/3.3.0`: {
        `type`: `package`,
        `compile`: {
          `lib/netstandard2.0/JavaScriptEngineSwitcher.Core.dll`: {}
        },
        `runtime`: {
          `lib/netstandard2.0/JavaScriptEngineSwitcher.Core.dll`: {}
        },
        `resource`: {
          `lib/netstandard2.0/ru-ru/JavaScriptEngineSwitcher.Core.resources.dll`: {
            `locale`: `{locale}`
          }
        }
      }
    }
  },
  `project`: {
    `version`: `1.0.0`,
    `frameworks`: {
      `{tfm}`: {
        `targetAlias`: `{tfm}`
      }
    },
    `restore`: {
        `frameworks`: {
          `{tfm}`: {
            `targetAlias`: `{tfm}`
          }
        }
    }
  }
}".Replace("`", "\"").Replace("{tfm}", tfm).Replace("{locale}", locale);

        [InlineData("net7.0", true)]
        [InlineData("net6.0", false)]
        [Theory]
        public void It_warns_on_invalid_culture_codes_of_resources(string tfm, bool shouldHaveWarnings)
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            var assetsContent = AssetsFileWithInvalidLocale(tfm, "what is this even");
            File.WriteAllText(projectAssetsJsonPath, assetsContent);
            var task = InitializeTask(out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.TargetFramework = tfm;
            var writer = new CacheWriter(task, new MockPackageResolver());
            writer.WriteToMemoryStream();
            var engine = task.BuildEngine as MockBuildEngine;

            var invalidContextWarnings = engine.Warnings.Where(msg => msg.Code == "NETSDK1188");
            invalidContextWarnings.Should().HaveCount(shouldHaveWarnings ? 1 : 0);

            var invalidContextMessages = engine.Messages.Where(msg => msg.Code == "NETSDK1188" && msg.Importance == MessageImportance.Low);
            invalidContextMessages.Should().HaveCount(shouldHaveWarnings ? 0 : 1);

        }

        [InlineData("net7.0", true)]
        [InlineData("net6.0", false)]
        [Theory]
        public void It_warns_on_incorrectly_cased_culture_codes_of_resources(string tfm, bool shouldHaveWarnings)
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            var assetsContent = AssetsFileWithInvalidLocale(tfm, "ru-ru");
            File.WriteAllText(projectAssetsJsonPath, assetsContent);
            var task = InitializeTask(out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.TargetFramework = tfm;
            var writer = new CacheWriter(task, new MockPackageResolver());
            writer.WriteToMemoryStream();
            var engine = task.BuildEngine as MockBuildEngine;

            var invalidContextWarnings = engine.Warnings.Where(msg => msg.Code == "NETSDK1187");
            invalidContextWarnings.Should().HaveCount(shouldHaveWarnings ? 1 : 0);

            var invalidContextMessages = engine.Messages.Where(msg => msg.Code == "NETSDK1187" && msg.Importance == MessageImportance.Low);
            invalidContextMessages.Should().HaveCount(shouldHaveWarnings ? 0 : 1);
        }

        private ResolvePackageAssets InitializeTask(out IEnumerable<PropertyInfo> inputProperties)
        {
            inputProperties = typeof(ResolvePackageAssets)
                .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(p => !p.IsDefined(typeof(OutputAttribute)) &&
                            p.Name != nameof(ResolvePackageAssets.DesignTimeBuild) &&
                            p.Name != nameof(ResolvePackageAssets.TaskEnvironment))
                .OrderBy(p => p.Name, StringComparer.Ordinal);

            var requiredProperties = inputProperties
                .Where(p => p.IsDefined(typeof(RequiredAttribute)));

            var task = new ResolvePackageAssets();
            // Initialize all required properties as a genuine task invocation would. We do this
            // because HashSettings need not defend against required parameters being null.
            foreach (var property in requiredProperties)
            {
                property.PropertyType.Should().Be(
                    typeof(string),
                    because: $"this test hasn't been updated to handle non-string required task parameters like {property.Name}");

                property.SetValue(task, "_");
            }

            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(Directory.GetCurrentDirectory());

            return task;
        }
    }
}

