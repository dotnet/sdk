// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// Computes the results directory handed to each test application.
/// The per-module layout mirrors the SDK artifacts output layout
/// (https://learn.microsoft.com/dotnet/core/sdk/artifacts-output): a project folder containing a
/// pivot folder, where pivot elements are joined by an underscore.
/// <para>
/// Project names are not guaranteed to be unique within a run, so the whole module set is inspected
/// up front. Only when two distinct projects would land in the same project folder is a short
/// identity hash appended to disambiguate them, keeping the common case clean.
/// </para>
/// </summary>
internal sealed class TestResultsDirectoryResolver
{
    private const string DefaultResultsDirectoryName = "TestResults";
    private const string UnknownComponent = "unknown";
    private const int MaxPathComponentLength = 255;

    private static readonly HashSet<char> s_invalidPathComponentCharacters =
        [.. Path.GetInvalidFileNameChars(), Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    private readonly PathOptions _pathOptions;
    private readonly string _workingDirectory;
    private readonly string _identityRoot;
    private readonly HashSet<string> _ambiguousProjectNames;

    private TestResultsDirectoryResolver(PathOptions pathOptions, string workingDirectory, string identityRoot, HashSet<string> ambiguousProjectNames)
    {
        _pathOptions = pathOptions;
        _workingDirectory = workingDirectory;
        _identityRoot = identityRoot;
        _ambiguousProjectNames = ambiguousProjectNames;
    }

    public static TestResultsDirectoryResolver Create(PathOptions pathOptions, IEnumerable<TestModule> modules, string workingDirectory)
    {
        if (pathOptions.ResultsDirectoryLayout == ResultsDirectoryLayout.Flat)
        {
            return new TestResultsDirectoryResolver(pathOptions, workingDirectory, workingDirectory, []);
        }
        // Anchor identities to the directory shared by every module rather than the current
        // directory, so the same solution produces the same folder names no matter where
        // 'dotnet test' was invoked from.
        List<TestModule> materializedModules = [.. modules];
        string identityRoot = GetCommonRootDirectory(materializedModules, workingDirectory);

        Dictionary<string, HashSet<string>> identitiesByProjectName = new(StringComparer.OrdinalIgnoreCase);
        foreach (TestModule module in materializedModules)
        {
            string projectName = GetProjectName(module);
            if (!identitiesByProjectName.TryGetValue(projectName, out HashSet<string>? identities))
            {
                identities = new HashSet<string>(StringComparer.Ordinal);
                identitiesByProjectName.Add(projectName, identities);
            }

            identities.Add(GetProjectIdentity(module, identityRoot));
        }

        HashSet<string> ambiguousProjectNames = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string projectName, HashSet<string> identities) in identitiesByProjectName)
        {
            if (identities.Count > 1)
            {
                ambiguousProjectNames.Add(projectName);
            }
        }

