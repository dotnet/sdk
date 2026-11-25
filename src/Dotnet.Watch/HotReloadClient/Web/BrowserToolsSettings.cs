// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// Updates the build-owned browser tools settings for a Hot Reload session.
/// The browser-facing project's evaluated intermediate output selects the file for both
/// dotnet watch and Visual Studio.
/// </summary>
internal sealed class BrowserToolsSettings(string path)
{
    public const string FileName = "hot-reload-settings.json";

    private static readonly byte[] s_enabled = Encoding.UTF8.GetBytes("{ \"hotReload\": true }" + Environment.NewLine);
    private static readonly byte[] s_disabled = Encoding.UTF8.GetBytes("{ \"hotReload\": false }" + Environment.NewLine);

    public static BrowserToolsSettings FromProjectSettings(string intermediateOutputDirectory)
        => new(Path.Combine(intermediateOutputDirectory, SharedSecretBuildOutputs.DirectoryName, FileName));

    public void Enable()
        => WriteIfDifferent(s_enabled);

    public void Disable()
        => WriteIfDifferent(s_disabled);

    private void WriteIfDifferent(byte[] contents)
    {
        if (!File.ReadAllBytes(path).SequenceEqual(contents))
        {
            File.WriteAllBytes(path, contents);
        }
    }
}
