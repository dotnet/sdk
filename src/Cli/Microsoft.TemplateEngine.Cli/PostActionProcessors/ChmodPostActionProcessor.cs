// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class ChmodPostActionProcessor : PostActionProcessorBase
    {
        private static readonly Guid ActionProcessorId = new("cb9a6cf3-4f5c-4860-b9d2-03a574959774");

        public override Guid Id => ActionProcessorId;

        protected override bool ProcessInternal(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            if (OperatingSystem.IsWindows())
            {
                // Chmod is Unix-specific
                return true;
            }

            bool allSucceeded = true;
            foreach (KeyValuePair<string, string> entry in actionConfig.Args)
            {
                string[] values;
                try
                {
                    JArray valueArray = JArray.Parse(entry.Value);
                    values = new string[valueArray.Count];

                    for (int i = 0; i < valueArray.Count; ++i)
                    {
                        values[i] = valueArray[i].ToString();
                    }
                }
                catch
                {
                    values = new[] { entry.Value };
                }

                foreach (string file in values)
                {
                    try
                    {
                        foreach (string filePath in ResolveFiles(outputBasePath, file))
                        {
                            File.SetUnixFileMode(filePath, ChmodHelper.GetArguments(entry.Key.AsSpan(), filePath));
                        }
                    }
                    catch (Exception ex)
                    {
                        Reporter.Error.WriteLine(LocalizableStrings.UnableToSetPermissions, entry.Key, file);
                        Reporter.Verbose.WriteLine(LocalizableStrings.Generic_Details, ex.ToString());
                        allSucceeded = false;
                    }
                }
            }

            return allSucceeded;
        }

        private static IEnumerable<string> ResolveFiles(string outputBasePath, string file)
        {
            string candidatePath = Path.IsPathRooted(file)
                ? file
                : Path.Combine(outputBasePath, file);

            if (!ContainsWildcard(file))
            {
                return [candidatePath];
            }

            string? directory = Path.GetDirectoryName(candidatePath);
            string searchPattern = Path.GetFileName(candidatePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(searchPattern) || !Directory.Exists(directory))
            {
                return [candidatePath];
            }

            string[] resolved = Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly).ToArray();
            return resolved.Length == 0 ? [candidatePath] : resolved;
        }

        private static bool ContainsWildcard(string value) => value.IndexOfAny(['*', '?']) >= 0;
    }
}
