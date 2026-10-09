// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

#if !NET
#nullable enable
#endif

namespace Microsoft.DotNet.Tools.Dotnetup.BuildTasks;

public sealed class PrepareDotnetupCandidateAssetIds : Microsoft.Build.Utilities.Task
{
    private const string BuildElementName = "Build";
    private const string BlobElementName = "Blob";
    private const string PublishingVersionAttributeName = "PublishingVersion";
    private const string PublishingVersion = "4";
    private const string IdAttributeName = "Id";
    private const string PipelineArtifactNameAttributeName = "PipelineArtifactName";
    private const string PipelineArtifactPathAttributeName = "PipelineArtifactPath";
    private const string ArchiveArtifactName = "AssetManifests";
    private const string ArchiveBlobPrefix = "assets/manifests/";
    private const string MergedManifestFileName = "MergedManifest.xml";
    private const string BlobAssetIdManifestExtension = ".blobids";
    private const string DailyQuality = "daily";
    private const string PreviewQuality = "preview";
    private const string CandidateArtifactSuffix = "_Artifacts";
    private const string CandidateVersionLabel = "-candidate.";
    private const string NativeBinaryPrefix = "dotnetup-";
    private const string WindowsRidPrefix = "win-";
    private const string WindowsExecutableExtension = ".exe";
    private const string ChecksumExtension = ".sha512";
    private const string CandidateAssetIdPattern = @"^dotnetup/([0-9]+\.[0-9]+\.[0-9]+-{0}\.{1}[0-9]+\.[0-9]+)/([^/]+)$";

    private static readonly string[] s_qualities = { DailyQuality, PreviewQuality };
    private static readonly string[] s_runtimeIdentifiers =
    {
        "win-x64", "win-arm64", "linux-x64", "linux-arm64",
        "linux-musl-x64", "linux-musl-arm64", "osx-x64", "osx-arm64"
    };
    private static readonly string[] s_bootstrapScripts = { "get-dotnetup.ps1", "get-dotnetup.sh" };
    private static readonly HashSet<string> s_expectedFiles = CreateExpectedFiles();

    public string ManifestPath { get; set; } = "";

    public string OutputDirectory { get; set; } = "";

    public string ManifestsPath { get; set; } = "";

    public override bool Execute()
    {
        if (!string.IsNullOrEmpty(ManifestsPath))
        {
            ValidateInputs(ManifestsPath);
            return true;
        }
        if (string.IsNullOrEmpty(ManifestPath) || string.IsNullOrEmpty(OutputDirectory))
        {
            Log.LogError("ManifestPath and OutputDirectory are required when preparing candidate asset ID manifests.");
            return false;
        }
        Dictionary<string, string[]> candidates = GetCandidateAssetIds(XDocument.Load(ManifestPath));
        Directory.CreateDirectory(OutputDirectory);
        foreach (KeyValuePair<string, string[]> candidate in candidates)
        {
            File.WriteAllLines(Path.Combine(OutputDirectory, candidate.Key + BlobAssetIdManifestExtension), candidate.Value);
        }
        return true;
    }

