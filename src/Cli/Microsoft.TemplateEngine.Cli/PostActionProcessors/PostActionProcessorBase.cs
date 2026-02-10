// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    public abstract class PostActionProcessorBase : IPostActionProcessor
    {
        public abstract Guid Id { get; }

        public bool Process(
            IEngineEnvironmentSettings environment,
            IPostAction action,
            ICreationEffects creationEffects,
            ICreationResult templateCreationResult,
            string outputBasePath)
        {
            if (string.IsNullOrWhiteSpace(outputBasePath))
            {
                throw new ArgumentException($"'{nameof(outputBasePath)}' cannot be null or whitespace.", nameof(outputBasePath));
            }
            outputBasePath = Path.GetFullPath(outputBasePath);
            return ProcessInternal(environment, action, creationEffects, templateCreationResult, outputBasePath);
        }

        /// <summary>
        /// Gets absolute normalized path for a target matching <paramref name="sourcePathGlob"/>.
        /// </summary>
        protected static IReadOnlyList<string> GetTargetForSource(ICreationEffects2 creationEffects, string sourcePathGlob, string outputBasePath)
        {
            Glob g = Glob.Parse(sourcePathGlob);
            List<string> results = new();

            if (creationEffects.FileChanges != null)
            {
                foreach (IFileChange2 change in creationEffects.FileChanges)
                {
                    if (g.IsMatch(change.SourceRelativePath))
                    {
                        results.Add(Path.GetFullPath(change.TargetRelativePath, outputBasePath));
                    }
                }
            }
            return results;
        }

        protected static IReadOnlyList<string> GetConfiguredFiles(
            IReadOnlyDictionary<string, string> postActionArgs,
            ICreationEffects creationEffects,
            string argName,
            string outputBasePath,
            Func<string, bool>? matchCriteria = null)
        {
            if (creationEffects is not ICreationEffects2 creationEffects2)
            {
                return new List<string>();
            }
            if (!postActionArgs.TryGetValue(argName, out string? targetFiles))
            {
                return new List<string>();
            }
            if (string.IsNullOrWhiteSpace(targetFiles))
            {
                return new List<string>();
            }

            if (TryParseAsJson(targetFiles, out IReadOnlyList<string> paths))
            {
                return ProcessPaths(paths);
            }

            return ProcessPaths(targetFiles.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

            IReadOnlyList<string> ProcessPaths(IReadOnlyList<string> paths)
            {
                matchCriteria ??= p => true;
                return paths
                    .SelectMany(t => GetTargetForSource(creationEffects2, t, outputBasePath))
                    .Where(t => matchCriteria(t))
                    .ToArray();
            }
        }

        protected static IReadOnlyList<string>? GetTargetFilesPaths(
            IReadOnlyDictionary<string, string> postActionArgs,
            string outputBasePath)
        {
            postActionArgs.TryGetValue("targetFiles", out string? targetFiles);
            if (string.IsNullOrWhiteSpace(targetFiles))
            {
                return null;
            }

            // try to parse the argument as json; if it is not valid json, use it as a string
            if (TryParseAsJson(targetFiles, out IReadOnlyList<string> paths))
            {
                return GetFullPaths(paths);
            }

            return GetFullPaths(targetFiles.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

            IReadOnlyList<string> GetFullPaths(IEnumerable<string> paths)
            {
                var fullPaths = paths
                    .Select(p => Path.GetFullPath(p, outputBasePath))
                    .ToList();

                return fullPaths.AsReadOnly();
            }
        }

        protected abstract bool ProcessInternal(
            IEngineEnvironmentSettings environment,
            IPostAction action,
            ICreationEffects creationEffects,
            ICreationResult templateCreationResult,
            string outputBasePath);

        private static bool TryParseAsJson(string targetFiles, out IReadOnlyList<string> paths)
        {
            paths = new List<string>();
            targetFiles.TryParse(out JToken? config);
            if (config is null)
            {
                return false;
            }

            if (config.Type == JTokenType.String)
            {
                paths = config.ToString().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                return true;
            }

            if (config is not JArray arr)
            {
                return false;
            }

            var parts = arr
                .Where(token => token.Type == JTokenType.String)
                .Select(token => token.ToString()).ToList();

            if (parts.Count == 0)
            {
                return false;
            }

            paths = parts.AsReadOnly();
            return true;
        }
    }
}
