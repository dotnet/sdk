// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.FileBasedPrograms;

namespace Microsoft.DotNet.Cli.Commands.Package;

/// <summary>
/// Utility for reflecting changes in a <see cref="ProjectRootElement"/> (modified by NuGet)
/// back to <c>#:package</c> directives in a C# source file.
/// </summary>
internal static class VirtualProjectPackageReflector
{
    /// <summary>
    /// Reads <c>PackageReference</c> items from the given <paramref name="projectRootElement"/> and
    /// updates the <c>#:package</c> directives in the source file at <paramref name="entryPointFilePath"/> to match.
    /// </summary>
    internal static void ReflectChangesToDirectives(ProjectRootElement projectRootElement, string entryPointFilePath)
    {
        // Collect PackageReference items from the modified virtual project.
        var packageReferences = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var itemGroup in projectRootElement.ItemGroups)
        {
            foreach (var item in itemGroup.Items)
            {
                if (string.Equals(item.ItemType, "PackageReference", StringComparison.OrdinalIgnoreCase))
                {
                    var version = item.Metadata.FirstOrDefault(
                        m => string.Equals(m.Name, "Version", StringComparison.OrdinalIgnoreCase))?.Value;
                    packageReferences[item.Include] = version;
                }
            }
        }

        // Load the source file and its current directives.
        var editor = FileBasedAppSourceEditor.Load(SourceFile.Load(entryPointFilePath));
        var directives = editor.Directives;

        // Remove directives for packages that are no longer in the project.
        // Process in reverse order to avoid invalidating spans.
        for (int i = directives.Length - 1; i >= 0; i--)
        {
            if (directives[i] is CSharpDirective.Package p)
            {
                if (!packageReferences.ContainsKey(p.Name))
                {
                    editor.Remove(directives[i]);
                }
            }
        }

        // Add or update directives for packages in the project.
        foreach (var (name, version) in packageReferences)
        {
            // Always update existing directives (version might have changed) and add new ones.
            editor.Add(new CSharpDirective.Package(default) { Name = name, Version = version });
        }

        editor.SourceFile.Save();
    }
}
