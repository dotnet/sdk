// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using System.Text.Json.Nodes;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class ChmodPostActionProcessor : PostActionProcessorBase
    {
        private static readonly Guid ActionProcessorId = new("cb9a6cf3-4f5c-4860-b9d2-03a574959774");

        public override Guid Id => ActionProcessorId;

        protected override bool ProcessInternal(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            bool allSucceeded = true;
            foreach (KeyValuePair<string, string> entry in actionConfig.Args)
            {
                string[] values;
                try
                {
                    JsonArray valueArray = JsonNode.Parse(entry.Value)!.AsArray();
                    values = new string[valueArray.Count];

                    for (int i = 0; i < valueArray.Count; ++i)
                    {
                        values[i] = valueArray[i]?.GetValue<string>() ?? "";
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
                        // Template arguments may contain shell globs (for example, "*.sh").
                        ProcessExitStatus status = System.Diagnostics.Process.Run(new ProcessStartInfo("/bin/sh", ["-c", $"chmod {entry.Key} {file}"])
                        {
                            InheritedHandles = [],
                            WorkingDirectory = outputBasePath,
                        });

                        if (status.ExitCode != 0)
                        {
                            Reporter.Error.WriteLine(LocalizableStrings.UnableToSetPermissions, entry.Key, file);
                            allSucceeded = false;
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
    }
}
