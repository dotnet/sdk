// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Generates the $(project).runtimeconfig.json and optionally $(project).runtimeconfig.dev.json files
    /// for a project.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class GenerateRuntimeConfigurationFiles : TaskBase, IMultiThreadableTask
    {
        public string AssetsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        [Required]
        public string TargetFrameworkMoniker { get; set; }

        [Required]
        public string RuntimeConfigPath { get; set; }

        public string RuntimeConfigDevPath { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string PlatformLibraryName { get; set; }

        public ITaskItem[] RuntimeFrameworks { get; set; }

        public string RollForward { get; set; }

        public string UserRuntimeConfig { get; set; }

        public ITaskItem[] HostConfigurationOptions { get; set; }

        public ITaskItem[] AdditionalProbingPaths { get; set; }

        public bool IsSelfContained { get; set; }

        public bool WriteAdditionalProbingPathsToMainConfig { get; set; }

        public bool WriteIncludedFrameworks { get; set; }

        /// <summary>
        /// True to generate probing paths to runtimeconfig.dev.json file.
        /// </summary>
        public bool GenerateProbingPathsToRuntimeConfigDevFile { get; set; }

        /// <summary>
        /// True to generate switches that enable Hot Reload to runtimeconfig.dev.json file.
        /// </summary>
        public bool GenerateHotReloadRuntimeOptionsToRuntimeConfigDevFile { get; set; }

        private bool GenerateRuntimeConfigDevFile =>
            GenerateProbingPathsToRuntimeConfigDevFile || GenerateHotReloadRuntimeOptionsToRuntimeConfigDevFile;

        public bool AlwaysIncludeCoreFramework { get; set; }

        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        List<ITaskItem> _filesWritten = new();

        private static readonly string[] RollForwardValues = new string[]
        {
            "Disable",
            "LatestPatch",
            "Minor",
            "LatestMinor",
            "Major",
            "LatestMajor"
        };

        [Output]
        public ITaskItem[] FilesWritten
        {
            get { return _filesWritten.ToArray(); }
        }

        protected override void ExecuteCore()
        {
            if (!WriteAdditionalProbingPathsToMainConfig)
            {
                // If we want to generate the runtimeconfig.dev.json file
                // and we have additional probing paths to add to it
                // BUT the runtimeconfigdevpath is empty, log a warning.
                if (GenerateProbingPathsToRuntimeConfigDevFile && AdditionalProbingPaths?.Any() == true && string.IsNullOrEmpty(RuntimeConfigDevPath))
                {
                    Log.LogWarning(Strings.SkippingAdditionalProbingPaths);
                }
            }

            if (!string.IsNullOrEmpty(RollForward))
            {
                if (!RollForwardValues.Contains(RollForward, StringComparer.OrdinalIgnoreCase))
                {
                    Log.LogError(Strings.InvalidRollForwardValue, RollForward, string.Join(", ", RollForwardValues));
                    return;
                }
            }

            if (AssetsFilePath == null)
            {
                var isFrameworkDependent = LockFileExtensions.IsFrameworkDependent(
                    RuntimeFrameworks,
                    IsSelfContained,
                    RuntimeIdentifier,
                    string.IsNullOrWhiteSpace(PlatformLibraryName));

                if (isFrameworkDependent != true)
                {
                    throw new ArgumentException(
                        $"{nameof(DependencyContextBuilder)} Does not support non FrameworkDependent without asset file. " +
                        $"runtimeFrameworks: {string.Join(",", RuntimeFrameworks.Select(r => r.ItemSpec))} " +
                        $"isSelfContained: {IsSelfContained} " +
                        $"runtimeIdentifier: {RuntimeIdentifier} " +
                        $"platformLibraryName: {PlatformLibraryName}");
                }

                if (PlatformLibraryName != null)
                {
                    throw new ArgumentException(
                        "Does not support non null PlatformLibraryName(TFM < 3) without asset file.");
                }

                WriteRuntimeConfig(
                    RuntimeFrameworks.Select(r => new ProjectContext.RuntimeFramework(r)).ToArray(),
                    null,
                    isFrameworkDependent: true, new List<LockFileItem>());
            }
            else
            {
                AbsolutePath assetsPath = TaskEnvironment.GetAbsolutePath(AssetsFilePath);
                LockFile lockFile = new LockFileCache(this).GetLockFile(assetsPath);

                ProjectContext projectContext = lockFile.CreateProjectContext(
                    TargetFramework,
                    RuntimeIdentifier,
                    PlatformLibraryName,
                    RuntimeFrameworks,
                    IsSelfContained);

                WriteRuntimeConfig(projectContext.RuntimeFrameworks,
                    projectContext.PlatformLibrary,
                    projectContext.IsFrameworkDependent,
                    projectContext.LockFile.PackageFolders);

                if (GenerateRuntimeConfigDevFile && !string.IsNullOrEmpty(RuntimeConfigDevPath))
                {
                    WriteDevRuntimeConfig(projectContext.LockFile.PackageFolders);
                }
            }
        }

        private void WriteRuntimeConfig(
            ProjectContext.RuntimeFramework[] runtimeFrameworks,
            LockFileTargetLibrary platformLibrary,
            bool isFrameworkDependent,
            IList<LockFileItem> packageFolders)
        {
            RuntimeConfig config = new()
            {
                RuntimeOptions = new RuntimeOptions()
            };

            AddFrameworks(
                config.RuntimeOptions,
                runtimeFrameworks,
                platformLibrary,
                isFrameworkDependent);
            AddUserRuntimeOptions(config.RuntimeOptions);

            // HostConfigurationOptions are added after AddUserRuntimeOptions so if there are
            // conflicts the HostConfigurationOptions win. The reasoning is that HostConfigurationOptions
            // can be changed using MSBuild properties, which can be specified at build time.
            AddHostConfigurationOptions(config.RuntimeOptions);

            if (WriteAdditionalProbingPathsToMainConfig)
            {
                AddAdditionalProbingPaths(config.RuntimeOptions, packageFolders);
            }

            WriteToJsonFile(TaskEnvironment.GetAbsolutePath(RuntimeConfigPath), config);
            _filesWritten.Add(new TaskItem(RuntimeConfigPath));
        }

        private void AddFrameworks(RuntimeOptions runtimeOptions,
                                   ProjectContext.RuntimeFramework[] runtimeFrameworks,
                                   LockFileTargetLibrary lockFilePlatformLibrary,
                                   bool isFrameworkDependent)
        {
            runtimeOptions.Tfm = NuGetFramework.Parse(TargetFrameworkMoniker).GetShortFolderName();

            var frameworks = new List<RuntimeConfigFramework>();
            if (runtimeFrameworks == null || runtimeFrameworks.Length == 0)
            {
                // If the project is not targetting .NET Core, it will not have any platform library (and is marked as non-FrameworkDependent).
                if (lockFilePlatformLibrary != null)
                {
                    //  If there are no RuntimeFrameworks (which would be set in the ProcessFrameworkReferences task based
                    //  on FrameworkReference items), then use package resolved from MicrosoftNETPlatformLibrary for
                    //  the runtimeconfig
                    RuntimeConfigFramework framework = new()
                    {
                        Name = lockFilePlatformLibrary.Name,
                        Version = lockFilePlatformLibrary.Version.ToNormalizedString()
                    };

                    frameworks.Add(framework);
                }
            }
            else
            {
                HashSet<string> usedFrameworkNames = new(StringComparer.OrdinalIgnoreCase);
                foreach (var platformLibrary in runtimeFrameworks)
                {
                    //  In earlier versions of the SDK, we would exclude Microsoft.NETCore.App from the frameworks listed in the runtimeconfig file.
                    //  This was originally a workaround for a bug: https://github.com/dotnet/core-setup/issues/4947
                    //  We would only do this for framework-dependent apps, as the full list was required for self-contained apps.
                    //  As the bug is fixed, we now always include the Microsoft.NETCore.App framework by default for .NET Core 6 and higher
                    if (!AlwaysIncludeCoreFramework &&
                        runtimeFrameworks.Length > 1 &&
                        platformLibrary.Name.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase) &&
                        isFrameworkDependent)
                    {
                        continue;
                    }

                    //  Don't add multiple entries for the same shared framework.
                    //  This is necessary if there are FrameworkReferences to different profiles
                    //  that map to the same shared framework.
                    if (!usedFrameworkNames.Add(platformLibrary.Name))
                    {
                        continue;
                    }

                    RuntimeConfigFramework framework = new()
                    {
                        Name = platformLibrary.Name,
                        Version = platformLibrary.Version
                    };

                    frameworks.Add(framework);
                }
            }

            if (isFrameworkDependent)
            {
                runtimeOptions.RollForward = RollForward;

                //  If there is only one runtime framework, then it goes in the framework property of the json
                //  If there are multiples, then we leave the framework property unset and put the list in
                //  the frameworks property.
                if (frameworks.Count == 1)
                {
                    runtimeOptions.Framework = frameworks[0];
                }
                else
                {
                    runtimeOptions.Frameworks = frameworks;
                }
            }
            else if (WriteIncludedFrameworks)
            {
                //  Self-contained apps don't have framework references, instead write the frameworks
                //  into the includedFrameworks property.
                runtimeOptions.IncludedFrameworks = frameworks;
            }
        }

        private void AddUserRuntimeOptions(RuntimeOptions runtimeOptions)
        {
            if (string.IsNullOrEmpty(UserRuntimeConfig))
            {
                return;
            }

            AbsolutePath userConfigPath = TaskEnvironment.GetAbsolutePath(UserRuntimeConfig);
            if (!File.Exists(userConfigPath))
            {
                return;
            }

            JsonObject runtimeOptionsFromProject = (JsonObject)JsonNode.Parse(
                File.ReadAllText(userConfigPath),
                documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

            foreach (KeyValuePair<string, JsonNode> runtimeOption in runtimeOptionsFromProject)
            {
                runtimeOptions.RawOptions.Add(runtimeOption.Key, runtimeOption.Value);
            }
        }

        private void AddHostConfigurationOptions(RuntimeOptions runtimeOptions)
        {
            if (HostConfigurationOptions == null || !HostConfigurationOptions.Any())
            {
                return;
            }

            JsonObject configProperties = GetConfigProperties(runtimeOptions);

            foreach (var hostConfigurationOption in HostConfigurationOptions)
            {
                configProperties[hostConfigurationOption.ItemSpec] = GetConfigPropertyValue(hostConfigurationOption);
            }
        }

        private static JsonObject GetConfigProperties(RuntimeOptions runtimeOptions)
        {
            if (!runtimeOptions.RawOptions.TryGetValue("configProperties", out JsonNode configProperties)
                || configProperties is not JsonObject)
            {
                configProperties = new JsonObject();
                runtimeOptions.RawOptions["configProperties"] = configProperties;
            }

            return (JsonObject)configProperties;
        }

        private static JsonNode GetConfigPropertyValue(ITaskItem hostConfigurationOption)
        {
            string valueString = hostConfigurationOption.GetMetadata("Value");

            if (bool.TryParse(valueString, out bool boolValue))
            {
                return JsonValue.Create(boolValue);
            }

            if (int.TryParse(valueString, out int intValue))
            {
                return JsonValue.Create(intValue);
            }

            return JsonValue.Create(valueString);
        }

        private void WriteDevRuntimeConfig(IList<LockFileItem> packageFolders)
        {
            RuntimeConfig devConfig = new()
            {
                RuntimeOptions = new RuntimeOptions()
            };

            if (GenerateProbingPathsToRuntimeConfigDevFile)
            {
                AddAdditionalProbingPaths(devConfig.RuntimeOptions, packageFolders);
            }

            if (GenerateHotReloadRuntimeOptionsToRuntimeConfigDevFile)
            {
                JObject configProperties = GetConfigProperties(devConfig.RuntimeOptions);
                configProperties["System.Reflection.Metadata.MetadataUpdater.IsSupported"] = true;
                configProperties["System.StartupHookProvider.IsSupported"] = true;
            }

            WriteToJsonFile(TaskEnvironment.GetAbsolutePath(RuntimeConfigDevPath), devConfig);
            _filesWritten.Add(new TaskItem(RuntimeConfigDevPath));
        }

        private void AddAdditionalProbingPaths(RuntimeOptions runtimeOptions, IList<LockFileItem> packageFolders)
        {
            if (runtimeOptions.AdditionalProbingPaths == null)
            {
                runtimeOptions.AdditionalProbingPaths = new List<string>();
            }

            // Add the specified probing paths first so they are probed first
            if (AdditionalProbingPaths?.Any() == true)
            {
                foreach (var additionalProbingPath in AdditionalProbingPaths)
                {
                    runtimeOptions.AdditionalProbingPaths.Add(additionalProbingPath.ItemSpec);
                }
            }

            foreach (var packageFolder in packageFolders)
            {
                // DotNetHost doesn't handle additional probing paths with a trailing slash
                runtimeOptions.AdditionalProbingPaths.Add(EnsureNoTrailingDirectorySeparator(packageFolder.Path));
            }
        }

        private static string EnsureNoTrailingDirectorySeparator(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                char lastChar = path[path.Length - 1];
                if (lastChar == Path.DirectorySeparatorChar)
                {
                    path = path.Substring(0, path.Length - 1);
                }
            }

            return path;
        }

        private static void WriteToJsonFile(string fileName, RuntimeConfig value)
        {
            // Build the document explicitly with the System.Text.Json node API instead of a
            // reflection-based serializer, which is not trim/AOT safe (IL2026/IL3050). The writer
            // is configured to reproduce the previous output exactly: 2-space indentation,
            // environment newlines, and relaxed escaping (so characters such as '+' in paths are
            // written verbatim rather than as \uXXXX escapes).
            JsonObject json = new()
            {
                ["runtimeOptions"] = SerializeRuntimeOptions(value.RuntimeOptions)
            };

            JsonWriterOptions options = new()
            {
                Indented = true,
                NewLine = Environment.NewLine,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            using (FileStream stream = File.Create(fileName))
            using (Utf8JsonWriter writer = new(stream, options))
            {
                json.WriteTo(writer);
            }
        }

        private static JsonObject SerializeRuntimeOptions(RuntimeOptions runtimeOptions)
        {
            // Declared properties are emitted in declaration order. Null values are omitted.
            JsonObject json = new();

            if (runtimeOptions.Tfm != null)
            {
                json["tfm"] = runtimeOptions.Tfm;
            }

            if (runtimeOptions.RollForward != null)
            {
                json["rollForward"] = runtimeOptions.RollForward;
            }

            if (runtimeOptions.Framework != null)
            {
                json["framework"] = SerializeFramework(runtimeOptions.Framework);
            }

            if (runtimeOptions.Frameworks != null)
            {
                json["frameworks"] = SerializeFrameworks(runtimeOptions.Frameworks);
            }

            if (runtimeOptions.IncludedFrameworks != null)
            {
                json["includedFrameworks"] = SerializeFrameworks(runtimeOptions.IncludedFrameworks);
            }

            if (runtimeOptions.AdditionalProbingPaths != null)
            {
                JsonArray probingPaths = new();
                foreach (string probingPath in runtimeOptions.AdditionalProbingPaths)
                {
                    // Cast to JsonNode so the non-generic Add overload is used; the generic
                    // JsonArray.Add<T> is annotated as trim/AOT unsafe (IL2026/IL3050).
                    probingPaths.Add((JsonNode)probingPath);
                }

                json["additionalProbingPaths"] = probingPaths;
            }

            // RawOptions is the extension-data bag; its entries are written as direct children of
            // runtimeOptions, after the declared properties. Clone each value so it can be
            // re-parented into this document regardless of where it originated.
            foreach (KeyValuePair<string, JsonNode> rawOption in runtimeOptions.RawOptions)
            {
                json[rawOption.Key] = rawOption.Value?.DeepClone();
            }

            return json;
        }

        private static JsonArray SerializeFrameworks(List<RuntimeConfigFramework> frameworks)
        {
            JsonArray array = new();

            foreach (RuntimeConfigFramework framework in frameworks)
            {
                // Cast to JsonNode to use the non-generic Add overload (the generic
                // JsonArray.Add<T> is annotated as trim/AOT unsafe).
                array.Add((JsonNode)SerializeFramework(framework));
            }

            return array;
        }

        private static JsonObject SerializeFramework(RuntimeConfigFramework framework)
        {
            JsonObject json = new();

            if (framework.Name != null)
            {
                json["name"] = framework.Name;
            }

            if (framework.Version != null)
            {
                json["version"] = framework.Version;
            }

            return json;
        }
    }
}
