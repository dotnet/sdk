// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.DotNet.Dotnetup.Release;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public sealed class ReleasePlannerTests
{
    private const string BuildId = "12345";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private static readonly string[] s_rids = ["linux-arm64", "linux-musl-arm64", "linux-musl-x64", "linux-x64", "osx-arm64", "osx-x64", "win-arm64", "win-x64"];
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnetup-release-tests-" + Guid.NewGuid().ToString("N"));

    public TestContext TestContext { get; set; } = null!;

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("preview", "0.3.0-preview.1.26530.1", "preview", false)]
    [DataRow("ga", "0.3.0", "stable", true)]
    public async Task PlansBothOriginsAndConditionalAkaOperationsWithoutRebranding(string quality, string version, string channel, bool placeholder)
    {
        WriteCandidates(quality, version);
        ReleasePlan plan = await CreatePlanAsync(quality).ConfigureAwait(false);

        Assert.IsTrue(plan.DryRun);
        Assert.AreEqual(version, plan.Version);
        Assert.AreEqual(placeholder, plan.Placeholder);
        Assert.HasCount(40, plan.Uploads);
        Assert.HasCount(20, plan.Links);
        foreach (BlobUpload upload in plan.Uploads)
        {
            string fileName = Path.GetFileName(upload.Source);
            Assert.AreEqual("PUT", upload.Method);
            Assert.IsTrue(upload.Destination == $"https://dotnetorigin.blob.core.windows.net/dotnet/dotnetup/{version}/{fileName}"
                || upload.Destination == $"https://dotnetcli.blob.core.windows.net/dotnet/dotnetup/{version}/{fileName}");
            Assert.AreEqual(Convert.ToHexString(SHA512.HashData(File.ReadAllBytes(upload.Source))), upload.Sha512);
        }

        foreach (AkaUpdate link in plan.Links)
        {
            string fileName = Path.GetFileName(link.ReadUri);
            Assert.AreEqual($"https://redirectionapi-ame.trafficmanager.net/api/aka/1/ncd/dotnet/dotnetup/{channel}/{fileName}", link.ReadUri);
            Assert.AreEqual("GET", link.ReadMethod);
            Assert.AreEqual("PUT", link.IfExistsMethod);
            Assert.AreEqual("POST", link.IfNotFoundMethod);
            Assert.AreEqual("https://redirectionapi-ame.trafficmanager.net/api/aka/1/ncd/", link.CreateUri);
            Assert.AreEqual($"https://builds.dotnet.microsoft.com/dotnet/dotnetup/{version}/{fileName}", link.UpdateBody.TargetUrl);
            Assert.AreEqual(link.UpdateBody.TargetUrl, link.CreateBody.TargetUrl);
            Assert.AreEqual($"dotnet/dotnetup/{channel}/{fileName}", link.CreateBody.ShortUrl);
            Assert.AreEqual("test-owners", link.UpdateBody.Owners);
            Assert.AreEqual("test-actor", link.CreateBody.CreatedBy);
            Assert.AreEqual("test-group", link.UpdateBody.GroupOwner);
        }

        BlobUpload checksum = plan.Uploads.First(upload => upload.Source.EndsWith(".sha512", StringComparison.Ordinal));
        Assert.AreEqual("text/plain", checksum.ContentType);
        Assert.AreEqual("public, max-age=900", checksum.CacheControl);
        Assert.AreEqual("public, max-age=864000", plan.Uploads.First(upload => upload.Source.EndsWith(".exe", StringComparison.Ordinal)).CacheControl);
        Assert.AreEqual("attachment", checksum.ContentDisposition);
    }

    [TestMethod]
    [DataRow("build")]
    [DataRow("commit")]
    [DataRow("schema")]
    [DataRow("placeholder")]
    [DataRow("rid")]
    [DataRow("layout")]
    public async Task RejectsInvalidCandidateIdentity(string error)
    {
        WriteCandidates();
        string manifestPath = ManifestPath("win-x64");
        CandidateManifest manifest = JsonSerializer.Deserialize<CandidateManifest>(await File.ReadAllTextAsync(manifestPath, TestContext.CancellationToken).ConfigureAwait(false), s_jsonOptions)!;
        manifest = error switch
        {
            "build" => manifest with { BuildId = "67890" },
            "commit" => manifest with { Commit = new string('f', 40) },
            "schema" => manifest with { SchemaVersion = 2 },
            "placeholder" => manifest with { Placeholder = true },
            "rid" => manifest with { Rid = "win-unknown" },
            "layout" => manifest with { Version = "0.3.0-preview.1.26530.2" },
            _ => throw new ArgumentOutOfRangeException(nameof(error))
        };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, s_jsonOptions), TestContext.CancellationToken).ConfigureAwait(false);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => CreatePlanAsync()).ConfigureAwait(false);
    }

    [TestMethod]
    [DataRow("missing-rid")]
    [DataRow("duplicate-rid")]
    [DataRow("mixed-version")]
    [DataRow("extra-file")]
    [DataRow("checksum-mismatch")]
    [DataRow("empty-checksum")]
    [DataRow("empty-payload")]
    public async Task RejectsIncompleteOrCorruptedPayloads(string error)
    {
        WriteCandidates();
        string directory = Path.GetDirectoryName(ManifestPath("win-x64"))!;
        switch (error)
        {
            case "missing-rid":
                File.Delete(ManifestPath("linux-x64"));
                break;
            case "duplicate-rid":
                string duplicate = Path.Combine(_root, "Dotnetup_duplicate", "preview", "0.3.0-preview.1.26530.1");
                Directory.CreateDirectory(duplicate);
                File.Copy(ManifestPath("linux-x64"), Path.Combine(duplicate, "release.json"));
                break;
            case "mixed-version":
                CandidateManifest manifest = JsonSerializer.Deserialize<CandidateManifest>(File.ReadAllText(ManifestPath("win-x64")), s_jsonOptions)!;
                string newDirectory = Path.Combine(Path.GetDirectoryName(directory)!, "0.3.0-preview.1.26530.2");
                Directory.Move(directory, newDirectory);
                File.WriteAllText(Path.Combine(newDirectory, "release.json"), JsonSerializer.Serialize(manifest with { Version = "0.3.0-preview.1.26530.2" }, s_jsonOptions));
                break;
            case "extra-file":
                File.WriteAllText(Path.Combine(directory, "unapproved.exe"), "unexpected");
                break;
            case "checksum-mismatch":
                File.WriteAllText(Path.Combine(directory, "dotnetup-win-x64.exe"), "tampered");
                break;
            case "empty-checksum":
                File.WriteAllText(Path.Combine(directory, "dotnetup-win-x64.exe.sha512"), " \r\n");
                break;
            case "empty-payload":
                File.WriteAllText(Path.Combine(directory, "dotnetup-win-x64.exe"), "");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(error));
        }

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => CreatePlanAsync()).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task DoesNotPromoteDailyArtifacts()
    {
        WriteCandidates("daily", "0.3.0-daily.26530.1");
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => CreatePlanAsync("daily")).ConfigureAwait(false);
    }

    private Task<ReleasePlan> CreatePlanAsync(string quality = "preview") =>
        ReleasePlanner.CreateAsync(_root, quality, BuildId, Commit, new AkaOwnership("test-owners", "test-actor", "test-group"));

    private string ManifestPath(string rid, string quality = "preview", string version = "0.3.0-preview.1.26530.1") =>
        Path.Combine(_root, "Dotnetup_" + rid, quality, version, "release.json");

    private void WriteCandidates(string quality = "preview", string version = "0.3.0-preview.1.26530.1")
    {
        foreach (string rid in s_rids)
        {
            string manifestPath = ManifestPath(rid, quality, version);
            string directory = Path.GetDirectoryName(manifestPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(new CandidateManifest(1, BuildId, Commit, quality, version, rid, quality == "ga"), s_jsonOptions));
            string[] names = rid == "win-x64" ? ["dotnetup-win-x64.exe", "get-dotnetup.ps1", "get-dotnetup.sh"]
                : [$"dotnetup-{rid}{(rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : "")}"];
            foreach (string name in names)
            {
                byte[] bytes = Encoding.UTF8.GetBytes("candidate " + name);
                File.WriteAllBytes(Path.Combine(directory, name), bytes);
                File.WriteAllText(Path.Combine(directory, name + ".sha512"), Convert.ToHexString(SHA512.HashData(bytes)));
            }
        }
    }
}
