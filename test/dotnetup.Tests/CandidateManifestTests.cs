// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Dotnetup.BuildTasks;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class CandidateManifestTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dotnetup-candidate-manifest-tests", Guid.NewGuid().ToString("N"));

    public CandidateManifestTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void ValidatesCandidateInputsBeforeRegistration()
    {
        XDocument manifest = CreateManifest();
        manifest.Root!.Element("Blob")!.Remove();
        string file = Path.Combine(_directory, "Candidates.xml");
        manifest.Save(file);

        PrepareDotnetupCandidateAssetIds.ValidateInputs(_directory);

        Assert.IsTrue(XNode.DeepEquals(manifest, XDocument.Load(file)));
    }

    [TestMethod]
    public void RejectsEmptyCandidateInputs()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => PrepareDotnetupCandidateAssetIds.ValidateInputs(_directory));
    }

    [TestMethod]
    public void WritesBothCandidateAssetIdManifests()
    {
        string file = Path.Combine(_directory, "MergedManifest.xml");
        CreateManifest().Save(file);
        PrepareDotnetupCandidateAssetIds task = new()
        {
            ManifestPath = file,
            OutputDirectory = Path.Combine(_directory, "output")
        };

        Assert.IsTrue(task.Execute());

        foreach (string quality in new[] { "daily", "preview" })
        {
            string[] output = File.ReadAllLines(Path.Combine(task.OutputDirectory, quality + ".blobids"));
            Assert.AreSequenceEqual(PrepareDotnetupCandidateAssetIds.GetCandidateAssetIds(XDocument.Load(file))[quality], output);
        }
        Assert.HasCount(2, Directory.GetFiles(task.OutputDirectory));
        Assert.HasCount(41, XDocument.Load(file).Root!.Elements());
    }

    [TestMethod]
    public void InvalidCandidatesDoNotWriteAllowlists()
    {
        string file = Path.Combine(_directory, "MergedManifest.xml");
        XDocument manifest = CreateManifest();
        manifest.Root!.Elements("Blob").Last().Remove();
        manifest.Save(file);
        PrepareDotnetupCandidateAssetIds task = new()
        {
            ManifestPath = file,
            OutputDirectory = Path.Combine(_directory, "output")
        };

        Assert.ThrowsExactly<InvalidDataException>(() => task.Execute());
        Assert.IsFalse(Directory.Exists(task.OutputDirectory));
    }

    [TestMethod]
    public void SelectsCandidateIdsWithoutChangingSourceManifest()
    {
        XDocument manifest = CreateManifest();
        string original = manifest.ToString();
        Dictionary<string, string[]> candidates = PrepareDotnetupCandidateAssetIds.GetCandidateAssetIds(manifest);

        Assert.AreEqual(original, manifest.ToString());
        Assert.HasCount(2, candidates);
        foreach (string quality in new[] { "daily", "preview" })
        {
            string[] ids = candidates[quality];
            Assert.HasCount(21, ids);
            Assert.HasCount(20, ids.Where(id => id.StartsWith("dotnetup/", StringComparison.Ordinal)).ToArray());
            Assert.IsTrue(ids.Where(id => id.StartsWith("dotnetup/", StringComparison.Ordinal))
                .All(id => id.Contains("-" + quality + ".", StringComparison.Ordinal)));
            Assert.Contains((string)manifest.Root!.Element("Blob")!.Attribute("Id")!, ids);
            Assert.AreSequenceEqual(ids.OrderBy(id => id, StringComparer.Ordinal), ids);
        }
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("2")]
    [DataRow("10")]
    public void MatchesCandidatesIndependentlyOfPreviewIteration(string iteration)
    {
        XDocument manifest = CreateManifest();
        foreach (XElement preview in manifest.Root!.Elements("Blob").Where(b => ((string)b.Attribute("Id")!).Contains("-preview.", StringComparison.Ordinal)))
        {
            preview.SetAttributeValue("Id", ((string)preview.Attribute("Id")!).Replace("-preview.1.", "-preview." + iteration + ".", StringComparison.Ordinal));
        }

        Dictionary<string, string[]> candidates = PrepareDotnetupCandidateAssetIds.GetCandidateAssetIds(manifest);

        Assert.Contains("dotnetup/0.2.0-daily.26501.1/dotnetup-win-x64.exe", candidates["daily"]);
        Assert.Contains("dotnetup/0.2.0-preview." + iteration + ".26501.1/dotnetup-win-x64.exe", candidates["preview"]);
    }

    [TestMethod]
    [DataRow("missing-rid")]
    [DataRow("missing-checksum")]
    [DataRow("missing-script")]
    [DataRow("duplicate")]
    [DataRow("wrong-version")]
    [DataRow("mixed-version")]
    [DataRow("mismatched-candidates")]
    [DataRow("missing-artifact-path")]
    [DataRow("wrong-artifact")]
    [DataRow("unknown-asset")]
    [DataRow("missing-archive")]
    [DataRow("v3")]
    [DataRow("daily-iteration")]
    [DataRow("preview-missing-iteration")]
    public void RejectsIncompleteOrMisroutedCandidates(string fault)
    {
        XDocument manifest = CreateManifest();
        XElement blob = manifest.Root!.Elements("Blob").First(b => ((string)b.Attribute("Id")!).Contains("-daily.", StringComparison.Ordinal));
        switch (fault)
        {
            case "missing-rid":
                blob.Remove();
                break;
            case "missing-checksum":
                blob.ElementsAfterSelf("Blob").First().Remove();
                break;
            case "missing-script":
                manifest.Root.Elements("Blob").First(b => ((string)b.Attribute("Id")!).EndsWith("/get-dotnetup.sh", StringComparison.Ordinal)).Remove();
                break;
            case "duplicate":
                manifest.Root.Add(new XElement(blob));
                break;
            case "wrong-version":
                blob.SetAttributeValue("Id", ((string)blob.Attribute("Id")!).Replace("-daily.", "-preview.", StringComparison.Ordinal));
                break;
            case "wrong-artifact":
                blob.SetAttributeValue("PipelineArtifactName", "Build_preview_Artifacts");
                break;
            case "mixed-version":
                blob.SetAttributeValue("Id", ((string)blob.Attribute("Id")!).Replace("26501.1", "26501.2", StringComparison.Ordinal));
                break;
            case "mismatched-candidates":
                foreach (XElement preview in manifest.Root.Elements("Blob").Where(b => ((string)b.Attribute("Id")!).Contains("-preview.", StringComparison.Ordinal)))
                {
                    preview.SetAttributeValue("Id", ((string)preview.Attribute("Id")!).Replace("26501.1", "26501.2", StringComparison.Ordinal));
                }
                break;
            case "missing-artifact-path":
                blob.Attribute("PipelineArtifactPath")!.Remove();
                break;
            case "unknown-asset":
                manifest.Root.Add(new XElement("Package", new XAttribute("Id", "Unexpected")));
                break;
            case "missing-archive":
                manifest.Root.Element("Blob")!.Remove();
                break;
            case "v3":
                manifest.Root.SetAttributeValue("PublishingVersion", "3");
                break;
            case "daily-iteration":
                foreach (XElement daily in manifest.Root.Elements("Blob").Where(b => ((string)b.Attribute("Id")!).Contains("-daily.", StringComparison.Ordinal)))
                {
                    daily.SetAttributeValue("Id", ((string)daily.Attribute("Id")!).Replace("-daily.", "-daily.1.", StringComparison.Ordinal));
                }
                break;
            case "preview-missing-iteration":
                foreach (XElement preview in manifest.Root.Elements("Blob").Where(b => ((string)b.Attribute("Id")!).Contains("-preview.", StringComparison.Ordinal)))
                {
                    preview.SetAttributeValue("Id", ((string)preview.Attribute("Id")!).Replace("-preview.1.", "-preview.", StringComparison.Ordinal));
                }
                break;
        }
        Assert.ThrowsExactly<InvalidDataException>(() => PrepareDotnetupCandidateAssetIds.GetCandidateAssetIds(manifest));
    }

    private static XDocument CreateManifest()
    {
        XElement build = new("Build",
            new XAttribute("PublishingVersion", "4"),
            new XAttribute("AzureDevOpsBuildId", "12345"),
            new XElement("Blob",
                new XAttribute("Id", "assets/manifests/sdk/20261001.1/MergedManifest.xml"),
                new XAttribute("PipelineArtifactName", "AssetManifests"),
                new XAttribute("PipelineArtifactPath", "MergedManifest.xml")));
        foreach (string quality in new[] { "daily", "preview" })
        {
            List<string> files = [];
            foreach (string rid in new[] { "win-x64", "win-arm64", "linux-x64", "linux-arm64", "linux-musl-x64", "linux-musl-arm64", "osx-x64", "osx-arm64" })
            {
                files.Add("dotnetup-" + rid + (rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : ""));
            }
            files.AddRange(["get-dotnetup.ps1", "get-dotnetup.sh"]);
            foreach (string file in files)
            {
                foreach (string name in new[] { file, file + ".sha512" })
                {
                    build.Add(new XElement("Blob",
                        new XAttribute("Id", "dotnetup/0.2.0-" + quality + "." + (quality == "preview" ? "1." : "") + "26501.1/" + name),
                        new XAttribute("PipelineArtifactName", "Build_" + quality + "_Artifacts"),
                        new XAttribute("PipelineArtifactPath", "assets/" + name)));
                }
            }
        }
        return new XDocument(build);
    }
}
