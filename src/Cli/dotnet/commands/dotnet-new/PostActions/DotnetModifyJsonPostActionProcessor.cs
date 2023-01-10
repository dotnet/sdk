// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;

namespace Microsoft.DotNet.Tools.New.PostActionProcessors
{
    internal class DotnetModifyJsonPostActionProcessor : PostActionProcessorBase
    {
        public override Guid Id => ActionProcessorId;

        internal static Guid ActionProcessorId { get; } = new Guid("695A3659-EB40-4FF5-A6A6-C9C4E629FCB0");

        private static readonly JsonSerializerOptions s_serializerOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        private static readonly JsonDocumentOptions s_deserializerOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        private const string JsonFileNameArgument = "jsonFileName";
        private const string ParentPropertyPathArgument = "parentPropertyPath";
        private const string NewJsonPropertyNameArgument = "newJsonPropertyName";
        private const string NewJsonPropertyValueArgument = "newJsonPropertyValue";

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

            IReadOnlyList<string> jsonFiles = FindFilesAtOrAbovePath(environment.Host.FileSystem, outputBasePath, matchPattern: jsonFileName);

            if (jsonFiles.Count == 0)
            {
                Reporter.Error.WriteLine(LocalizableStrings.PostAction_ModifyJson_Error_NoJsonFile);
                return false;
            }

            if (jsonFiles.Count > 1)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_MultipleJsonFiles, jsonFileName));
                return false;
            }

            // args required:
            // parent property name in json file (if not specified, assume null and add the json section to the root)
            // optional property separation character
            // mandatory json text that must be added.
            action.Args.TryGetValue(ParentPropertyPathArgument, out string? parentProperty);
            action.Args.TryGetValue(NewJsonPropertyNameArgument, out string? newJsonPropertyName);
            action.Args.TryGetValue(NewJsonPropertyValueArgument, out string? newJsonPropertyValue);

            if (newJsonPropertyName == null)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, NewJsonPropertyNameArgument));
                return false;
            }

            if (newJsonPropertyValue == null)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, NewJsonPropertyValueArgument));
                return false;
            }

            JsonNode? newJsonContent = AddElementToJson(
                environment.Host.FileSystem,
                jsonFiles[0],
                parentProperty,
                ":",
                newJsonPropertyName,
                newJsonPropertyValue);

            if (newJsonContent == null)
            {
                return false;
            }

            environment.Host.FileSystem.WriteAllText(jsonFiles[0], newJsonContent.ToJsonString(s_serializerOptions));

            Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Succeeded, jsonFileName));

            return true;
        }

        private static JsonNode? AddElementToJson(IPhysicalFileSystem fileSystem, string targetJsonFile, string? propertyPath, string propertyPathSeparator, string newJsonPropertyName, string newJsonPropertyValue)
        {
            JsonNode? jsonContent = JsonNode.Parse(fileSystem.ReadAllText(targetJsonFile), nodeOptions: null, documentOptions: s_deserializerOptions);

            if (jsonContent == null)
            {
                Reporter.Error.WriteLine("json file is empty");
                return null;
            }

            JsonNode? parentProperty = FindJsonNode(jsonContent, propertyPath, propertyPathSeparator);

            if (parentProperty == null)
            {
                Reporter.Error.WriteLine(LocalizableStrings.PostAction_ModifyJson_Error_ParentPropertyPathInvalid);
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

        private static JsonNode? FindJsonNode(JsonNode content, string? nodePath, string pathSeparator)
        {
            if (nodePath == null)
            {
                return content;
            }

            string[] properties = nodePath.Split(pathSeparator);

            JsonNode? node = content;

            foreach (string property in properties)
            {
                Reporter.Output.WriteLine("Finding node " + property);

                if (node == null)
                {
                    Reporter.Error.WriteLine(property + " not found");
                    return null;
                }

                node = node[property];
            }

            return node;
        }

        private static IReadOnlyList<string> FindFilesAtOrAbovePath(IPhysicalFileSystem fileSystem, string startPath, string matchPattern, Func<string, bool>? secondaryFilter = null)
        {
            string? directory = fileSystem.DirectoryExists(startPath) ? startPath : Path.GetDirectoryName(startPath);

            if (directory == null)
            {
                throw new InvalidOperationException();
            }

            do
            {
                List<string> filesInDir = fileSystem.EnumerateFileSystemEntries(directory, matchPattern, SearchOption.AllDirectories).ToList();
                List<string> matches = new();

                matches = secondaryFilter == null ? filesInDir : filesInDir.Where(x => secondaryFilter(x)).ToList();

                if (matches.Count > 0)
                {
                    return matches;
                }

                directory = Path.GetPathRoot(directory) != directory ? Directory.GetParent(directory)?.FullName : null;
            }
            while (directory != null);

            return new List<string>();
        }
    }
}
