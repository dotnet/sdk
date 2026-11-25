// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Build.Execution;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class BrowserToolsSettings(string path)
{
    public const string FileName = "hot-reload-settings.json";

    private static readonly byte[] s_enabled = Encoding.UTF8.GetBytes("{ \"hotReload\": true }" + Environment.NewLine);
    private static readonly byte[] s_disabled = Encoding.UTF8.GetBytes("{ \"hotReload\": false }" + Environment.NewLine);

    public static BrowserToolsSettings? FromProject(ProjectInstance project, ILogger logger)
    {
        if (SharedSecretBuildOutputs.FromProject(project, logger) == null)
        {
            return null;
        }

        return new BrowserToolsSettings(Path.Combine(
            project.GetIntermediateOutputDirectory()!,
            SharedSecretBuildOutputs.DirectoryName,
            FileName));
    }

    public void Enable()
        => WriteIfDifferent(s_enabled);

    public void Disable()
        => WriteIfDifferent(s_disabled);

    private void WriteIfDifferent(byte[] contents)
    {
        if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(contents))
        {
            File.WriteAllBytes(path, contents);
        }
    }
}
