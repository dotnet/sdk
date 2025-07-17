// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.PackageInstall.Tests
{
    [CollectionDefinition(nameof(TestToolBuilderCollection))]
    public class TestToolBuilderCollection : ICollectionFixture<TestToolBuilder>
    {
        // This class is intentionally left empty.
    }


    //  This class is responsible for creating test dotnet tool packages.  We don't want every test to have to create it's own tool package, as that could slow things down quite a bit.
    //  So this class handles creating each tool package once.  To use it, add it as a constructor parameter to your test class.  You will also need to add [Collection(nameof(TestToolBuilderCollection))]
    //  to your test class to ensure that only one test at a time uses this class.  xUnit will give you an error if you forget to add the collection attribute but do add the constructor parameter.
    //
    //  The TestToolBuilder class uses a common folder to store all the tool package projects and their built nupkgs.  When CreateTestTool is called, it will compare the contents of the project
    //  in the common folder to the contents of the project that is being requested.  If there are any differences, it will re-create the project and build it again.  It will also delete the package from the
    //  global packages folder to ensure that the newly built package is used the next time a test tries to install it.
    //
    //  The main thing this class can't handle is if the way the .NET SDK builds packages changes.  In CI runs, we should use a clean test execution folder each time (I think!), so this shouldn't be an issue.
    //  For local testing, you may need to delete the artifacts\tmp\Debug\TestTools folder if the SDK changes in a way that affects the built package.
    public class TestToolBuilder
    {
        public class TestToolSettings
        {
            public string ToolPackageId { get; set; } = "TestTool";
            public string ToolPackageVersion { get; set; } = "1.0.0";
            public string ToolCommandName { get; set; } = "TestTool";
            public string[]? AdditionalPackageTypes { get; set; } = null;

            public bool NativeAOT { get; set { field = value; this.RidSpecific = value; } } = false;
            public bool SelfContained { get; set { field = value; this.RidSpecific = value; } } = false;
            public bool Trimmed { get; set { field = value; this.RidSpecific = value; } } = false;
            public bool IncludeAnyRid { get; set { field = value; } } = false;
            public bool RidSpecific { get; set; } = false;
            public bool IncludeCurrentRid { get; set; } = true;

            public string GetIdentifier() {
                var builder = new StringBuilder();
                builder.Append(ToolPackageId.ToLowerInvariant());
                builder.Append('-');
                builder.Append(ToolPackageVersion.ToLowerInvariant());
                builder.Append('-');
                builder.Append(ToolCommandName.ToLowerInvariant());
                if (NativeAOT)
                {
                    builder.Append("-nativeaot");
                }
                else if (SelfContained)
                {
                    builder.Append("-selfcontained");
                }
                else if (Trimmed)
                {
                    builder.Append("-trimmed");
                }
                else
                {
                    builder.Append("-managed");
                }
                if (RidSpecific)
                {
                    builder.Append("-specific");
                }
                if (IncludeAnyRid)
                {
                    builder.Append("-anyrid");
                }
                if (!IncludeCurrentRid)
                {
                    builder.Append("-no-current-rid");
                }
                if (AdditionalPackageTypes is not null && AdditionalPackageTypes.Length > 0)
                {
                    builder.Append('-');
                    builder.Append(string.Join("-", AdditionalPackageTypes.Select(p => p.ToLowerInvariant())));
                }

                return builder.ToString();
            }
        }


        public string CreateTestTool(ITestOutputHelper log, TestToolSettings toolSettings, bool collectBinlogs = false)
        {
            var targetDirectory = Path.Combine(TestContext.Current.TestExecutionDirectory, "TestTools", toolSettings.GetIdentifier());

            var testProject = new TestProject(toolSettings.ToolPackageId)
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };
            testProject.AdditionalProperties["PackAsTool"] = "true";
            testProject.AdditionalProperties["ToolCommandName"] = toolSettings.ToolCommandName;
            testProject.AdditionalProperties["ImplicitUsings"] = "enable";
            testProject.AdditionalProperties["Version"] = toolSettings.ToolPackageVersion;

            var multiRid = toolSettings.IncludeCurrentRid ? ToolsetInfo.LatestRuntimeIdentifiers : ToolsetInfo.LatestRuntimeIdentifiers.Replace(RuntimeInformation.RuntimeIdentifier, string.Empty).Trim(';');

            if (toolSettings.RidSpecific)
            {
                testProject.AdditionalProperties["RuntimeIdentifiers"] = multiRid;
            }
            if (toolSettings.IncludeAnyRid)
            {
                testProject.AdditionalProperties["RuntimeIdentifiers"] = testProject.AdditionalProperties.TryGetValue("RuntimeIdentifiers", out var existingRids)
                    ? $"{existingRids};any"
                    : "any";
            }

            if (toolSettings.NativeAOT)
            {
                testProject.AdditionalProperties["PublishAot"] = "true";
            }

            if (toolSettings.SelfContained)
            {
                testProject.AdditionalProperties["SelfContained"] = "true";
            }

            if (toolSettings.Trimmed)
            {
                testProject.AdditionalProperties["PublishTrimmed"] = "true";
            }

            if (toolSettings.AdditionalPackageTypes is not null && toolSettings.AdditionalPackageTypes.Length > 0)
            {
                testProject.AdditionalProperties["PackageType"] = string.Join(";", toolSettings.AdditionalPackageTypes);
            }

            testProject.SourceFiles.Add("Program.cs", "Console.WriteLine(\"Hello Tool!\");");

            var testAssetManager = new TestAssetsManager(log);
            var testAsset = testAssetManager.CreateTestProject(testProject, identifier: toolSettings.GetIdentifier());

            var testAssetProjectDirectory = Path.Combine(testAsset.Path, testProject.Name!);

            // Avoid rebuilding the package unless the project has changed.  If there is a difference in contents between the files from the TestProject and the target directory,
            // then we delete and recopy everything over.
            if (!AreDirectoriesEqual(testAssetProjectDirectory, targetDirectory))
            {
                if (Directory.Exists(targetDirectory))
                {
                    Directory.Delete(targetDirectory, true);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetDirectory)!);

                Directory.Move(testAssetProjectDirectory, targetDirectory);
            }

            // If the .nupkg hasn't been created yet, then build it.  This may be because this is the first time we need the package, or because we've updated the tests and
            // the contents of the files have changed
            string packageOutputPath = Path.Combine(targetDirectory, "bin", "Release");
            if (!Directory.Exists(packageOutputPath) || Directory.GetFiles(packageOutputPath, "*.nupkg").Length == 0)
            {
                new DotnetPackCommand(log)
                    .WithWorkingDirectory(targetDirectory)
                    .Execute(collectBinlogs ? $"--bl:{toolSettings.GetIdentifier()}-{{}}" : "")
                    .Should().Pass();

                if (toolSettings.NativeAOT)
                {
                    //  For Native AOT tools, we need to repack the tool to include the runtime-specific files that were generated during publish
                    new DotnetPackCommand(log, "-r", RuntimeInformation.RuntimeIdentifier)
                        .WithWorkingDirectory(targetDirectory)
                        .Execute(collectBinlogs ? $"--bl:{toolSettings.GetIdentifier()}-{RuntimeInformation.RuntimeIdentifier}-{{}}" : "")
                        .Should().Pass();
                }

                //  If we have built a new package, delete any old versions of it from the global packages folder
                RemovePackageFromGlobalPackages(log, toolSettings.ToolPackageId, toolSettings.ToolPackageVersion);
            }

            return packageOutputPath;
        }

        public void RemovePackageFromGlobalPackages(ITestOutputHelper log, string packageId, string version)
        {
            var result = new DotnetCommand(log, "nuget", "locals", "global-packages", "--list")
                .Execute();

            result.Should().Pass();

            string? globalPackagesPath;

            var outputDict = result.StdOut!.Split(Environment.NewLine)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToDictionary(l => l.Split(':')[0].Trim(), l => l.Split(':', count: 2)[1].Trim());

            if (!outputDict.TryGetValue("global-packages", out globalPackagesPath))
            {
                throw new InvalidOperationException("Could not determine global packages location.");
            }

            var packagePathInGlobalPackages = Path.Combine(globalPackagesPath, packageId.ToLowerInvariant(), version);

            if (Directory.Exists(packagePathInGlobalPackages))
            {
                Directory.Delete(packagePathInGlobalPackages, true);
            }
        }

        /// <summary>
        /// Compares the files in two directories (non-recursively) and returns true if they have the same files with identical text contents.
        /// </summary>
        /// <param name="dir1">First directory path</param>
        /// <param name="dir2">Second directory path</param>
        /// <returns>True if the directories have the same files with the same text contents, false otherwise.</returns>
        public static bool AreDirectoriesEqual(string dir1, string dir2)
        {
            if (!Directory.Exists(dir1) || !Directory.Exists(dir2))
                return false;

            var files1 = Directory.GetFiles(dir1);
            var files2 = Directory.GetFiles(dir2);

            if (files1.Length != files2.Length)
                return false;

            var fileNames1 = new HashSet<string>(Array.ConvertAll(files1, f => Path.GetFileName(f) ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            var fileNames2 = new HashSet<string>(Array.ConvertAll(files2, f => Path.GetFileName(f) ?? string.Empty), StringComparer.OrdinalIgnoreCase);

            if (!fileNames1.SetEquals(fileNames2))
                return false;

            foreach (var fileName in fileNames1)
            {
                var filePath1 = Path.Combine(dir1, fileName);
                var filePath2 = Path.Combine(dir2, fileName);
                if (!File.Exists(filePath1) || !File.Exists(filePath2))
                    return false;
                var text1 = File.ReadAllText(filePath1);
                var text2 = File.ReadAllText(filePath2);
                if (!string.Equals(text1, text2, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }
    }
}
