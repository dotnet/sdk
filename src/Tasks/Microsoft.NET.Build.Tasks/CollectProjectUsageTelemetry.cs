// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks;

/// <summary>
/// Logs a 'projectusage' telemetry event that describes the shape of the project being built.
/// The dotnet CLI logger aggregates these events locally per day and per project.
/// </summary>
[MSBuildMultiThreadableTask]
public sealed class CollectProjectUsageTelemetry : TaskBase
{
    public const string EventName = "projectusage";
    private const string NoExtension = "(none)";
    private const int FileHashBytes = 8;

    [Required] public string ProjectPath { get; set; } = string.Empty;

    public string? TargetFramework { get; set; }

    public string? RuntimeIdentifier { get; set; }

    public string? RuntimeIdentifiers { get; set; }

    public string? SdkVersion { get; set; }

    public ITaskItem[] References { get; set; } = Array.Empty<ITaskItem>();

    public ITaskItem[] Files { get; set; } = Array.Empty<ITaskItem>();

    protected override void ExecuteCore()
    {
        (BuildEngine as IBuildEngine5)?.LogTelemetry(EventName, CreateProperties());
    }

    internal IDictionary<string, string> CreateProperties()
    {
        string fullProjectPath = Path.GetFullPath(ProjectPath);
        string projectDirectory = Path.GetDirectoryName(fullProjectPath) ?? string.Empty;

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProjectId"] = Hash(fullProjectPath.ToUpperInvariant(), byteCount: null),
            ["ProjectType"] = Path.GetExtension(fullProjectPath).ToLowerInvariant(),
            ["TargetFramework"] = TargetFramework ?? string.Empty,
            ["RuntimeIdentifier"] = RuntimeIdentifier ?? string.Empty,
            ["RuntimeIdentifiers"] = JoinSorted(Split(RuntimeIdentifiers)),
            ["SdkVersion"] = SdkVersion ?? string.Empty,
            ["References"] = JoinSorted(GetReferenceNames()),
            ["Files"] = FormatFiles(projectDirectory),
        };
    }

    private IEnumerable<string> GetReferenceNames()
    {
        foreach (ITaskItem reference in References)
        {
            string name = Path.GetFileNameWithoutExtension(reference.ItemSpec);
            if (name.Length == 0)
            {
                continue;
            }

            bool isPublic = !string.IsNullOrEmpty(reference.GetMetadata("FrameworkReferenceName"))
                || !string.IsNullOrEmpty(reference.GetMetadata("NuGetPackageId"));
            yield return isPublic ? name : Hash(name.ToUpperInvariant(), byteCount: null);
        }
    }

    private string FormatFiles(string projectDirectory)
    {
        var hashesByExtension = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ITaskItem file in Files)
        {
            string fullPath = Path.GetFullPath(Path.Combine(projectDirectory, file.ItemSpec));
            if (!seenPaths.Add(fullPath))
            {
                continue;
            }

            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (extension.Length == 0)
            {
                extension = NoExtension;
            }

            if (!hashesByExtension.TryGetValue(extension, out SortedSet<string>? hashes))
            {
                hashes = new SortedSet<string>(StringComparer.Ordinal);
                hashesByExtension[extension] = hashes;
            }

            hashes.Add(Hash(NormalizeRelativePath(projectDirectory, fullPath), FileHashBytes));
        }

        return string.Join(";", hashesByExtension.Select(pair => pair.Key + "=" + string.Join(",", pair.Value)));
    }

    internal static string NormalizeRelativePath(string projectDirectory, string fullPath)
    {
        string prefix = projectDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? fullPath.Substring(prefix.Length)
            : fullPath;
        return path.Replace('\\', '/').ToUpperInvariant();
    }

    private static IEnumerable<string> Split(string? value) =>
        (value ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);

    private static string JoinSorted(IEnumerable<string> values) =>
        string.Join(";", new SortedSet<string>(values, StringComparer.Ordinal));

    internal static string Hash(string text, int? byteCount)
    {
        byte[] hash;
        using (SHA256 sha = SHA256.Create())
        {
            hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        }

        int length = byteCount ?? hash.Length;
        var builder = new StringBuilder(length * 2);
        for (int i = 0; i < length; i++)
        {
            builder.Append(hash[i].ToString("x2"));
        }

        return builder.ToString();
    }
}