        return new TestResultsDirectoryResolver(pathOptions, workingDirectory, identityRoot, ambiguousProjectNames);
    }

    public string? Resolve(TestModule module)
    {
        if (_pathOptions.ResultsDirectoryLayout == ResultsDirectoryLayout.Flat)
        {
            return _pathOptions.ResultsDirectoryPath;
        }

        string resultsDirectory = _pathOptions.ResultsDirectoryPath
            ?? Path.Combine(_workingDirectory, DefaultResultsDirectoryName);

        string resolved = Path.GetFullPath(
            Path.Combine(resultsDirectory, GetProjectDirectoryName(module), GetPivotDirectoryName(module)));

        // Sanitization strips separators and dot-only components, so a module can never steer its
        // results out of the requested root. Asserted rather than thrown because it is unreachable
        // by design and only a future change to the component rules could break it.
        Debug.Assert(IsUnderRoot(resolved, resultsDirectory), $"'{resolved}' escaped the results directory '{resultsDirectory}'.");

        return resolved;
    }

    private static bool IsUnderRoot(string candidate, string root)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string relative = Path.GetRelativePath(normalizedRoot, candidate);

        return relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    /// <summary>
    /// The deepest directory that contains every module, used as a stable anchor for identities.
    /// Falls back to the working directory when the modules share nothing (for example, modules on
    /// different drives).
    /// </summary>
    private static string GetCommonRootDirectory(List<TestModule> modules, string workingDirectory)
    {
        string? commonRoot = null;
        foreach (TestModule module in modules)
        {
            string? moduleDirectory = Path.GetDirectoryName(GetProjectPath(module, workingDirectory));
            if (string.IsNullOrEmpty(moduleDirectory))
            {
                continue;
            }

            commonRoot = commonRoot is null ? moduleDirectory : GetCommonPrefixDirectory(commonRoot, moduleDirectory);
            if (string.IsNullOrEmpty(commonRoot))
            {
                return workingDirectory;
            }
        }

        return string.IsNullOrEmpty(commonRoot) ? workingDirectory : commonRoot;
    }

    private static string GetCommonPrefixDirectory(string first, string second)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        // Keep the filesystem root ('C:\', '/', '\\server\share\') attached. Joining bare segments
        // would turn 'C:\foo' and 'C:\bar' into the drive-relative 'C:', whose meaning depends on
        // the process working directory.
        string firstRoot = Path.GetPathRoot(first) ?? string.Empty;
        string secondRoot = Path.GetPathRoot(second) ?? string.Empty;
        if (firstRoot.Length == 0 || !string.Equals(firstRoot, secondRoot, comparison))
        {
            return string.Empty;
        }

        char[] separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];
        string[] firstSegments = first[firstRoot.Length..].Split(separators, StringSplitOptions.RemoveEmptyEntries);
        string[] secondSegments = second[secondRoot.Length..].Split(separators, StringSplitOptions.RemoveEmptyEntries);

        int shared = 0;
        while (shared < firstSegments.Length
            && shared < secondSegments.Length
            && string.Equals(firstSegments[shared], secondSegments[shared], comparison))
        {
            shared++;
        }

        return Path.Combine(firstRoot, string.Join(Path.DirectorySeparatorChar, firstSegments, 0, shared));
    }

    /// <summary>
    /// The project folder, defaulting to the project file name and falling back to the assembly name
    /// when the module was discovered through <c>--test-modules</c> instead of a project. A short hash
    /// is appended only when another distinct project in the same run shares the name.
    /// </summary>
    private string GetProjectDirectoryName(TestModule module)
    {
        string projectName = GetProjectName(module);

        return LimitComponentLength(_ambiguousProjectNames.Contains(projectName)
            ? $"{projectName}_{GetShortHash(GetProjectIdentity(module, _identityRoot))}"
            : projectName);
    }

    /// <summary>
    /// The pivot folder distinguishing runs of the same project across target frameworks and
    /// runtimes. Multiple elements are joined by an underscore, following the artifacts layout.
    /// The configuration is deliberately not part of the pivot: a single test run targets one
    /// configuration, so it would only ever add a constant level to every path.
    /// </summary>
    private static string GetPivotDirectoryName(TestModule module)
    {
        string targetFramework = SanitizePathComponent(module.TargetFramework);
        string runtime = SanitizePathComponent(GetRuntimeComponent(module));

        return LimitComponentLength($"{targetFramework}_{runtime}".ToLowerInvariant());
    }

    /// <summary>
    /// Prefers the runtime identifier the module was actually built for, so that runs differing
    /// only by RID stay separate, and falls back to the architecture for the common case where no
    /// runtime identifier was requested.
    /// </summary>
    private static string GetRuntimeComponent(TestModule module)
    {
        if (!string.IsNullOrEmpty(module.RunProperties.RuntimeIdentifier))
        {
            return module.RunProperties.RuntimeIdentifier;
        }

        return GetTargetArchitecture(module).ToString();
    }

    private static string GetProjectName(TestModule module)
    {
        string? projectName = string.IsNullOrEmpty(module.ProjectFullPath)
            ? Path.GetFileNameWithoutExtension(module.TargetPath)
            : Path.GetFileNameWithoutExtension(module.ProjectFullPath);

        return SanitizePathComponent(projectName);
    }

    /// <summary>
    /// Identifies the project a module belongs to. Modules of a multi-targeted project share an
    /// identity so they nest under a single project folder and are separated only by their pivot.
    /// </summary>
    private static string GetProjectIdentity(TestModule module, string identityRoot)
    {
        string path = GetProjectPath(module, identityRoot);
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        string relativePath = Path.GetRelativePath(identityRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/');

        return OperatingSystem.IsWindows() ? relativePath.ToLowerInvariant() : relativePath;
    }

    private static string GetProjectPath(TestModule module, string basePath)
    {
        string path = string.IsNullOrEmpty(module.ProjectFullPath) ? module.TargetPath : module.ProjectFullPath;

        return string.IsNullOrEmpty(path) ? string.Empty : Path.GetFullPath(path, basePath);
    }

    private static string GetShortHash(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }

    private static Architecture GetTargetArchitecture(TestModule module)
    {
        if (EnvironmentVariableNames.TryParseArchitecture(module.RunProperties.RuntimeIdentifier, out Architecture architecture)
            || EnvironmentVariableNames.TryParseArchitecture(module.RunProperties.DefaultAppHostRuntimeIdentifier, out architecture))
        {
            return architecture;
        }

        return RuntimeInformation.ProcessArchitecture;
    }

    private static string SanitizePathComponent(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return UnknownComponent;
        }

        StringBuilder builder = new(value.Length);
        foreach (char character in value)
        {
            builder.Append(s_invalidPathComponentCharacters.Contains(character) ? '_' : character);
        }

        string sanitized = builder.ToString();

        // A project named '...csproj' yields '..', which would otherwise walk out of the results
        // directory. Trailing dots and spaces are also not addressable on Windows.
        string trimmed = sanitized.TrimEnd('.', ' ');

        return trimmed.Length == 0 ? UnknownComponent : trimmed;
    }

    /// <summary>
    /// Keeps a single directory component within the limit common to Windows and Linux
    /// filesystems, so that a long project name (or a long name plus its disambiguating suffix)
    /// cannot make the test application fail to create its results directory.
    /// </summary>
    private static string LimitComponentLength(string component)
    {
        if (component.Length <= MaxPathComponentLength)
        {
            return component;
        }

        // The appended hash is computed over the full component, so truncated names stay unique.
        string hash = GetShortHash(component);
        return string.Concat(component.AsSpan(0, MaxPathComponentLength - hash.Length - 1), "_", hash);
    }
}
