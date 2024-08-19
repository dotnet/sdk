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
    internal class RemoveJsonPropertyPostActionProcessor : PostActionProcessorBase
    {
        private const string JsonFileNameArgument = "jsonFileName";
        private const string ParentPropertyPathArgument = "parentPropertyPath";
        private const string JsonPropertyNameArgument = "jsonPropertyName";

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

        internal static Guid ActionProcessorId { get; } = new Guid("73406DFC-1255-4798-86E8-DF66AB9F7A18");

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
                return true;
            }

            if (jsonFiles.Count > 1)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_MultipleJsonFiles, jsonFileName));
                return false;
            }

            var jsonElementProperties = JsonContentParameters.CreateFromPostAction(action);

            if (jsonElementProperties is null)
            {
                return false;
            }

            JsonNode? newJsonContent = RemoveElementFromJson(
                environment.Host.FileSystem,
                jsonFiles[0],
                jsonElementProperties!.ParentProperty,
                ":",
                jsonElementProperties.JsonPropertyName,
                action);

            if (newJsonContent is null)
            {
                return false;
            }

            environment.Host.FileSystem.WriteAllText(jsonFiles[0], newJsonContent.ToJsonString(SerializerOptions));

            Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Succeeded, jsonFileName));

            return true;
        }

        private static JsonNode? RemoveElementFromJson(IPhysicalFileSystem fileSystem, string targetJsonFile, string? propertyPath, string propertyPathSeparator, string propertyName, IPostAction action)
        {
            JsonNode? jsonContent = JsonNode.Parse(fileSystem.ReadAllText(targetJsonFile), nodeOptions: null, documentOptions: DeserializerOptions);

            if (jsonContent is null)
            {
                return null;
            }

            JsonNode? parentProperty = FindJsonNode(jsonContent, propertyPath, propertyPathSeparator);

            if (parentProperty is not null && parentProperty[propertyName] is not null)
            {
                parentProperty.AsObject().Remove(propertyName);
            }

            return jsonContent;
        }

        private static JsonNode? FindJsonNode(JsonNode content, string? nodePath, string pathSeparator)
        {
            if (nodePath is null)
            {
                return content;
            }

            string[] properties = nodePath.Split(pathSeparator);

            JsonNode? node = content;

            foreach (string property in properties)
            {
                if (node is null)
                {
                    return null;
                }

                node = node[property];
            }

            return node;
        }

        private static string[] FindFilesInCurrentFolderOrParentFolder(
            IPhysicalFileSystem fileSystem,
            string startPath,
            string matchPattern)
        {
            string? directory = fileSystem.DirectoryExists(startPath) ? startPath : Path.GetDirectoryName(startPath);

            if (directory is null)
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
            while (directory is not null && numberOfUpLevels <= 1);

            return Array.Empty<string>();
        }

        private class JsonContentParameters
        {
            private JsonContentParameters(string? parentProperty, string jsonPropertyName)
            {
                ParentProperty = parentProperty;
                JsonPropertyName = jsonPropertyName;
            }

            public string? ParentProperty { get; }

            public string JsonPropertyName { get; }

            /// <summary>
            /// Creates an instance of <see cref="JsonContentParameters"/> based on the configured arguments in the Post Action.
            /// </summary>
            /// <param name="action"></param>
            /// <returns>A <see cref="JsonContentParameters"/> instance, or null if no instance could be created.</returns>
            public static JsonContentParameters? CreateFromPostAction(IPostAction action)
            {
                action.Args.TryGetValue(ParentPropertyPathArgument, out string? parentProperty);
                action.Args.TryGetValue(JsonPropertyNameArgument, out string? jsonPropertyName);

                if (jsonPropertyName is null)
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, JsonPropertyNameArgument));
                    return null;
                }

                return new JsonContentParameters(parentProperty, jsonPropertyName);
            }
        }
    }
}
