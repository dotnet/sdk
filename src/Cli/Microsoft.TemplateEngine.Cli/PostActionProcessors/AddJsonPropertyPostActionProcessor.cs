// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class AddJsonPropertyPostActionProcessor : PostActionProcessorBase
    {
        private const string AllowFileCreationArgument = "allowFileCreation";
        private const string AllowPathCreationArgument = "allowPathCreation";
        private const string JsonFileNameArgument = "jsonFileName";
        private const string ParentPropertyPathArgument = "parentPropertyPath";
        private const string NewJsonPropertyNameArgument = "newJsonPropertyName";
        private const string NewJsonPropertyValueArgument = "newJsonPropertyValue";
        private const string DetectRepoRootForFileCreation = "detectRepositoryRootForFileCreation";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        private static readonly JsonDocumentOptions DeserializerOptions = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        public override Guid Id => ActionProcessorId;

        internal static Guid ActionProcessorId { get; } = new Guid("695A3659-EB40-4FF5-A6A6-C9C4E629FCB0");

        protected override bool ProcessInternal(
            IEngineEnvironmentSettings environment,
            IPostAction action,
            ICreationEffects creationEffects,
            ICreationResult templateCreationResult,
            string outputBasePath)
        {
            if (!action.Args.TryGetValue(JsonFileNameArgument, out string? jsonFileName))
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, JsonFileNameArgument));
                return false;
            }

            IReadOnlyList<string> jsonFiles = FindFilesInCurrentFolderOrParentFolder(environment.Host.FileSystem, outputBasePath, jsonFileName);

            if (jsonFiles.Count == 0)
            {
                if (!bool.TryParse(action.Args.GetValueOrDefault(AllowFileCreationArgument, "false"), out bool createFile))
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotBoolean, AllowFileCreationArgument));
                    return false;
                }

                if (!createFile)
                {
                    Reporter.Error.WriteLine(LocalizableStrings.PostAction_ModifyJson_Error_NoJsonFile);
                    return false;
                }

                if (!bool.TryParse(action.Args.GetValueOrDefault(DetectRepoRootForFileCreation, "false"), out bool detectRepoRoot))
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotBoolean, DetectRepoRootForFileCreation));
                    return false;
                }

                string newJsonFilePath = Path.Combine(detectRepoRoot ? GetRootDirectory(environment.Host.FileSystem, outputBasePath) : outputBasePath, jsonFileName);
                environment.Host.FileSystem.WriteAllText(newJsonFilePath, "{}");
                jsonFiles = new List<string> { newJsonFilePath };
            }

            if (jsonFiles.Count > 1)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_MultipleJsonFiles, jsonFileName));
                return false;
            }

            var newJsonElementProperties = JsonContentParameters.CreateFromPostAction(action);

            if (newJsonElementProperties == null)
            {
                return false;
            }

            JsonNode? newJsonContent = AddElementToJson(
                environment.Host.FileSystem,
                jsonFiles[0],
                newJsonElementProperties!.ParentProperty,
                ":",
                newJsonElementProperties.NewJsonPropertyName,
                newJsonElementProperties.NewJsonPropertyValue,
                action);

            if (newJsonContent == null)
            {
                return false;
            }

            environment.Host.FileSystem.WriteAllText(jsonFiles[0], newJsonContent.ToJsonString(SerializerOptions));

            Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Succeeded, jsonFileName));

            return true;
        }

        private static JsonNode? AddElementToJson(IPhysicalFileSystem fileSystem, string targetJsonFile, string? propertyPath, string propertyPathSeparator, string newJsonPropertyName, string newJsonPropertyValue, IPostAction action)
        {
            JsonNode? jsonContent = JsonNode.Parse(fileSystem.ReadAllText(targetJsonFile), nodeOptions: null, documentOptions: DeserializerOptions);

            if (jsonContent == null)
            {
                return null;
            }

            if (!bool.TryParse(action.Args.GetValueOrDefault(AllowPathCreationArgument, "false"), out bool createPath))
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotBoolean, AllowPathCreationArgument));
                return false;
            }

            JsonNode? parentProperty = FindJsonNode(jsonContent, propertyPath, propertyPathSeparator, createPath);

            if (parentProperty == null)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ParentPropertyPathInvalid, propertyPath));
                return null;
            }

            try
            {
                parentProperty[newJsonPropertyName] = JsonNode.Parse(newJsonPropertyValue);
            }
            catch (JsonException)
            {
                parentProperty[newJsonPropertyName] = newJsonPropertyValue;
            }

            return jsonContent;
        }

        private static JsonNode? FindJsonNode(JsonNode content, string? nodePath, string pathSeparator, bool createPath)
        {
            if (nodePath == null)
            {
                return content;
            }

            string[] properties = nodePath.Split(pathSeparator);

            JsonNode? node = content;

            foreach (string property in properties)
            {
                if (node == null)
                {
                    return null;
                }

                JsonNode? childNode = node[property];
                if (childNode is null && createPath)
                {
                    node[property] = childNode = new JsonObject();
                }

                node = childNode;
            }

            return node;
        }

        private static string[] FindFilesInCurrentFolderOrParentFolder(
            IPhysicalFileSystem fileSystem,
            string startPath,
            string matchPattern)
        {
            string? directory = fileSystem.DirectoryExists(startPath) ? startPath : Path.GetDirectoryName(startPath);

            if (directory == null)
            {
                throw new InvalidOperationException();
            }

            int numberOfUpLevels = 0;

            do
            {
                Reporter.Verbose.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Verbose_AttemptingToFindJsonFile, matchPattern, directory));
                string[] filesInDir = fileSystem.EnumerateFileSystemEntries(directory, matchPattern, SearchOption.AllDirectories).ToArray();

                if (filesInDir.Length > 0)
                {
                    return filesInDir;
                }

                directory = Path.GetPathRoot(directory) != directory ? Directory.GetParent(directory)?.FullName : null;
                numberOfUpLevels++;
            }
            while (directory != null && numberOfUpLevels <= 1);

            return Array.Empty<string>();
        }

        private static string GetRootDirectory(IPhysicalFileSystem fileSystem, string outputBasePath)
        {
            string? currentDirectory = outputBasePath;
            string? directoryWithSln = null;
            while (currentDirectory is not null)
            {
                if (fileSystem.FileExists(Path.Combine(currentDirectory, "global.json")) ||
                    fileSystem.FileExists(Path.Combine(currentDirectory, ".git")) ||
                    fileSystem.DirectoryExists(Path.Combine(currentDirectory, ".git")))
                {
                    // If we found global.json or .git, we immediately return the directory as the repo root.
                    // We won't go up any further.
                    return currentDirectory;
                }

                // DirectoryExists here should always be true in practice, but for the way tests are mocking the file system, it's not.
                // The check was added to prevent test failures similar to:
                // System.IO.DirectoryNotFoundException : Could not find a part of the path '/Users/runner/work/1/s/artifacts/bin/Microsoft.TemplateEngine.Cli.UnitTests/Release/sandbox'.
                // We get to this exception when doing `EnumerateFiles` on a directory that was virtually created in memory (not really available on disk).
                // EnumerateFiles tries to access the physical file system, which then fails.
                if (fileSystem.DirectoryExists(currentDirectory) &&
                    (fileSystem.EnumerateFiles(currentDirectory, "*.sln", SearchOption.TopDirectoryOnly).Any() ||
                    fileSystem.EnumerateFiles(currentDirectory, "*.slnx", SearchOption.TopDirectoryOnly).Any()))
                {
                    directoryWithSln = currentDirectory;
                }

                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            }

            // If we reach here, that means we didn't find .git or global.json.
            // So, we return the directory where we found a .sln/.slnx file, if any.
            // Note that when we keep track of directoryWithSln, we keep updating it from sln/slnx from parent directories, if found.
            // This means that if there are multiple .sln/.slnx files in the parent directories, we will return the top-most one.
            return directoryWithSln ?? outputBasePath;
        }

        private class JsonContentParameters
        {
            private JsonContentParameters(string? parentProperty, string newJsonPropertyName, string newJsonPropertyValue)
            {
                ParentProperty = parentProperty;
                NewJsonPropertyName = newJsonPropertyName;
                NewJsonPropertyValue = newJsonPropertyValue;
            }

            public string? ParentProperty { get; }

            public string NewJsonPropertyName { get; }

            public string NewJsonPropertyValue { get; }

            /// <summary>
            /// Creates an instance of <see cref="JsonContentParameters"/> based on the configured arguments in the Post Action.
            /// </summary>
            /// <param name="action"></param>
            /// <returns>A <see cref="JsonContentParameters"/> instance, or null if no instance could be created.</returns>
            public static JsonContentParameters? CreateFromPostAction(IPostAction action)
            {
                // If no ParentPropertyPath is specified, the new JSON property must be added to the root of the
                // document.
                action.Args.TryGetValue(ParentPropertyPathArgument, out string? parentProperty);
                action.Args.TryGetValue(NewJsonPropertyNameArgument, out string? newJsonPropertyName);
                action.Args.TryGetValue(NewJsonPropertyValueArgument, out string? newJsonPropertyValue);

                if (newJsonPropertyName == null)
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, NewJsonPropertyNameArgument));
                    return null;
                }

                if (newJsonPropertyValue == null)
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, NewJsonPropertyValueArgument));
                    return null;
                }

                return new JsonContentParameters(parentProperty, newJsonPropertyName, newJsonPropertyValue);
            }
        }
    }
}