    public static void ValidateInputs(string directory)
    {
        XElement build = new XElement(BuildElementName, new XAttribute(PublishingVersionAttributeName, PublishingVersion),
            new XElement(BlobElementName, new XAttribute(IdAttributeName, ArchiveBlobPrefix + "validation/" + MergedManifestFileName),
                new XAttribute(PipelineArtifactNameAttributeName, ArchiveArtifactName),
                new XAttribute(PipelineArtifactPathAttributeName, MergedManifestFileName)));
        foreach (string file in Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories))
        {
            XElement input = XDocument.Load(file).Root ?? throw new InvalidDataException("Empty candidate manifest: " + file);
            if (!IsV4Build(input))
            {
                throw new InvalidDataException("Candidate publishing requires a V4 Build manifest: " + file);
            }
            build.Add(input.Elements().Select(e => new XElement(e)));
        }
        GetCandidateAssetIds(new XDocument(build));
    }

    public static Dictionary<string, string[]> GetCandidateAssetIds(XDocument manifest)
    {
        XElement[] artifacts = GetBlobArtifacts(manifest);
        XElement archive = GetArchiveArtifact(artifacts);
        Dictionary<string, string[]> candidates = new Dictionary<string, string[]>(StringComparer.Ordinal);
        HashSet<XElement> classified = new HashSet<XElement> { archive };
        string? candidateVersion = null;
        foreach (string quality in s_qualities)
        {
            XElement[] selected = artifacts.Where(a => IsCandidateArtifact(a, quality)).ToArray();
            string version = ValidateCandidate(selected, quality);
            version = quality == PreviewQuality
                ? Regex.Replace(version, @"-preview\.[0-9]+\.", CandidateVersionLabel)
                : version.Replace("-" + quality + ".", CandidateVersionLabel);
            if (candidateVersion != null && candidateVersion != version)
            {
                throw new InvalidDataException("Daily and preview candidates must have matching release and build versions.");
            }
            candidateVersion = version;
            classified.UnionWith(selected);
            candidates.Add(quality, selected.Append(archive)
                .Select(a => (string)a.Attribute(IdAttributeName)!)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
        if (classified.Count != artifacts.Length)
        {
            throw new InvalidDataException("The build manifest contains assets outside the daily and preview candidates.");
        }
        return candidates;
    }

    private static bool IsV4Build(XElement build) =>
        build.Name == BuildElementName && (string?)build.Attribute(PublishingVersionAttributeName) == PublishingVersion;

    private static XElement[] GetBlobArtifacts(XDocument manifest)
    {
        XElement build = manifest.Root ?? throw new InvalidDataException("The build manifest is empty.");
        if (!IsV4Build(build))
        {
            throw new InvalidDataException("Candidate publishing requires a V4 Build manifest.");
        }
        XElement[] artifacts = build.Elements().ToArray();
        if (artifacts.Any(a => a.Name != BlobElementName))
        {
            throw new InvalidDataException("The dotnetup candidate manifest must contain only blob artifacts.");
        }
        return artifacts;
    }

    private static XElement GetArchiveArtifact(XElement[] artifacts)
    {
        XElement[] archives = artifacts.Where(a =>
            (string?)a.Attribute(PipelineArtifactNameAttributeName) == ArchiveArtifactName &&
            (string?)a.Attribute(PipelineArtifactPathAttributeName) == MergedManifestFileName &&
            ((string?)a.Attribute(IdAttributeName) ?? "").StartsWith(ArchiveBlobPrefix, StringComparison.Ordinal)).ToArray();
        if (archives.Length != 1)
        {
            throw new InvalidDataException("Expected exactly one complete BAR archive manifest.");
        }
        return archives[0];
    }

    private static bool IsCandidateArtifact(XElement artifact, string quality) =>
        ((string?)artifact.Attribute(PipelineArtifactNameAttributeName) ?? "").EndsWith("_" + quality + CandidateArtifactSuffix, StringComparison.Ordinal);

    private static string ValidateCandidate(XElement[] artifacts, string quality)
    {
        string pattern = string.Format(System.Globalization.CultureInfo.InvariantCulture, CandidateAssetIdPattern,
            quality, quality == PreviewQuality ? @"[0-9]+\." : "");
        HashSet<string> files = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> versions = new HashSet<string>(StringComparer.Ordinal);
        foreach (XElement artifact in artifacts)
        {
            string id = (string?)artifact.Attribute(IdAttributeName) ?? "";
            Match match = Regex.Match(id, pattern);
            if (!match.Success || string.IsNullOrEmpty((string?)artifact.Attribute(PipelineArtifactPathAttributeName)))
            {
                throw new InvalidDataException("Invalid " + quality + " candidate asset: " + id);
            }
            versions.Add(match.Groups[1].Value);
            if (!files.Add(match.Groups[2].Value))
            {
                throw new InvalidDataException("Duplicate candidate asset: " + id);
            }
        }
        if (versions.Count != 1 || !files.SetEquals(s_expectedFiles))
        {
            throw new InvalidDataException("The " + quality + " candidate must contain one version, all eight RID binaries and checksums, and both bootstrap scripts and checksums.");
        }
        return versions.Single();
    }

    private static HashSet<string> CreateExpectedFiles()
    {
        HashSet<string> expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (string rid in s_runtimeIdentifiers)
        {
            string file = NativeBinaryPrefix + rid + (rid.StartsWith(WindowsRidPrefix, StringComparison.Ordinal) ? WindowsExecutableExtension : "");
            expected.Add(file);
            expected.Add(file + ChecksumExtension);
        }
        foreach (string script in s_bootstrapScripts)
        {
            expected.Add(script);
            expected.Add(script + ChecksumExtension);
        }
        return expected;
    }
}
