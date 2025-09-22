// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class OutputPathCalculator
    {
        public string? ProjectPath { get; set; }
        public bool UseArtifactsOutput { get; set; }
        public bool IncludeProjectNameInArtifactsPaths { get; set; }
        public string? ArtifactsPath { get; set; }
        public string? TargetFramework { get; set; }
        public string? TargetFrameworks { get; set; }

        public string? RuntimeIdentifier { get; set; }

        public bool IsSdkProject { get; set; } = true;

        public static OutputPathCalculator FromProject(string projectPath, TestAsset testAsset)
        {
            return FromProject(projectPath, testAsset.TestProject);
        }

        public static OutputPathCalculator FromProject(string? projectPath, TestProject? testProject = null)
        {
            string? originalProjectPath = projectPath;

            if (!File.Exists(projectPath) && Directory.Exists(projectPath))
            {
                projectPath = Directory.GetFiles(projectPath, "*.*proj").FirstOrDefault();
            }

            //  Support passing in the root test path and looking in subfolder specified by testProject
            if (projectPath == null && testProject != null && testProject.Name is not null && originalProjectPath is not null)
            {
                projectPath = Path.Combine(originalProjectPath, testProject.Name);

                if (!File.Exists(projectPath) && Directory.Exists(projectPath))
                {
                    projectPath = Directory.GetFiles(projectPath, "*.*proj").FirstOrDefault();
                }
            }

            if (projectPath == null)
            {
                throw new ArgumentException($"Test project not found under {projectPath}");
            }

            var calculator = new OutputPathCalculator()
            {
                ProjectPath = projectPath,
            };

            if (testProject != null)
            {
                calculator.UseArtifactsOutput = testProject.UseArtifactsOutput;
                calculator.IsSdkProject = testProject.IsSdkProject;
                calculator.IncludeProjectNameInArtifactsPaths = true;

                if (testProject.TargetFrameworks.Contains(';'))
                {
                    calculator.TargetFrameworks = testProject.TargetFrameworks;
                }
                else
                {
                    calculator.TargetFramework = testProject.TargetFrameworks;
                }

                calculator.RuntimeIdentifier = testProject.RuntimeIdentifier;

                if (calculator.IncludeProjectNameInArtifactsPaths)
                {
                    string? directoryBuildPropsFile = GetDirectoryBuildPropsPath(projectPath);
                    if (directoryBuildPropsFile == null)
                    {
                        throw new InvalidOperationException("Couldn't find Directory.Build.props for test project " + projectPath);
                    }
                    calculator.ArtifactsPath = Path.Combine(Path.GetDirectoryName(directoryBuildPropsFile) ?? string.Empty, "artifacts");
                }
                else
                {
                    calculator.ArtifactsPath = Path.Combine(Path.GetDirectoryName(projectPath) ?? string.Empty, "artifacts");
                }
            }
            else
            {
                var project = XDocument.Load(projectPath);

                if (project.Root is null)
                {
                    throw new InvalidOperationException($"The project file '{projectPath}' does not have a root element.");
                }

                var ns = project.Root.Name.Namespace;

                var useArtifactsOutputElement = project.Root.Elements(ns + "PropertyGroup").Elements(ns + "UseArtifactsOutput").FirstOrDefault();
                if (useArtifactsOutputElement != null)
                {
                    calculator.UseArtifactsOutput = bool.Parse(useArtifactsOutputElement.Value);
                    if (calculator.UseArtifactsOutput)
                    {
                        calculator.IncludeProjectNameInArtifactsPaths = false;
                        calculator.ArtifactsPath = Path.Combine(Path.GetDirectoryName(projectPath) ?? string.Empty, "artifacts");
                    }
                }

                var targetFrameworkElement = project.Root.Elements(ns + "PropertyGroup").Elements(ns + "TargetFramework").FirstOrDefault();
                if (targetFrameworkElement != null)
                {
                    calculator.TargetFramework = targetFrameworkElement.Value;
                }

                var targetFrameworksElement = project.Root.Elements(ns + "PropertyGroup").Elements(ns + "TargetFrameworks").FirstOrDefault();
                if (targetFrameworksElement != null)
                {
                    calculator.TargetFrameworks = targetFrameworksElement.Value;
                }

                var runtimeIdentifierElement = project.Root.Elements(ns + "PropertyGroup").Elements(ns + "RuntimeIdentifier").FirstOrDefault();
                if (runtimeIdentifierElement != null)
                {
                    calculator.RuntimeIdentifier = runtimeIdentifierElement.Value;
                }

                var directoryBuildPropsFile = GetDirectoryBuildPropsPath(projectPath);
                if (directoryBuildPropsFile != null)
                {
                    var dbp = XDocument.Load(directoryBuildPropsFile);
                    if (dbp.Root is null)
                    {
                        throw new InvalidOperationException($"The project file '{directoryBuildPropsFile}' does not have a root element.");
                    }
                    var dbpns = dbp.Root.Name.Namespace;

                    var dbpUsesArtifacts = dbp.Root.Elements(dbpns + "PropertyGroup").Elements(dbpns + "UseArtifactsOutput").FirstOrDefault();
                    if (dbpUsesArtifacts != null)
                    {

                        calculator.UseArtifactsOutput = bool.Parse(dbpUsesArtifacts.Value);
                        if (calculator.UseArtifactsOutput)
                        {
                            calculator.IncludeProjectNameInArtifactsPaths = true;
                            calculator.ArtifactsPath = Path.Combine(Path.GetDirectoryName(directoryBuildPropsFile) ?? string.Empty, "artifacts");
                        }
                    }
                }
            }

            return calculator;
        }

        private static string? GetDirectoryBuildPropsPath(string projectPath)
        {
            string? folder = Path.GetDirectoryName(projectPath);
            while (folder != null)
            {
                string directoryBuildPropsFile = Path.Combine(folder, "Directory.Build.props");
                if (File.Exists(directoryBuildPropsFile))
                {
                    return directoryBuildPropsFile;
                }
                folder = Path.GetDirectoryName(folder);
            }
            return null;
        }

        public bool IsMultiTargeted()
        {
            return !string.IsNullOrEmpty(TargetFrameworks);
        }

        public string GetOutputDirectory(string? targetFramework = null, string configuration = "Debug", string? runtimeIdentifier = "", string? platform = "")
        {
            if (UseArtifactsOutput)
            {
                string pivot = configuration.ToLowerInvariant();
                if (IsMultiTargeted())
                {
                    pivot += "_" + targetFramework ?? TargetFramework;
                }
                if (string.IsNullOrEmpty(runtimeIdentifier))
                {
                    runtimeIdentifier = RuntimeIdentifier;
                }
                if (!string.IsNullOrEmpty(runtimeIdentifier))
                {
                    pivot += "_" + runtimeIdentifier;
                }

                if (IncludeProjectNameInArtifactsPaths)
                {
                    return Path.Combine(ArtifactsPath ?? string.Empty, "bin", Path.GetFileNameWithoutExtension(ProjectPath) ?? string.Empty, pivot);
                }
                else
                {
                    return Path.Combine(ArtifactsPath ?? string.Empty, "bin", pivot);
                }
            }
            else
            {
                targetFramework ??= TargetFramework ?? string.Empty;
                configuration ??= string.Empty;
                runtimeIdentifier ??= RuntimeIdentifier ?? string.Empty;
                platform ??= string.Empty;

                if (IsSdkProject)
                {
                    string output = Path.Combine(Path.GetDirectoryName(ProjectPath) ?? string.Empty, "bin", platform, configuration, targetFramework, runtimeIdentifier);
                    return output;
                }
                else
                {
                    string output = Path.Combine(Path.GetDirectoryName(ProjectPath) ?? string.Empty, "bin", platform, configuration);
                    return output;
                }
            }
        }

        public string GetPublishDirectory(string? targetFramework = null, string configuration = "Debug", string? runtimeIdentifier = "", string? platform = "")
        {
            if (UseArtifactsOutput)
            {
                string pivot = configuration.ToLowerInvariant();
                if (IsMultiTargeted())
                {
                    pivot += "_" + targetFramework ?? TargetFramework;
                }
                if (string.IsNullOrEmpty(runtimeIdentifier))
                {
                    runtimeIdentifier = RuntimeIdentifier;
                }
                if (!string.IsNullOrEmpty(runtimeIdentifier))
                {
                    pivot += "_" + runtimeIdentifier;
                }

                if (IncludeProjectNameInArtifactsPaths)
                {
                    return Path.Combine(ArtifactsPath ?? string.Empty, "publish", Path.GetFileNameWithoutExtension(ProjectPath) ?? string.Empty, pivot);
                }
                else
                {
                    return Path.Combine(ArtifactsPath ?? string.Empty, "publish", pivot);
                }
            }
            else
            {
                targetFramework ??= TargetFramework ?? string.Empty;
                configuration ??= string.Empty;
                runtimeIdentifier ??= RuntimeIdentifier ?? string.Empty;
                platform ??= string.Empty;

                string output = Path.Combine(Path.GetDirectoryName(ProjectPath) ?? string.Empty, "bin", platform, configuration, targetFramework, runtimeIdentifier, "publish");
                return output;
            }
        }

        public string GetIntermediateDirectory(string? targetFramework = null, string configuration = "Debug", string? runtimeIdentifier = "")
        {
            if (UseArtifactsOutput)
            {
                string pivot = configuration.ToLowerInvariant();
                if (IsMultiTargeted())
                {
                    pivot += "_" + targetFramework ?? TargetFramework;
                }
                if (string.IsNullOrEmpty(runtimeIdentifier))
                {
                    runtimeIdentifier = RuntimeIdentifier;
                }
                if (!string.IsNullOrEmpty(runtimeIdentifier))
                {
                    pivot += "_" + runtimeIdentifier;
                }

                if (IncludeProjectNameInArtifactsPaths)
                {
                    return Path.Combine(ArtifactsPath ?? string.Empty, "obj", Path.GetFileNameWithoutExtension(ProjectPath) ?? string.Empty, pivot);
                }
                else
                {
                    return Path.Combine(ArtifactsPath ?? string.Empty, "obj", pivot);
                }

            }

            targetFramework = targetFramework ?? TargetFramework ?? string.Empty;
            configuration = configuration ?? string.Empty;
            runtimeIdentifier = runtimeIdentifier ?? RuntimeIdentifier ?? string.Empty;

            string output = Path.Combine(Path.GetDirectoryName(ProjectPath) ?? string.Empty, "obj", configuration, targetFramework, runtimeIdentifier);
            return output;
        }

        public string GetPackageDirectory(string configuration = "Debug")
        {
            if (UseArtifactsOutput)
            {
                return Path.Combine(ArtifactsPath ?? string.Empty, "package", configuration.ToLowerInvariant());
            }
            else
            {
                return Path.Combine(Path.GetDirectoryName(ProjectPath) ?? string.Empty, "bin", configuration);
            }
        }
    }
}
