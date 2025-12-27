// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using FluentAssertions;
using FluentAssertions.Json;
using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenADependencyContextBuilder
    {
        /// <summary>
        /// Tests that DependencyContextBuilder generates DependencyContexts correctly.
        /// </summary>
        [Theory]
        [MemberData(nameof(ProjectData))]
        public void ItBuildsDependencyContextsFromProjectLockFiles(
            string mainProjectName,
            string mainProjectVersion,
            CompilationOptions compilationOptions,
            string baselineFileName,
            string runtime,
            ITaskItem[] assemblySatelliteAssemblies,
            ITaskItem[] referencePaths,
            ITaskItem[] referenceSatellitePaths,
            object[] resolvedNuGetFiles)
        {
            LockFile lockFile = TestLockFiles.GetLockFile(mainProjectName);
            LockFileLookup lockFileLookup = new(lockFile);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                "/usr/Path",
                mainProjectName,
                ".dll",
                mainProjectVersion,
                assemblySatelliteAssemblies ?? new ITaskItem[] { });

            IEnumerable<ReferenceInfo> directReferences =
                ReferenceInfo.CreateDirectReferenceInfos(
                    referencePaths ?? new ITaskItem[] { },
                    referenceSatellitePaths ?? new ITaskItem[] { },
                    lockFileLookup: lockFileLookup,
                    i => true,
                    true);

            ProjectContext projectContext = lockFile.CreateProjectContext(
                FrameworkConstants.CommonFrameworks.NetCoreApp10.GetShortFolderName(),
                runtime,
                Constants.DefaultPlatformLibrary,
                runtimeFrameworks: null,
                isSelfContained: !string.IsNullOrEmpty(runtime));

            if (resolvedNuGetFiles == null)
            {
                resolvedNuGetFiles = Array.Empty<ResolvedFile>();
            }

            DependencyContext dependencyContext = new DependencyContextBuilder(mainProject, includeRuntimeFileVersions: false, runtimeGraph: null, projectContext: projectContext, libraryLookup: lockFileLookup)
                .WithDirectReferences(directReferences)
                .WithCompilationOptions(compilationOptions)
                .WithResolvedNuGetFiles((ResolvedFile[])resolvedNuGetFiles)
                .Build();

            JObject result = Save(dependencyContext);
            JObject baseline = ReadJson($"{baselineFileName}.deps.json");

            try
            {
                baseline
                    .Should()
                    .BeEquivalentTo(result);
            }
            catch
            {
                // write the result file out on failure for easy comparison

                using (JsonTextWriter writer = new(File.CreateText($"result-{baselineFileName}.deps.json")))
                {
                    JsonSerializer serializer = new()
                    {
                        Formatting = Formatting.Indented
                    };
                    serializer.Serialize(writer, result);
                }

                throw;
            }
        }

        public static IEnumerable<object[]> ProjectData
        {
            get
            {
                ITaskItem[] dotnetNewSatelliteAssemblies = new ITaskItem[]
                {
                    new MockTaskItem(
                        @"de\dotnet.new.resources.dll",
                        new Dictionary<string, string>
                        {
                            { "Culture", "de" },
                            { "TargetPath", @"de\dotnet.new.resources.dll" },
                        }),
                    new MockTaskItem(
                        @"fr\dotnet.new.resources.dll",
                        new Dictionary<string, string>
                        {
                            { "Culture", "fr" },
                            { "TargetPath", @"fr\dotnet.new.resources.dll" },
                        }),
                };

                var resolvedNuGetFiles = new[]
                {
                    new ResolvedFile("Newtonsoft.Json.dll", "",
                        new PackageIdentity("Newtonsoft.Json", new NuGetVersion("9.0.1")),
                        AssetType.Runtime,
                        "lib/netstandard1.0/Newtonsoft.Json.dll"),

                    new ResolvedFile("System.Collections.NonGeneric.dll", "",
                        new PackageIdentity("System.Collections.NonGeneric", new NuGetVersion("4.0.1")),
                        AssetType.Runtime,
                        "lib/netstandard1.3/System.Collections.NonGeneric.dll"),

                    new ResolvedFile("System.Runtime.Serialization.Primitives.dll", "",
                        new PackageIdentity("System.Runtime.Serialization.Primitives", new NuGetVersion("4.1.1")),
                        AssetType.Runtime,
                        "lib/netstandard1.3/System.Runtime.Serialization.Primitives.dll")
                };

                return new[]
                {
                    new object[] { "dotnet.new", "1.0.0", null, "dotnet.new", null, null, null, null, null},
                    new object[] { "dotnet.new", "1.0.0", null, "dotnet.new.resources", null, dotnetNewSatelliteAssemblies, null, null, null },
                    new object[] { "simple.dependencies", "1.0.0", null, "simple.dependencies", null, null, null, null, resolvedNuGetFiles },
                };
            }
        }

        private static JObject ReadJson(string path)
        {
            using (JsonTextReader jsonReader = new(File.OpenText(path)))
            {
                return JObject.Load(jsonReader);
            }
        }

        private JObject Save(DependencyContext dependencyContext)
        {
            using (var memoryStream = new MemoryStream())
            {
                new DependencyContextWriter().Write(dependencyContext, memoryStream);
                using (var readStream = new MemoryStream(memoryStream.ToArray()))
                {
                    using (var textReader = new StreamReader(readStream))
                    {
                        using (var reader = new JsonTextReader(textReader))
                        {
                            return JObject.Load(reader);
                        }
                    }
                }
            }
        }

        [Fact]
        public void ItDoesntCreateReferenceAssembliesWhenNoCompilationOptions()
        {
            DependencyContext dependencyContext = BuildDependencyContextWithReferenceAssemblies(useCompilationOptions: false);

            dependencyContext.CompileLibraries.Should().BeEmpty();
            dependencyContext
                .RuntimeLibraries
                .Should()
                .NotContain(l => l.Type == "referenceassembly");
            dependencyContext
                .RuntimeLibraries
                .SelectMany(l => l.Dependencies)
                .Should()
                .BeEmpty();
        }

        [Fact]
        public void ItDoesntCreateKeepUnneededRuntimeReferences()
        {
            DependencyContext dependencyContext = BuildDependencyContextWithReferenceAssemblies(useCompilationOptions: false);

            dependencyContext.RuntimeLibraries.Count.Should().Be(1);
            dependencyContext.RuntimeLibraries[0].Name.Should().Be("simple.dependencies"); // This is the entrypoint
        }

        [Fact]
        public void ItHandlesReferenceAndPackageReferenceNameCollisions()
        {
            DependencyContext dependencyContext = BuildDependencyContextWithReferenceAssemblies(useCompilationOptions: true);

            dependencyContext.CompileLibraries.Should()
                .Contain(c => c.Name == "System.NotConflicting" && c.Type == "referenceassembly");

            // Note: System.Collections.NonGeneric is referenced in the lockfile, so DependencyContextBuilder
            // appends ".Reference" to make it unique
            dependencyContext.CompileLibraries.Should()
                .Contain(c => c.Name == "System.Collections.NonGeneric.Reference" && c.Type == "referenceassembly");
            dependencyContext.CompileLibraries.Should()
                .Contain(c => c.Name == "System.Collections.NonGeneric.Reference.Reference" && c.Type == "referenceassembly");
        }

        // If an assembly is in withResources, it has to be a key in dependencies, even with an empty list.
        private static DependencyContext BuildDependencyContextFromDependenciesWithResources(Dictionary<string, List<string>> dependencies, List<string> withResources, List<string> references, bool dllReference)
        {
            string mainProjectName = "simpleApp";
            LockFile lockFile = TestLockFiles.GetLockFile(mainProjectName);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                "/usr/Path",
                mainProjectName,
                ".dll",
                "1.0.0",
                []);
            string mainProjectDirectory = Path.GetDirectoryName(mainProject.ProjectPath);


            ITaskItem[] referencePaths = dllReference ? references.Select(reference =>
                new MockTaskItem($"/usr/Path/{reference}.dll", new Dictionary<string, string> {
                    { "CopyLocal", "false" },
                    { "FusionName", $"{reference}, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null" },
                    { "Version", "" },
                })).ToArray() : [];

            ProjectContext projectContext = lockFile.CreateProjectContext(
                FrameworkConstants.CommonFrameworks.Net10_0.GetShortFolderName(),
                runtime: null,
                platformLibraryName: Constants.DefaultPlatformLibrary,
                runtimeFrameworks: null,
                isSelfContained: false);

            if (!dllReference)
            {
                projectContext.LockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(string.Empty, references));
            }

            Dictionary<string, SingleProjectInfo> referenceProjectInfos = new();

            foreach (KeyValuePair<string, List<string>> kvp in dependencies)
            {
                projectContext.LockFileTarget.Libraries = projectContext.LockFileTarget.Libraries.Concat([
                    new LockFileTargetLibrary()
                    {
                        Name = kvp.Key,
                        Version = new NuGetVersion(4, 0, 0),
                        Type = withResources.Contains(kvp.Key) ? "project" : "unrealType",
                        Dependencies = kvp.Value.Select(n => new PackageDependency(n)).ToList()
                    }]).ToList();

                if (withResources.Contains(kvp.Key))
                {
                    var fullPath = Path.GetFullPath(Path.Combine(mainProjectDirectory, kvp.Key));
                    lockFile.Libraries = lockFile.Libraries.Concat([new LockFileLibrary()
                    {
                        Name = kvp.Key,
                        Version = new NuGetVersion(4, 0, 0),
                        Type = "project",
                        MSBuildProject = fullPath
                    }]).ToList();

                    referenceProjectInfos.Add(fullPath, SingleProjectInfo.Create(kvp.Key, kvp.Key, ".dll", "4.0.0",
                        [new MockTaskItem($"{kvp.Key}.resource", new Dictionary<string, string>() {
                            { "Culture", "en-us" },
                            { "TargetPath", $"{kvp.Key}.resource" }
                        })]));
                }
            }

            CompilationOptions compilationOptions = CreateCompilationOptions();

            return new DependencyContextBuilder(mainProject, includeRuntimeFileVersions: false, runtimeGraph: null, projectContext: projectContext, libraryLookup: new LockFileLookup(lockFile))
                .WithReferenceAssemblies(ReferenceInfo.CreateReferenceInfos(referencePaths))
                .WithCompilationOptions(compilationOptions)
                .WithReferenceProjectInfos(referenceProjectInfos)
                .Build();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DirectReferenceToPackageWithNoAssets(bool dllReference)
        {
            DependencyContext dependencyContext = BuildDependencyContextFromDependenciesWithResources([], [], ["System.A"], dllReference);
            Save(dependencyContext);
            dependencyContext.RuntimeLibraries.Count.Should().Be(1);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IndirectReferenceToPackageWithNoAssets(bool dllReference)
        {
            DependencyContext dependencyContext = BuildDependencyContextFromDependenciesWithResources(new Dictionary<string, List<string>>() {
                { "System.A", ["System.B"] }
            }, ["System.A"], ["System.A"], dllReference);
            Save(dependencyContext);
            dependencyContext.RuntimeLibraries.Count.Should().Be(2);
            dependencyContext.RuntimeLibraries.Should().Contain(x => x.Name.Equals("System.A"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PackageWithNoAssetsReferencesPackageWithNoAssets(bool dllReference)
        {
            DependencyContext dependencyContext = BuildDependencyContextFromDependenciesWithResources(new Dictionary<string, List<string>>() {
                { "System.A", ["System.B"] },
                { "System.B", [] }
            }, [], ["System.A"], dllReference);
            Save(dependencyContext);
            dependencyContext.RuntimeLibraries.Count.Should().Be(1);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PackageWithNoAssetsReferencesPackageWithAssets(bool dllReference)
        {
            DependencyContext dependencyContext = BuildDependencyContextFromDependenciesWithResources(new Dictionary<string, List<string>>() {
                { "System.A", ["System.B"] },
                { "System.B", [] }
            }, ["System.B"], ["System.A"], dllReference);
            Save(dependencyContext);
            dependencyContext.RuntimeLibraries.Count.Should().Be(3);
            dependencyContext.RuntimeLibraries.Should().Contain(x => x.Name.Equals("System.A"));
            dependencyContext.RuntimeLibraries.Should().Contain(x => x.Name.Equals("System.B"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PackageWithNoAssetsReferencesPackageReferencesByOtherPackage(bool dllReference)
        {
            DependencyContext dependencyContext = BuildDependencyContextFromDependenciesWithResources(new Dictionary<string, List<string>>()
            {
                { "System.A", ["System.B"] },
                { "System.B", [] },
            }, ["System.B"], ["System.A", "System.B"], dllReference);
            Save(dependencyContext);
            dependencyContext.RuntimeLibraries.Count.Should().Be(2);
            dependencyContext.RuntimeLibraries.Should().Contain(x => x.Name.Equals("System.B"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PackageWithNoAssetsReferencesPackageWithAssetsWithOtherReferencer(bool dllReference)
        {
            DependencyContext dependencyContext = BuildDependencyContextFromDependenciesWithResources(new Dictionary<string, List<string>>()
            {
                { "System.A", ["System.B"] },
                { "System.B", [] },
                { "System.C", ["System.B"] }
            }, ["System.B", "System.C"], ["System.A", "System.C"], dllReference);
            Save(dependencyContext);
            dependencyContext.RuntimeLibraries.Count.Should().Be(3);
            dependencyContext.RuntimeLibraries.Should().Contain(x => x.Name.Equals("System.C"));
            dependencyContext.RuntimeLibraries.Should().Contain(x => x.Name.Equals("System.B"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TwoPackagesWithNoAssetsReferencePackageWithAssets(bool dllReference)
        {
            DependencyContext dependencyContext = BuildDependencyContextFromDependenciesWithResources(new Dictionary<string, List<string>>()
            {
                { "System.A", ["System.B"] },
                { "System.C", ["System.B"] },
                { "System.B", [] }
            }, ["System.B"], ["System.A", "System.C"], dllReference);
            Save(dependencyContext);
            dependencyContext.RuntimeLibraries.Count.Should().Be(3);
            dependencyContext.RuntimeLibraries.Should().Contain(x => x.Name.Equals("System.B"));
            if (dependencyContext.RuntimeLibraries.Any(x => x.Name.Equals("System.A")))
            {
                dependencyContext.RuntimeLibraries.Should().NotContain(x => x.Name.Equals("System.C"));
            }
            else
            {
                dependencyContext.RuntimeLibraries.Should().Contain(x => x.Name.Equals("System.C"));
            }
        }

        private DependencyContext BuildDependencyContextWithReferenceAssemblies(bool useCompilationOptions)
        {
            string mainProjectName = "simple.dependencies";
            LockFile lockFile = TestLockFiles.GetLockFile(mainProjectName);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                "/usr/Path",
                mainProjectName,
                ".dll",
                "1.0.0",
                new ITaskItem[] { });

            ITaskItem[] referencePaths = new ITaskItem[]
            {
                new MockTaskItem(
                    "/usr/Path/System.NotConflicting.dll",
                    new Dictionary<string, string>
                    {
                        { "CopyLocal", "false" },
                        { "FusionName", "System.NotConflicting, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null" },
                        { "Version", "" },
                    }),
                new MockTaskItem(
                    "/usr/Path/System.Collections.NonGeneric.dll",
                    new Dictionary<string, string>
                    {
                        { "CopyLocal", "false" },
                        { "FusionName", "System.Collections.NonGeneric, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null" },
                        { "Version", "" },
                    }),
                new MockTaskItem(
                    "/usr/Path/System.Collections.NonGeneric.Reference.dll",
                    new Dictionary<string, string>
                    {
                        { "CopyLocal", "false" },
                        { "FusionName", "System.Collections.NonGeneric.Reference, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null" },
                        { "Version", "" },
                    }),
            };

            ProjectContext projectContext = lockFile.CreateProjectContext(
                FrameworkConstants.CommonFrameworks.NetCoreApp10.GetShortFolderName(),
                runtime: null,
                platformLibraryName: Constants.DefaultPlatformLibrary,
                runtimeFrameworks: null,
                isSelfContained: false);

            CompilationOptions compilationOptions =
                useCompilationOptions ? CreateCompilationOptions() :
                null;

            DependencyContext dependencyContext = new DependencyContextBuilder(mainProject, includeRuntimeFileVersions: false, runtimeGraph: null, projectContext: projectContext, libraryLookup: new LockFileLookup(lockFile))
                .WithReferenceAssemblies(ReferenceInfo.CreateReferenceInfos(referencePaths))
                .WithCompilationOptions(compilationOptions)
                .Build();

            // ensure the DependencyContext can be written out successfully - it has no duplicate dependency names
            Save(dependencyContext);

            return dependencyContext;
        }

        private static CompilationOptions CreateCompilationOptions()
        {
            return new CompilationOptions(
                    defines: new[] { "DEBUG", "TRACE" },
                    languageVersion: "6",
                    platform: "x64",
                    allowUnsafe: true,
                    warningsAsErrors: false,
                    optimize: null,
                    keyFile: "../keyfile.snk",
                    delaySign: null,
                    publicSign: null,
                    debugType: "portable",
                    emitEntryPoint: true,
                    generateXmlDocumentation: true);
        }

        [Fact]
        public void ItCanGenerateTheRuntimeFallbackGraph()
        {
            string mainProjectName = "simple.dependencies";
            LockFile lockFile = TestLockFiles.GetLockFile(mainProjectName);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                "/usr/Path",
                mainProjectName,
                ".dll",
                "1.0.0",
                new ITaskItem[] { });

            ProjectContext projectContext = lockFile.CreateProjectContext(
                FrameworkConstants.CommonFrameworks.NetCoreApp10.GetShortFolderName(),
                runtime: null,
                platformLibraryName: Constants.DefaultPlatformLibrary,
                runtimeFrameworks: null,
                isSelfContained: true);

            var runtimeGraph = new RuntimeGraph(
                new RuntimeDescription[]
                {
                    new RuntimeDescription("os-arch", new string [] { "os", "base" }),
                    new RuntimeDescription("new_os-arch", new string [] { "os-arch", "os", "base" }),
                    new RuntimeDescription("os-new_arch", new string [] { "os-arch", "os", "base" }),
                    new RuntimeDescription("new_os-new_arch", new string [] { "new_os-arch", "os-new_arch", "os-arch", "os", "base" }),
                    new RuntimeDescription("os-another_arch", new string [] { "os", "base" })
                });

            void CheckRuntimeFallbacks(string runtimeIdentifier, int fallbackCount)
            {
                projectContext.LockFileTarget.RuntimeIdentifier = runtimeIdentifier;
                var dependencyContextBuilder = new DependencyContextBuilder(mainProject, includeRuntimeFileVersions: false, runtimeGraph, projectContext, libraryLookup: new LockFileLookup(lockFile));
                var runtimeFallbacks = dependencyContextBuilder.Build().RuntimeGraph;

                runtimeFallbacks
                    .Count()
                    .Should()
                    .Be(fallbackCount);

                runtimeFallbacks
                    .Any(runtimeFallback => !runtimeFallback.Runtime.Equals(runtimeIdentifier) && !runtimeFallback.Fallbacks.Contains(runtimeIdentifier))
                    .Should()
                    .BeFalse();
            }

            CheckRuntimeFallbacks("os-arch", 4);
            CheckRuntimeFallbacks("new_os-arch", 2);
            CheckRuntimeFallbacks("os-new_arch", 2);
            CheckRuntimeFallbacks("new_os-new_arch", 1);
            CheckRuntimeFallbacks("unrelated_os-unknown_arch", 0);
        }

        [Fact]
        public void ItIncludesLocalPathForResolvedNuGetFiles()
        {
            string mainProjectName = "simple.dependencies";
            LockFile lockFile = TestLockFiles.GetLockFile(mainProjectName);
            LockFileLookup lockFileLookup = new(lockFile);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                "/usr/Path",
                mainProjectName,
                ".dll",
                "1.0.0",
                []);

            ProjectContext projectContext = lockFile.CreateProjectContext(
                FrameworkConstants.CommonFrameworks.NetCoreApp10.GetShortFolderName(),
                runtime: null,
                Constants.DefaultPlatformLibrary,
                runtimeFrameworks: null,
                isSelfContained: false);

            string packageName = "Newtonsoft.Json";
            string packageVersion = "9.0.1";

            // Runtime assemblies
            ResolvedFile runtime = new(
                "Newtonsoft.Json.dll",
                destinationSubDirectory: null,
                new PackageIdentity(packageName, new NuGetVersion(packageVersion)),
                AssetType.Runtime,
                $"lib/{ToolsetInfo.CurrentTargetFramework}/Newtonsoft.Json.dll");
            ResolvedFile runtimeWithCustomSubPath = new(
                "CustomSubPath.dll",
                "pkg/",
                new PackageIdentity(packageName, new NuGetVersion(packageVersion)),
                AssetType.Runtime,
                $"lib/{ToolsetInfo.CurrentTargetFramework}/CustomSubPath.dll");

            // Native libraries
            ResolvedFile native = new(
                "nativelib.dll",
                "runtimes/win-x64/native/",
                new PackageIdentity(packageName, new NuGetVersion(packageVersion)),
                AssetType.Native,
                "runtimes/win-x64/native/nativelib.dll");
            ResolvedFile nativeWithCustomSubPath = new(
                "nativecustomsubpath.dll",
                "pkg/runtimes/win-x64/native/",
                new PackageIdentity(packageName, new NuGetVersion(packageVersion)),
                AssetType.Native,
                "runtimes/win-x64/native/nativecustomsubpath.dll");

            // Resource assemblies
            MockTaskItem resourceTaskItem = new("de/Newtonsoft.Json.resources.dll",
                new Dictionary<string, string>
                {
                    [MetadataKeys.DestinationSubDirectory] = "de/",
                    [MetadataKeys.AssetType] = "resources",
                    [MetadataKeys.NuGetPackageId] = packageName,
                    [MetadataKeys.NuGetPackageVersion] = packageVersion,
                    [MetadataKeys.PathInPackage] = $"lib/{ToolsetInfo.CurrentTargetFramework}/de/Newtonsoft.Json.resources.dll",
                    [MetadataKeys.Culture] = "de",
                });
            MockTaskItem resourceWithCustomSubPathTaskItem = new("fr/Newtonsoft.Json.resources.dll",
                new Dictionary<string, string>
                {
                    [MetadataKeys.DestinationSubDirectory] = "pkg/fr/",
                    [MetadataKeys.AssetType] = "resources",
                    [MetadataKeys.NuGetPackageId] = packageName,
                    [MetadataKeys.NuGetPackageVersion] = packageVersion,
                    [MetadataKeys.PathInPackage] = $"lib/{ToolsetInfo.CurrentTargetFramework}/fr/Newtonsoft.Json.resources.dll",
                    [MetadataKeys.Culture] = "fr",
                });
            ResolvedFile resource = new(resourceTaskItem, false);
            ResolvedFile resourceWithCustomSubPath = new(resourceWithCustomSubPathTaskItem, false);

            DependencyContext dependencyContext = new DependencyContextBuilder(mainProject, includeRuntimeFileVersions: false, runtimeGraph: null, projectContext: projectContext, libraryLookup: lockFileLookup)
                .WithResolvedNuGetFiles([runtime, runtimeWithCustomSubPath, native, nativeWithCustomSubPath, resource, resourceWithCustomSubPath])
                .Build();

            var library = dependencyContext.RuntimeLibraries.FirstOrDefault(l => l.Name == "Newtonsoft.Json");
            library.Should().NotBeNull();

            // Runtime assembly
            library.RuntimeAssemblyGroups.Should().HaveCount(1);
            IReadOnlyList<RuntimeFile> runtimeFiles = library.RuntimeAssemblyGroups[0].RuntimeFiles;
            runtimeFiles.Should().HaveCount(2);
            runtimeFiles.Should().Contain(
                f => f.LocalPath == runtime.DestinationSubPath && f.Path == runtime.PathInPackage,
                $"runtime assemblies should have item with LocalPath={runtime.DestinationSubPath} and Path matching {runtime.PathInPackage}");
            runtimeFiles.Should().Contain(
                f => f.LocalPath == runtimeWithCustomSubPath.DestinationSubPath && f.Path == runtimeWithCustomSubPath.PathInPackage,
                $"runtime assemblies should have item with LocalPath={runtimeWithCustomSubPath.DestinationSubPath} and Path matching {runtimeWithCustomSubPath.PathInPackage}");

            // Native library
            library.NativeLibraryGroups.Should().HaveCount(1);
            IReadOnlyList<RuntimeFile> nativeFiles = library.NativeLibraryGroups[0].RuntimeFiles;
            nativeFiles.Should().HaveCount(2);
            nativeFiles.Should().Contain(
                f => f.LocalPath == native.DestinationSubPath && f.Path == native.PathInPackage,
                $"native libraries should have item with LocalPath={native.PathInPackage} and Path={native.DestinationSubPath}");
            nativeFiles.Should().Contain(
                f => f.LocalPath == nativeWithCustomSubPath.DestinationSubPath && f.Path == nativeWithCustomSubPath.PathInPackage,
                $"native libraries should have item with LocalPath={nativeWithCustomSubPath.PathInPackage} and Path={nativeWithCustomSubPath.DestinationSubPath}");

            // Resource assembly
            IReadOnlyList<ResourceAssembly> resourceAssemblies = library.ResourceAssemblies;
            resourceAssemblies.Should().HaveCount(2);
            resourceAssemblies.Should().Contain(
                f => f.LocalPath == resource.DestinationSubPath && f.Path == resource.PathInPackage,
                $"resource assemblies should have item with LocalPath={resource.PathInPackage} and Path={resource.DestinationSubPath}");
            resourceAssemblies.Should().Contain(
                f => f.LocalPath == resourceWithCustomSubPath.DestinationSubPath && f.Path == resourceWithCustomSubPath.PathInPackage,
                $"resource assemblies should have item with LocalPath={resourceWithCustomSubPath.PathInPackage} and Path={resourceWithCustomSubPath.DestinationSubPath}");
        }

        [Fact]
        public void ItIncludesLocalPathForReferences()
        {
            string mainProjectName = "simple.dependencies";
            LockFile lockFile = TestLockFiles.GetLockFile(mainProjectName);
            LockFileLookup lockFileLookup = new(lockFile);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                "/usr/Path",
                mainProjectName,
                ".dll",
                "1.0.0",
                []);

            ProjectContext projectContext = lockFile.CreateProjectContext(
                FrameworkConstants.CommonFrameworks.NetCoreApp10.GetShortFolderName(),
                runtime: null,
                Constants.DefaultPlatformLibrary,
                runtimeFrameworks: null,
                isSelfContained: false);

            MockTaskItem[] directReferenceTaskItems =
            [
                new MockTaskItem("DirectReference.dll", new Dictionary<string, string>
                {
                    [MetadataKeys.DestinationSubDirectory] = "direct-ref/",
                })
            ];
            IEnumerable<ReferenceInfo> directReferences = ReferenceInfo.CreateDirectReferenceInfos(
                directReferenceTaskItems,
                [],
                lockFileLookup: lockFileLookup,
                i => true,
                includeProjectsNotInAssetsFile: true);

            MockTaskItem[] dependencyReferenceTaskItems =
            [
                new MockTaskItem("DependencyReference.dll", new Dictionary<string, string>
                {
                    [MetadataKeys.DestinationSubDirectory] = "dependency-ref/",
                })
            ];
            IEnumerable<ReferenceInfo> dependencyReferences = ReferenceInfo.CreateDependencyReferenceInfos(
                dependencyReferenceTaskItems,
                [],
                i => true);

            DependencyContext dependencyContext = new DependencyContextBuilder(mainProject, includeRuntimeFileVersions: false, runtimeGraph: null, projectContext: projectContext, libraryLookup: lockFileLookup)
                .WithDirectReferences(directReferences)
                .WithDependencyReferences(dependencyReferences)
                .Build();

            ReferenceInfo[] expectedReferences = [.. directReferences, .. dependencyReferences];
            foreach (ReferenceInfo referenceInfo in expectedReferences)
            {
                var lib = dependencyContext.RuntimeLibraries.FirstOrDefault(l => l.Name == referenceInfo.Name);
                lib.Should().NotBeNull();
                lib.RuntimeAssemblyGroups.Should().HaveCount(1);
                lib.RuntimeAssemblyGroups[0].RuntimeFiles.Should().HaveCount(1);
                lib.RuntimeAssemblyGroups[0].RuntimeFiles.Should().Contain(
                    f => f.LocalPath == referenceInfo.DestinationSubPath && f.Path == referenceInfo.FileName,
                    $"runtime assemblies should have item with LocalPath={referenceInfo.DestinationSubPath} and Path matching {referenceInfo.FileName}");
            }
        }
    }
}
