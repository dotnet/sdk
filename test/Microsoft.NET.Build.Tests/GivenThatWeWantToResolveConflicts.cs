// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToResolveConflicts : SdkTest
    {
        public GivenThatWeWantToResolveConflicts(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData("netstandard2.0")]
        public void The_same_references_are_used_with_or_without_DisableDefaultPackageConflictOverrides(string targetFramework)
        {
            var defaultProject = new TestProject()
            {
                Name = "DefaultProject",
                TargetFrameworks = targetFramework,
            };
            AddConflictReferences(defaultProject);
            GetReferences(
                defaultProject,
                expectConflicts: false,
                references: out List<string> defaultReferences,
                referenceCopyLocalPaths: out List<string> defaultReferenceCopyLocalPaths,
                targetFramework);

            var disableProject = new TestProject()
            {
                Name = "DisableProject",
                TargetFrameworks = targetFramework,
            };
            disableProject.AdditionalProperties.Add("DisableDefaultPackageConflictOverrides", "true");
            AddConflictReferences(disableProject);
            GetReferences(
                disableProject,
                expectConflicts: true,
                references: out List<string> disableReferences,
                referenceCopyLocalPaths: out List<string> disableReferenceCopyLocalPaths,
                targetFramework);

            Assert.Equal(defaultReferences, disableReferences);
            Assert.Equal(defaultReferenceCopyLocalPaths, disableReferenceCopyLocalPaths);
        }

        private void AddConflictReferences(TestProject testProject)
        {
            foreach (var dependency in ConflictResolutionAssets.ConflictResolutionDependencies)
            {
                testProject.PackageReferences.Add(new TestPackageReference(dependency.Item1, dependency.Item2));
            }
        }

        private void GetReferences(TestProject testProject, bool expectConflicts, out List<string> references, out List<string> referenceCopyLocalPaths, string identifier)
        {
            string targetFramework = testProject.TargetFrameworks;
            TestAsset tempTestAsset = _testAssetsManager.CreateTestProject(testProject, identifier: identifier);

            string projectFolder = Path.Combine(tempTestAsset.TestRoot, testProject.Name);

            var getReferenceCommand = new GetValuesCommand(
                Log,
                projectFolder,
                targetFramework,
                "Reference",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "Build"
            };
            var result = getReferenceCommand.Execute("/v:detailed").Should().Pass();
            if (expectConflicts)
            {
                result.And.HaveStdOutMatching("Encountered conflict", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            }
            else
            {
                result.And.NotHaveStdOutMatching("Encountered conflict", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            }

            references = getReferenceCommand.GetValues();

            var getReferenceCopyLocalPathsCommand = new GetValuesCommand(
                Log,
                projectFolder,
                targetFramework,
                "ReferenceCopyLocalPaths",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "Build"
            };
            getReferenceCopyLocalPathsCommand.Execute().Should().Pass();

            referenceCopyLocalPaths = getReferenceCopyLocalPathsCommand.GetValues();
        }

        [Fact]
        public void CompileConflictsAreNotRemovedFromRuntimeDepsAssets()
        {
            TestProject testProject = new()
            {
                Name = "NetStandard2Library",
                TargetFrameworks = "netstandard2.0",
                //  In deps file, assets are under the ".NETStandard,Version=v2.0/" target (ie with empty RID) for some reason
                RuntimeIdentifier = string.Empty
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Mvc.Razor", "2.1.0"));

            //  This test relies on a package that would be pruned.  This doesn't seem to be a customer scenario, it looks like it was
            //  an easier way to test that files that were removed 
            testProject.AdditionalProperties["RestoreEnablePackagePruning"] = "false";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string outputFolder = buildCommand.GetOutputDirectory(testProject.TargetFrameworks,
                runtimeIdentifier: testProject.RuntimeIdentifier).FullName;

            string depsJsonPath = Path.Combine(outputFolder, $"{testProject.Name}.deps.json");

            var assets = DepsFileSkipTests.GetDepsJsonAssets(depsJsonPath, testProject, "runtime")
                .Select(DepsFileSkipTests.GetDepsJsonFilename)
                .ToList();

            assets.Should().Contain("System.ValueTuple.dll");

        }

        [Fact]
        public void AProjectCanReferenceADllInAPackageDirectly()
        {
            TestProject testProject = new()
            {
                Name = "ReferencePackageDllDirectly",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.VisualStudio.Composition", "15.8.112"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = p.Root.Element(ns + "ItemGroup");
                    itemGroup.Add(new XElement(ns + "Reference",
                        new XAttribute("Include", @"$(NuGetPackageRoot)/microsoft.visualstudio.composition/15.8.112/lib/net45/Microsoft.VisualStudio.Composition.dll"),
                        new XAttribute("Private", "true")));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void DuplicateFrameworkAssembly()
        {
            TestProject testProject = new()
            {
                Name = "DuplicateFrameworkAssembly",
                TargetFrameworks = "net472",
                IsExe = true
            };
            testProject.References.Add("System.Runtime");
            testProject.References.Add("System.Runtime");
            testProject.PackageReferences.Add(new TestPackageReference("System.Runtime", "4.3.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void FilesFromAspNetCoreSharedFrameworkAreNotIncluded()
        {
            var testProject = new TestProject()
            {
                Name = "AspNetCoreProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.Extensions.DependencyInjection.Abstractions", "2.2.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);
                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                    new XAttribute("Include", "Microsoft.AspNetCore.App")));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().NotHaveFile("Microsoft.Extensions.DependencyInjection.Abstractions.dll");
        }

        [CoreMSBuildOnlyFact]
        public void AnalyzersAreConflictResolved()
        {
            var testProject = new TestProject()
            {
                Name = nameof(AnalyzersAreConflictResolved),
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            // add the package referenced analyzers
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.CodeAnalysis.NetAnalyzers", "5.0.3"));

            // enable inbox analyzers too
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    project.Root.Add(itemGroup);
                    itemGroup.Add(new XElement(ns + "EnableNETAnalyzers", "true"));
                    itemGroup.Add(new XElement(ns + "TreatWarningsAsErrors", "true"));

                    // Don't error when generators/analyzers can't be loaded.
                    // This can occur when running tests against FullFramework MSBuild
                    // if the build machine has an MSBuild install with an older version of Roslyn
                    // than the generators in the SDK reference. We aren't testing the generators here
                    // and this failure will occur more clearly in other places when it's
                    // actually an important failure, so don't error out here.
                    itemGroup.Add(new XElement(ns + "WarningsNotAsErrors", "CS9057"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        //  Should also run on full framework, but needs the right version of NuGet, which isn't on CI yet
        [CoreMSBuildOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void PlatformPackagesCanBePruned(bool prunePackages)
        {
            var referencedProject = new TestProject("ReferencedProject")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = false
            };
            referencedProject.PackageReferences.Add(new TestPackageReference("System.Text.Json", "8.0.0"));
            referencedProject.AdditionalProperties["RestoreEnablePackagePruning"] = prunePackages.ToString();

            var testProject = new TestProject()
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            testProject.AdditionalProperties["RestoreEnablePackagePruning"] = prunePackages.ToString();
            testProject.ReferencedProjects.Add(referencedProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: prunePackages.ToString());

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute().Should().Pass();

            var assetsFilePath = Path.Combine(buildCommand.GetBaseIntermediateDirectory().FullName, "project.assets.json");
            var lockFile = LockFileUtilities.GetLockFile(assetsFilePath, new NullLogger());
            var lockFileTarget = lockFile.GetTarget(NuGetFramework.Parse(ToolsetInfo.CurrentTargetFramework), runtimeIdentifier: null);
            
            if (prunePackages)
            {
                lockFileTarget.Libraries.Should().NotContain(library => library.Name.Equals("System.Text.Json", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                lockFileTarget.Libraries.Should().Contain(library => library.Name.Equals("System.Text.Json", StringComparison.OrdinalIgnoreCase));
            }
        }

        [CoreMSBuildOnlyTheory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        [InlineData("net9.0")]
        [InlineData("net8.0")]
        [InlineData("net7.0")]
        [InlineData("net6.0")]
        [InlineData("netcoreapp3.1")]
        [InlineData("netcoreapp3.0")]
        [InlineData("netcoreapp2.1")]
        [InlineData("netcoreapp2.0")]
        [InlineData("netcoreapp1.1", false)]
        [InlineData("netcoreapp1.0", false)]
        [InlineData("netstandard2.1")]
        [InlineData("netstandard2.0")]
        [InlineData("netstandard1.1", false)]
        [InlineData("netstandard1.0", false)]
        [InlineData("net451", false)]
        [InlineData("net462")]
        [InlineData("net481")]
        //  These target frameworks shouldn't prune packages unless explicitly enabled
        [InlineData("net9.0", false, "")]
        [InlineData("netstandard2.1", false, "")]
        //  .NET 10 and up should prune packages by default
        [InlineData("net10.0", true, "")]
        public void PrunePackageDataSucceeds(string targetFramework, bool shouldPrune = true, string enablePackagePruning = "True")
        {
            var nugetFramework = NuGetFramework.Parse(targetFramework);

            List<KeyValuePair<string,string>> GetPrunedPackages(string frameworkReference)
            {
                var testProject = new TestProject()
                {
                    TargetFrameworks = targetFramework
                };

                testProject.AdditionalProperties["RestoreEnablePackagePruning"] = enablePackagePruning;

                if (!string.IsNullOrEmpty(frameworkReference))
                {
                    testProject.FrameworkReferences.Add(frameworkReference);
                }

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && frameworkReference != null && frameworkReference.StartsWith("Microsoft.WindowsDesktop", StringComparison.OrdinalIgnoreCase))
                {
                    testProject.AdditionalProperties["EnableWindowsTargeting"] = "True";
                }

                var testAsset = _testAssetsManager.CreateTestProject(testProject, callingMethod: nameof(PrunePackageDataSucceeds), identifier: targetFramework + frameworkReference);

                var buildCommand = new BuildCommand(testAsset);

                var prunePackageItemFile = Path.Combine(testAsset.TestRoot, "prunePackageItems.txt");

                buildCommand.Execute("/t:CollectPrunePackageReferences", "-getItem:PrunePackageReference", $"-getResultOutputFile:{prunePackageItemFile}").Should().Pass();

                var prunedPackages = ParsePrunePackageReferenceJson(File.ReadAllText(prunePackageItemFile));

                foreach (var kvp in prunedPackages)
                {
                    var prunedPackageVersion = NuGetVersion.Parse(kvp.Value);
                    if (nugetFramework.Framework.Equals(".NETCoreApp", StringComparison.OrdinalIgnoreCase) && !prunedPackageVersion.IsPrerelease)
                    {
                        prunedPackageVersion.Patch.Should().BeGreaterThan(99, $"Patch for {kvp.Key} should be at least 100");
                    }
                    else
                    {
                        prunedPackageVersion.Patch.Should().BeLessThan(1000, $"Patch for {kvp.Key} should be less than 1000");
                    }
                }


                return prunedPackages;
            }

            var prunedPackages = GetPrunedPackages("");
            if (shouldPrune)
            {
                prunedPackages.Should().NotBeEmpty();
            }
            else
            {
                prunedPackages.Should().BeEmpty();
            }

            if (shouldPrune && nugetFramework.Framework.Equals(".NETCoreApp", StringComparison.OrdinalIgnoreCase) && nugetFramework.Version.Major >= 3)
            {
                foreach(var frameworkReference in new [] {
                        "Microsoft.AspNetCore.App",
                        "Microsoft.WindowsDesktop.App",
                        "Microsoft.WindowsDesktop.App.WindowsForms",
                    })
                {
                    var frameworkPrunedPackages = GetPrunedPackages(frameworkReference);
                    frameworkPrunedPackages.Count.Should().BeGreaterThan(prunedPackages.Count, frameworkReference + " should have more pruned packages than base framework");
                }
            }
        }

        [Fact]
        public void TransitiveFrameworkReferencesDoNotAffectPruning()
        {
            var referencedProject = new TestProject("ReferencedProject")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = false
            };
            referencedProject.PackageReferences.Add(new TestPackageReference("System.Text.Json", "8.0.0"));
            referencedProject.FrameworkReferences.Add("Microsoft.AspNetCore.App");

            var testProject = new TestProject()
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            testProject.AdditionalProperties["RestoreEnablePackagePruning"] = "True";
            testProject.ReferencedProjects.Add(referencedProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            new BuildCommand(testAsset).Execute().Should().Pass();

            var getItemsCommand1 = new MSBuildCommand(testAsset, "AddPrunePackageReferences");
            var itemsResult1 = getItemsCommand1.Execute("-getItem:PrunePackageReference");
            itemsResult1.Should().Pass();

            var items1 = ParsePrunePackageReferenceJson(itemsResult1.StdOut);

            var getItemsCommand2 = new MSBuildCommand(testAsset, "ResolvePackageAssets;AddTransitiveFrameworkReferences;AddPrunePackageReferences");
            var itemsResult2 = getItemsCommand2.Execute("-getItem:PrunePackageReference");
            itemsResult2.Should().Pass();

            var items2 = ParsePrunePackageReferenceJson(itemsResult2.StdOut);

            items2.Should().BeEquivalentTo(items1);

        }

        [CoreMSBuildOnlyTheory]
        [InlineData("net10.0;net9.0", true)]
        [InlineData("net10.0;net8.0", true)]
        [InlineData("net6.0;net7.0", false)]
        public void WithMultitargetedProjects_PruningsDefaultsAreApplies(string frameworks, bool prunePackages)
        {
            var referencedProject = new TestProject("ReferencedProject")
            {
                TargetFrameworks = frameworks,
                IsExe = false
            };
            referencedProject.PackageReferences.Add(new TestPackageReference("System.Text.Json", "6.0.0"));
            referencedProject.AdditionalProperties["RestoreEnablePackagePruning"] = "false";

            var testProject = new TestProject()
            {
                TargetFrameworks = frameworks,
            };

            testProject.ReferencedProjects.Add(referencedProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: prunePackages.ToString());

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute().Should().Pass();

            var assetsFilePath = Path.Combine(buildCommand.GetBaseIntermediateDirectory().FullName, "project.assets.json");
            var lockFile = LockFileUtilities.GetLockFile(assetsFilePath, new NullLogger());

            foreach(var lockFileTarget in lockFile.Targets)
            {
                if (prunePackages)
                {
                    lockFileTarget.Libraries.Should().NotContain(library => library.Name.Equals("System.Text.Json", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    lockFileTarget.Libraries.Should().Contain(library => library.Name.Equals("System.Text.Json", StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        static List<KeyValuePair<string, string>> ParsePrunePackageReferenceJson(string json)
        {
            List<KeyValuePair<string, string>> ret = new();
            var root = JsonNode.Parse(json);
            var items = (JsonArray)root["Items"]["PrunePackageReference"];
            foreach (var item in items)
            {
                ret.Add(new KeyValuePair<string, string>((string)item["Identity"], (string)item["Version"]));
            }
            return ret;
        }
    }
}
