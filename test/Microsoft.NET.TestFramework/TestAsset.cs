// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// A directory wrapper around the <see cref="TestProject"/> class, or any other TestAsset type.
    /// It manages the on-disk files of the test asset and provides additional functionality to edit projects.
    /// </summary>
    public class TestAsset : TestDirectory
    {
        private readonly string? _testAssetRoot;

        private List<string>? _projectFiles;

        public string TestRoot => Path;

        /// <summary>
        /// The hashed test name (so file paths do not become too long) of the TestAsset owning test.
        /// Contains the leaf folder name of any particular test's root folder.
        /// The hashing occurs in <see cref="TestAssetsManager"/>.
        /// </summary>
        public readonly string Name;

        public ITestOutputHelper Log { get; }

        //  The TestProject from which this asset was created, if any
        public TestProject? TestProject { get; set; }

        internal TestAsset(string testDestination, string? sdkVersion, ITestOutputHelper log) : base(testDestination, sdkVersion)
        {
            Log = log;
            Name = new DirectoryInfo(testDestination).Name;
        }

        internal TestAsset(string testAssetRoot, string testDestination, string? sdkVersion, ITestOutputHelper log) : base(testDestination, sdkVersion)
        {
            if (string.IsNullOrEmpty(testAssetRoot))
            {
                throw new ArgumentException("testAssetRoot");
            }

            Log = log;
            Name = new DirectoryInfo(testAssetRoot).Name;
            _testAssetRoot = testAssetRoot;
        }

        internal void FindProjectFiles()
        {
            _projectFiles = new List<string>();

            var files = Directory.GetFiles(Path, "*.*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                if (System.IO.Path.GetFileName(file).EndsWith("proj"))
                {
                    _projectFiles.Add(file);
                }
            }
        }

        public TestAsset WithSource()
        {
            _projectFiles = new List<string>();

            var sourceDirs = Directory.GetDirectories(_testAssetRoot ?? string.Empty, "*", SearchOption.AllDirectories)
              .Where(dir => !IsBinOrObjFolder(dir));

            foreach (string sourceDir in sourceDirs)
            {
                Directory.CreateDirectory(sourceDir.Replace(_testAssetRoot ?? string.Empty, Path));
            }

            var sourceFiles = Directory.GetFiles(_testAssetRoot ?? string.Empty, "*.*", SearchOption.AllDirectories)
                                  .Where(file =>
                                  {
                                      return !IsInBinOrObjFolder(file);
                                  });

            foreach (string srcFile in sourceFiles)
            {
                string destFile = srcFile.Replace(_testAssetRoot ?? string.Empty, Path);

                if (System.IO.Path.GetFileName(srcFile).EndsWith("proj") || System.IO.Path.GetFileName(srcFile).EndsWith("xml"))
                {
                    _projectFiles.Add(destFile);
                }
                File.Copy(srcFile, destFile, true);
            }

            var substitutions = new[]
            {
                (propertyName: "TargetFramework", variableName: "CurrentTargetFramework", value: ToolsetInfo.CurrentTargetFramework),
                (propertyName: "CurrentTargetFramework", variableName: "CurrentTargetFramework", value: ToolsetInfo.CurrentTargetFramework),
                (propertyName: "RuntimeIdentifier", variableName: "LatestWinRuntimeIdentifier", value: ToolsetInfo.LatestWinRuntimeIdentifier),
                (propertyName: "RuntimeIdentifier", variableName: "LatestLinuxRuntimeIdentifier", value: ToolsetInfo.LatestLinuxRuntimeIdentifier),
                (propertyName: "RuntimeIdentifier", variableName: "LatestMacRuntimeIdentifier", value: ToolsetInfo.LatestMacRuntimeIdentifier),
                (propertyName: "RuntimeIdentifier", variableName: "LatestRuntimeIdentifiers", value: ToolsetInfo.LatestRuntimeIdentifiers)
            };

            foreach (var (propertyName, variableName, value) in substitutions)
            {
                UpdateProjProperty(propertyName, variableName, value);
            }

            foreach (var (propertyName, version) in ToolsetInfo.GetPackageVersionProperties())
            {
                ReplacePackageVersionVariable(propertyName, version);
            }

            return this;
        }

        public TestAsset UpdateProjProperty(string propertyName, string variableName, string targetValue)
        {
            return WithProjectChanges(
            p =>
            {
                if (p.Root is not null)
                {
                    var ns = p.Root.Name.Namespace;
                    var nodes = p.Root.Elements(ns + "PropertyGroup").Elements(ns + propertyName).Concat(
                                p.Root.Elements(ns + "PropertyGroup").Elements(ns + $"{propertyName}s"));

                    foreach (var node in nodes)
                    {
                        node.SetValue(node.Value.Replace($"$({variableName})", targetValue));
                    }
                }
            });
        }

        public TestAsset ReplacePackageVersionVariable(string targetName, string targetValue)
        {
            var elementsWithVersionAttribute = new[] { "PackageReference", "Package", "Sdk" };

            return WithProjectChanges(project =>
            {
                if (project.Root is not null)
                {
                    var ns = project.Root.Name.Namespace;
                    foreach (var elementName in elementsWithVersionAttribute)
                    {
                        var packageReferencesToUpdate =
                            project.Root.Descendants(ns + elementName)
                                .Select(p => p.Attribute("Version"))
                                .Where(va => va is not null && va.Value.Equals($"$({targetName})", StringComparison.OrdinalIgnoreCase));
                        foreach (var versionAttribute in packageReferencesToUpdate)
                        {
                            if (versionAttribute is not null)
                            {
                                versionAttribute.Value = targetValue;
                            }
                        }
                    }
                }
            });
        }

        public TestAsset WithTargetFramework(string targetFramework, string? projectName = null)
        {
            if (targetFramework == null)
            {
                return this;
            }
            return WithProjectChanges(
            p =>
            {
                if (p.Root is not null)
                {
                    var ns = p.Root.Name.Namespace;
                    p.Root.Elements(ns + "PropertyGroup").Elements(ns + "TargetFramework").Single().SetValue(targetFramework);
                }
            },
            projectName);
        }

        public TestAsset WithTargetFrameworks(string targetFrameworks, string? projectName = null)
        {
            if (targetFrameworks == null)
            {
                return this;
            }
            return WithProjectChanges(
            p =>
            {
                if (p.Root is not null)
                {
                    var ns = p.Root.Name.Namespace;
                    var propertyGroup = p.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Elements(ns + "TargetFramework").SingleOrDefault()?.Remove();
                    propertyGroup.Elements(ns + "TargetFrameworks").SingleOrDefault()?.Remove();
                    propertyGroup.Add(new XElement(ns + "TargetFrameworks", targetFrameworks));
                }
            },
            projectName);
        }

        public TestAsset WithTargetFrameworkOrFrameworks(string targetFrameworkOrFrameworks, bool multitarget, string? projectName = null)
        {
            if (multitarget)
            {
                return WithTargetFrameworks(targetFrameworkOrFrameworks, projectName);
            }
            else
            {
                return WithTargetFramework(targetFrameworkOrFrameworks, projectName);
            }
        }

        private TestAsset WithProjectChanges(Action<XDocument> actionOnProject, string? projectName = null)
        {
            return WithProjectChanges((path, project) =>
            {
                if (!string.IsNullOrEmpty(projectName))
                {
                    if (projectName is not null && !projectName.Equals(System.IO.Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
                if (project.Root is null)
                {
                    throw new InvalidOperationException($"The project file '{projectName}' does not have a root element.");
                }
                var ns = project.Root.Name.Namespace;
                actionOnProject(project);
            });
        }

        public TestAsset WithProjectChanges(Action<XDocument> xmlAction)
        {
            return WithProjectChanges((path, project) => xmlAction(project));
        }

        public TestAsset WithProjectChanges(Action<string, XDocument> xmlAction)
        {
            if (_projectFiles == null)
            {
                FindProjectFiles();
            }
            foreach (var projectFile in _projectFiles ?? new())
            {
                var project = XDocument.Load(projectFile);

                xmlAction(projectFile, project);

                using (var file = File.CreateText(projectFile))
                {
                    project.Save(file);
                }
            }
            return this;

        }

        public RestoreCommand GetRestoreCommand(ITestOutputHelper log, string relativePath = "")
        {
            return new RestoreCommand(log, System.IO.Path.Combine(TestRoot, relativePath));
        }

        public TestAsset Restore(ITestOutputHelper log, string relativePath = "", params string[] args)
        {
            var commandResult = GetRestoreCommand(log, relativePath)
                .Execute(args);

            commandResult.Should().Pass();

            return this;
        }

        public string ReadMSTestVersionFromProps(string propsFilePath)
        {
            XDocument doc = XDocument.Load(propsFilePath);
            XElement? msTestVersionElement = doc.Descendants("MSTestVersion").FirstOrDefault();
            return msTestVersionElement?.Value ?? throw new InvalidOperationException("MSTestVersion not found in Version.props");
        }

        public void UpdateProjectFileWithMSTestVersion(string projectPath, string msTestVersion)
        {
            if (projectPath is null)
            {
                throw new FileNotFoundException("No .csproj file found in the project directory.");
            }

            XDocument csprojDoc = XDocument.Load(projectPath);
            XElement? projectElement = csprojDoc.Element("Project");
            if (projectElement == null)
            {
                throw new InvalidOperationException("Invalid .csproj file format.");
            }

            projectElement.SetAttributeValue("Sdk", $"MSTest.Sdk/{msTestVersion}");

            csprojDoc.Save(projectPath);
        }

        private bool IsBinOrObjFolder(string directory)
        {
            var binFolder = $"{System.IO.Path.DirectorySeparatorChar}bin";
            var objFolder = $"{System.IO.Path.DirectorySeparatorChar}obj";

            directory = directory.ToLowerInvariant();
            return directory.EndsWith(binFolder)
                  || directory.EndsWith(objFolder)
                  || IsInBinOrObjFolder(directory);
        }

        private bool IsInBinOrObjFolder(string path)
        {
            var objFolderWithTrailingSlash =
              $"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}";
            var binFolderWithTrailingSlash =
              $"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}";

            path = path.ToLowerInvariant();
            return path.Contains(binFolderWithTrailingSlash)
                  || path.Contains(objFolderWithTrailingSlash);
        }
    }
}
