// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.DotNet.Dotnetup.Release;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public sealed class ReleaseToolTests : SdkTest
{
    private const int BuildId = 123;
    private const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly string[] s_rids = ["linux-arm64", "linux-musl-arm64", "linux-musl-x64", "linux-x64", "osx-arm64", "osx-x64", "win-arm64", "win-x64"];
    private string _root = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path;
    }

    [TestMethod]
    [DataRow("preview", "0.3.0-preview.1.26554.2")]
    [DataRow("daily", "0.3.0-daily.26554.2")]
    public async Task StagesOnlySelectedVariantWithExpectedPathsAndHashes(string variant, string version)
    {
        await WriteCandidatesAsync("preview", "0.3.0-preview.1.26554.2").ConfigureAwait(false);
        await WriteCandidatesAsync("daily", "0.3.0-daily.26554.2").ConfigureAwait(false);
        await WriteCandidatesAsync("ga", "0.3.0").ConfigureAwait(false);
        ReleaseRecord release = await CreateReleaseAsync(variant).ConfigureAwait(false);
        string destination = Path.Combine(_root, "staging");
        await ReleasePreparation.StageAsync(release, _root, destination).ConfigureAwait(false);

        Assert.AreEqual(variant, release.Variant);
        Assert.AreEqual(version, release.Version);
        Assert.AreEqual(BuildId, release.AzureDevOpsBuildId);
        Assert.AreEqual(Commit, release.Commit);
        Assert.HasCount(8, release.Candidates);
        Assert.HasCount(20, release.Files);
        Assert.HasCount(20, Directory.GetFiles(destination, "*", SearchOption.AllDirectories));
        foreach (ReleaseAsset file in release.Files)
        {
            Assert.AreEqual($"dotnetup/{version}/{file.Name}", file.RelativePath);
            Assert.Contains($"/{variant}/{version}/", file.SourceRelativePath);
            Assert.AreEqual(file.Sha512, await ReleaseFiles.HashAsync(Path.Combine(destination, file.RelativePath)).ConfigureAwait(false));
        }

        string binary = Path.Combine(destination, "dotnetup", version, "dotnetup-win-x64.exe");
        Assert.Contains($"signed {variant} fixture", await File.ReadAllTextAsync(binary, TestContext.CancellationToken).ConfigureAwait(false));
        await Assert.ThrowsExactlyAsync<IOException>(() => ReleasePreparation.StageAsync(release, _root, destination)).ConfigureAwait(false);
    }

    [TestMethod]
    [DataRow("id")]
    [DataRow("definition")]
    [DataRow("repository")]
    [DataRow("branch")]
    [DataRow("commit")]
    [DataRow("status")]
    [DataRow("result")]
    [DataRow("test")]
    [DataRow("missing-test")]
    [DataRow("unsigned")]
    public async Task RejectsIneligibleSourceRuns(string mutation)
    {
        JsonNode build = JsonNode.Parse(Build().GetRawText())!;
        switch (mutation)
        {
            case "id": build["id"] = 124; break;
            case "definition": build["definition"]!["id"] = 1; break;
            case "repository": build["repository"]!["name"] = "other"; break;
            case "branch": build["sourceBranch"] = "refs/heads/users/test"; break;
            case "commit": build["sourceVersion"] = new string('b', 40); break;
            case "status": build["status"] = "inProgress"; break;
            case "result": build["result"] = "partiallySucceeded"; break;
            case "test": build["templateParameters"]!["runTestBuild"] = true; break;
            case "missing-test": build["templateParameters"]!.AsObject().Remove("runTestBuild"); break;
            case "unsigned": build["tags"] = new JsonArray(); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            ReleasePreparation.CreateAsync(_root, "preview", BuildId, Commit, JsonSerializer.SerializeToElement(build))).ConfigureAwait(false);
    }

    [TestMethod]
    [DataRow("schema")]
    [DataRow("build")]
    [DataRow("commit")]
    [DataRow("variant")]
    [DataRow("placeholder")]
    [DataRow("duplicate-rid")]
    [DataRow("unknown-rid")]
    [DataRow("version")]
    [DataRow("path")]
    public async Task RejectsInvalidCandidateManifests(string mutation)
    {
        await WriteCandidatesAsync().ConfigureAwait(false);
        string path = ManifestPath("win-x64");
        CandidateManifest candidate = await ReleaseFiles.ReadJsonAsync<CandidateManifest>(path).ConfigureAwait(false);
        candidate = mutation switch
        {
            "schema" => candidate with { SchemaVersion = 2 },
            "build" => candidate with { BuildId = "124" },
            "commit" => candidate with { Commit = new string('b', 40) },
            "variant" => candidate with { Variant = "daily" },
            "placeholder" => candidate with { Placeholder = true },
            "duplicate-rid" => candidate with { Rid = "win-arm64" },
            "unknown-rid" => candidate with { Rid = "unknown" },
            "version" => candidate with { Version = "0.3.0-daily.26554.2" },
            "path" => candidate with { Version = "../0.3.0-preview.1.26554.2" },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        await ReleaseFiles.WriteJsonAsync(path, candidate).ConfigureAwait(false);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => CreateReleaseAsync()).ConfigureAwait(false);
    }

    [TestMethod]
    [DataRow("missing-placeholder")]
    [DataRow("string-placeholder")]
    [DataRow("malformed-json")]
    public async Task RequiresExplicitTypedCandidateFields(string mutation)
    {
        await WriteCandidatesAsync().ConfigureAwait(false);
        string path = ManifestPath("win-x64");
        JsonObject candidate = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.CancellationToken).ConfigureAwait(false))!.AsObject();
        if (mutation == "missing-placeholder")
        {
            candidate.Remove("placeholder");
        }
        else
        {
            candidate["placeholder"] = "false";
        }

        await File.WriteAllTextAsync(path, mutation == "malformed-json" ? "{" : candidate.ToJsonString(), TestContext.CancellationToken).ConfigureAwait(false);
        await Assert.ThrowsExactlyAsync<JsonException>(() => CreateReleaseAsync()).ConfigureAwait(false);
    }

    [TestMethod]
    [DataRow("missing-rid")]
    [DataRow("extra-candidate")]
    [DataRow("mixed-version")]
    [DataRow("extra-file")]
    [DataRow("missing-file")]
    [DataRow("empty-file")]
    [DataRow("missing-checksum")]
    [DataRow("invalid-checksum")]
    [DataRow("corrupt-payload")]
    public async Task RejectsIncompleteOrCorruptedCandidatesBeforeStaging(string mutation)
    {
        await WriteCandidatesAsync().ConfigureAwait(false);
        string directory = Path.GetDirectoryName(ManifestPath("win-x64"))!;
        string binary = Path.Combine(directory, "dotnetup-win-x64.exe");
        switch (mutation)
        {
            case "missing-rid":
                Directory.Delete(Path.Combine(_root, "Build_Linux_x64_Dotnetup"), recursive: true);
                break;
            case "extra-candidate":
                string duplicate = Path.Combine(_root, "Build_duplicate_Dotnetup", "preview", "0.3.0-preview.1.26554.2");
                Directory.CreateDirectory(duplicate);
                File.Copy(ManifestPath("linux-x64"), Path.Combine(duplicate, "release.json"));
                break;
            case "mixed-version":
                CandidateManifest manifest = await ReleaseFiles.ReadJsonAsync<CandidateManifest>(ManifestPath("win-x64")).ConfigureAwait(false);
                string other = Path.Combine(Path.GetDirectoryName(directory)!, "0.4.0-preview.1.26554.2");
                Directory.Move(directory, other);
                await ReleaseFiles.WriteJsonAsync(Path.Combine(other, "release.json"), manifest with { Version = "0.4.0-preview.1.26554.2" }).ConfigureAwait(false);
                break;
            case "extra-file": await File.WriteAllTextAsync(Path.Combine(directory, "unapproved.exe"), "extra", TestContext.CancellationToken).ConfigureAwait(false); break;
            case "missing-file": File.Delete(binary); break;
            case "empty-file": await File.WriteAllTextAsync(binary, "", TestContext.CancellationToken).ConfigureAwait(false); break;
            case "missing-checksum": File.Delete(binary + ".sha512"); break;
            case "invalid-checksum": await File.WriteAllTextAsync(binary + ".sha512", "invalid", TestContext.CancellationToken).ConfigureAwait(false); break;
            case "corrupt-payload": await File.WriteAllTextAsync(binary, "tampered", TestContext.CancellationToken).ConfigureAwait(false); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => CreateReleaseAsync()).ConfigureAwait(false);
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "staging")));
    }

    [TestMethod]
    public async Task RejectsChangesBetweenValidationAndStaging()
    {
        await WriteCandidatesAsync().ConfigureAwait(false);
        ReleaseRecord release = await CreateReleaseAsync().ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(_root, release.Files[0].SourceRelativePath), "changed", TestContext.CancellationToken).ConfigureAwait(false);
        string destination = Path.Combine(_root, "staging");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => ReleasePreparation.StageAsync(release, _root, destination)).ConfigureAwait(false);
        Assert.IsFalse(Directory.Exists(destination));
    }

    [TestMethod]
    public async Task RejectsGaSelection()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => CreateReleaseAsync("ga")).ConfigureAwait(false);
    }

    [TestMethod]
    [DataRow("failed")]
    [DataRow("missing")]
    [DataRow("duplicate")]
    [DataRow("host")]
    [DataRow("http")]
    [DataRow("query")]
    [DataRow("layout")]
    [DataRow("container")]
    [DataRow("double-prefix")]
    [DataRow("cdn-in-journal")]
    [DataRow("build")]
    [DataRow("commit")]
    [DataRow("variant")]
    [DataRow("hash")]
    [DataRow("pending")]
    public async Task RejectsInvalidBlobPublicationsBeforeDownloading(string mutation)
    {
        await WriteCandidatesAsync().ConfigureAwait(false);
        ReleaseRecord release = await CreateReleaseAsync().ConfigureAwait(false);
        BlobPublication details = BlobDetails(release);
        BlobPublishedAsset[] files = details.Files;
        string url = files[0].DownloadUrl;
        switch (mutation)
        {
            case "failed": details = details with { Status = "in-progress" }; break;
            case "missing": details = details with { Files = files.Skip(1).ToArray() }; break;
            case "duplicate": files[0] = files[1]; break;
            case "host": url = url.Replace("dotnetorigin.blob.core.windows.net", "example.com", StringComparison.Ordinal); break;
            case "http": url = url.Replace("https://", "http://", StringComparison.Ordinal); break;
            case "query": url += "?unexpected=true"; break;
            case "layout": url = url.Replace(release.Version, "0.4.0", StringComparison.Ordinal); break;
            case "container": url = url.Replace("/dotnetup/", "/dotnet/", StringComparison.Ordinal); break;
            case "double-prefix": url = url.Replace("/dotnetup/", "/dotnetup/dotnetup/", StringComparison.Ordinal); break;
            case "cdn-in-journal": url = BlobPublishing.GetCdnUrl(release.Files[0]); break;
            case "build": details = details with { AzureDevOpsBuildId = 124 }; break;
            case "commit": details = details with { Commit = new string('b', 40) }; break;
            case "variant": details = details with { Variant = "daily" }; break;
            case "hash": files[0] = files[0] with { Sha512 = new string('0', 128) }; break;
            case "pending": files[0] = files[0] with { Status = "pending" }; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        if (mutation is "host" or "http" or "query" or "layout" or "container" or "double-prefix" or "cdn-in-journal")
        {
            files[0] = files[0] with { DownloadUrl = url };
        }

        using HttpClient client = new(new FixtureHandler(_ => throw new AssertFailedException("Invalid blob data must fail before downloading.")));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            CdnVerification.VerifyAsync(release, details, Path.Combine(_root, "download"), client)).ConfigureAwait(false);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task VerifiesEveryCdnFileIncludingChecksumSidecars(bool corrupt)
    {
        await WriteCandidatesAsync().ConfigureAwait(false);
        ReleaseRecord release = await CreateReleaseAsync().ConfigureAwait(false);
        using HttpClient client = DownloadClient(release, corrupt);
        Task<CdnRelease> task = CdnVerification.VerifyAsync(release, BlobDetails(release), Path.Combine(_root, "downloads"), client);
        if (corrupt)
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => task).ConfigureAwait(false);
        }
        else
        {
            CdnRelease cdn = await task.ConfigureAwait(false);
            Assert.HasCount(20, cdn.Files);
            Assert.AreEqual(release.Variant, cdn.Variant);
            Assert.HasCount(20, Directory.GetFiles(Path.Combine(_root, "downloads")));
        }
    }

    [TestMethod]
    [DataRow("preview", "0.3.0-preview.1.26554.2")]
    [DataRow("daily", "0.3.0-daily.26554.2")]
    public async Task TracksPreviousAndVerifiedProposedAkaTargetsWithoutWrites(string variant, string version)
    {
        await WriteCandidatesAsync(variant, version).ConfigureAwait(false);
        ReleaseRecord release = await CreateReleaseAsync(variant).ConfigureAwait(false);
        using HttpClient snapshotClient = new(new FixtureHandler(request =>
        {
            Assert.AreEqual(HttpMethod.Head, request.Method);
            return new HttpResponseMessage(HttpStatusCode.Found)
            {
                Headers = { Location = new Uri($"https://ci.dot.net/public/dotnetup/previous/{Path.GetFileName(request.RequestUri!.AbsolutePath)}") }
            };
        }));
        AkaChangePlan plan = await AkaLinkPlanning.CreateAsync(release, snapshotClient).ConfigureAwait(false);
        Assert.IsFalse(plan.WritesEnabled);
        Assert.AreEqual("awaiting-cdn-verification", plan.Status);
        Assert.HasCount(20, plan.Changes);
        foreach (AkaChange change in plan.Changes)
        {
            Assert.AreEqual($"https://aka.ms/dotnet/dotnetup/{variant}/{change.Name}", change.ShortUrl);
            Assert.IsNotNull(change.PreviousTarget);
            Assert.IsNull(change.ProposedTarget);
            Assert.AreEqual("observed-public-redirect", change.ObservationStatus);
        }

        using HttpClient cdnClient = DownloadClient(release);
        CdnRelease cdn = await CdnVerification.VerifyAsync(release, BlobDetails(release), Path.Combine(_root, "cdn"), cdnClient).ConfigureAwait(false);
        AkaChangePlan verified = AkaLinkPlanning.WithVerifiedTargets(plan, cdn);
        Assert.IsFalse(verified.WritesEnabled);
        Assert.AreEqual("cdn-verified-writes-disabled", verified.Status);
        foreach (AkaChange change in verified.Changes)
        {
            Assert.AreEqual(cdn.Files.Single(file => file.Name == change.Name).DownloadUrl, change.ProposedTarget);
            Assert.AreEqual(plan.Changes.Single(previous => previous.Name == change.Name).PreviousTarget, change.PreviousTarget);
            Assert.AreEqual("planned-disabled", change.OperationStatus);
        }

        Assert.ThrowsExactly<InvalidDataException>(() => AkaLinkPlanning.WithVerifiedTargets(plan, cdn with { Version = "0.4.0" }));
        CdnAsset changed = cdn.Files[0] with { Sha512 = new string('0', 128) };
        Assert.ThrowsExactly<InvalidDataException>(() => AkaLinkPlanning.WithVerifiedTargets(plan, cdn with { Files = [changed, .. cdn.Files.Skip(1)] }));
    }

    [TestMethod]
    [DataRow("404")]
    [DataRow("500")]
    [DataRow("fallback")]
    [DataRow("network")]
    public async Task RecordsUnavailableAliasStateExplicitlyWithoutBlockingCdnPublication(string failure)
    {
        await WriteCandidatesAsync().ConfigureAwait(false);
        ReleaseRecord release = await CreateReleaseAsync().ConfigureAwait(false);
        using HttpClient client = new(new FixtureHandler(request =>
        {
            Assert.AreEqual(HttpMethod.Head, request.Method);
            return failure switch
            {
                "404" => new HttpResponseMessage(HttpStatusCode.NotFound),
                "500" => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                "fallback" => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://www.bing.com/?ref=aka") } },
                "network" => throw new HttpRequestException("fixture unavailable"),
                _ => throw new ArgumentOutOfRangeException(nameof(failure))
            };
        }));
        AkaChangePlan plan = await AkaLinkPlanning.CreateAsync(release, client).ConfigureAwait(false);
        Assert.IsFalse(plan.WritesEnabled);
        foreach (AkaChange change in plan.Changes)
        {
            Assert.IsNull(change.PreviousTarget);
            Assert.IsNull(change.ProposedTarget);
            Assert.AreEqual("unavailable", change.ObservationStatus);
            Assert.IsNotNull(change.ObservationError);
        }
    }

    [TestMethod]
    [DataRow("preview", "0.3.0-preview.1.26554.2", "empty")]
    [DataRow("daily", "0.3.0-daily.26554.2", "empty")]
    [DataRow("preview", "0.3.0-preview.1.26554.2", "existing")]
    [DataRow("preview", "0.3.0-preview.1.26554.2", "mixed")]
    [DataRow("preview", "0.3.0-preview.1.26554.2", "race")]
    [DataRow("preview", "0.3.0-preview.1.26554.2", "lost-response")]
    public async Task PublishesOnlyMissingBlobsWithCreateOnlyWritesAndExactCandidateBytes(string variant, string version, string scenario)
    {
        await WriteCandidatesAsync(variant, version).ConfigureAwait(false);
        ReleaseRecord release = await CreateReleaseAsync(variant).ConfigureAwait(false);
        string staging = Path.Combine(_root, "staging");
        await ReleasePreparation.StageAsync(release, _root, staging).ConfigureAwait(false);
        Dictionary<string, byte[]> blobs = new(StringComparer.Ordinal);
        for (int index = 0; index < release.Files.Length; index++)
        {
            ReleaseAsset file = release.Files[index];
            if (scenario == "existing" || scenario == "mixed" && index % 2 == 0)
            {
                blobs.Add(file.Name, await File.ReadAllBytesAsync(Path.Combine(staging, file.RelativePath), TestContext.CancellationToken).ConfigureAwait(false));
            }
        }

        int initialCount = blobs.Count;
        int reads = 0;
        int uploads = 0;
        bool lostResponse = false;
        using HttpClient client = new(new AsyncFixtureHandler(async request =>
        {
            string name = Path.GetFileName(request.RequestUri!.AbsolutePath);
            ReleaseAsset file = release.Files.Single(file => file.Name == name);
            Assert.AreEqual(BlobPublishing.GetUrl(file), request.RequestUri.AbsoluteUri);
            Assert.AreEqual($"https://dotnetorigin.blob.core.windows.net/dotnetup/{release.Version}/{name}", request.RequestUri.AbsoluteUri);
            Assert.AreEqual("Bearer", request.Headers.Authorization!.Scheme);
            Assert.AreEqual("2023-11-03", request.Headers.GetValues("x-ms-version").Single());
            Assert.IsTrue(request.Headers.Contains("x-ms-date"));
            if (request.Method == HttpMethod.Get)
            {
                reads++;
                return blobs.TryGetValue(name, out byte[]? bytes)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            Assert.AreEqual(HttpMethod.Put, request.Method);
            Assert.IsGreaterThanOrEqualTo(20, reads, "All existing blobs must be checked before the first write.");
            Assert.AreEqual("*", request.Headers.IfNoneMatch.Single().Tag);
            Assert.AreEqual("BlockBlob", request.Headers.GetValues("x-ms-blob-type").Single());
            Assert.AreEqual(BlobPublishing.ContentType(name), request.Headers.GetValues("x-ms-blob-content-type").Single());
            Assert.AreEqual(BlobPublishing.CacheControl(name), request.Headers.GetValues("x-ms-blob-cache-control").Single());
            Assert.AreEqual("attachment", request.Headers.GetValues("x-ms-blob-content-disposition").Single());
            byte[] content = await request.Content!.ReadAsByteArrayAsync(TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(await File.ReadAllBytesAsync(Path.Combine(staging, file.RelativePath), TestContext.CancellationToken).ConfigureAwait(false), content);
            uploads++;
            if (blobs.ContainsKey(name))
            {
                return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
            }

            blobs.Add(name, content);
            if (scenario == "lost-response" && !lostResponse)
            {
                lostResponse = true;
                throw new HttpRequestException("The successful upload response was lost.");
            }

            return new HttpResponseMessage(scenario == "race" ? HttpStatusCode.PreconditionFailed : HttpStatusCode.Created);
        }));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "fixture");
        List<BlobPublication> journal = [];
        BlobPublication result = await BlobPublishing.PublishAsync(release, staging, client, publication =>
        {
            journal.Add(publication);
            return Task.CompletedTask;
        }).ConfigureAwait(false);

        Assert.AreEqual(20 - initialCount + (scenario == "lost-response" ? 1 : 0), uploads);
        Assert.HasCount(20, blobs);
        Assert.AreEqual("pending", journal[0].Files[0].Status);
        Assert.AreEqual("storage-verified", result.Status);
        Assert.HasCount(initialCount, result.Files.Where(file => file.Status == "verified-existing").ToArray());
        Assert.HasCount(20 - initialCount, result.Files.Where(file => file.Status == "verified-upload").ToArray());
        foreach (ReleaseAsset file in release.Files)
        {
            Assert.AreEqual(file.Sha512, Convert.ToHexStringLower(SHA512.HashData(blobs[file.Name])));
            Assert.AreEqual($"https://dotnetorigin.blob.core.windows.net/dotnetup/{release.Version}/{file.Name}",
                result.Files.Single(asset => asset.Name == file.Name).DownloadUrl);
        }
    }

    [TestMethod]
    [DataRow("staging")]
    [DataRow("existing")]
    [DataRow("race")]
    [DataRow("changed-after-preflight")]
    public async Task RejectsConflictsWithoutOverwritingBlobs(string scenario)
    {
        await WriteCandidatesAsync().ConfigureAwait(false);
        ReleaseRecord release = await CreateReleaseAsync().ConfigureAwait(false);
        string staging = Path.Combine(_root, "staging");
        await ReleasePreparation.StageAsync(release, _root, staging).ConfigureAwait(false);
        if (scenario == "staging")
        {
            await File.WriteAllTextAsync(Path.Combine(staging, release.Files[^1].RelativePath), "changed", TestContext.CancellationToken).ConfigureAwait(false);
        }

        int reads = 0;
        int uploads = 0;
        BlobPublication? progress = null;
        using HttpClient client = new(new FixtureHandler(request =>
        {
            string name = Path.GetFileName(request.RequestUri!.AbsolutePath);
            if (request.Method == HttpMethod.Put)
            {
                uploads++;
                Assert.AreEqual("race", scenario);
                return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
            }

            reads++;
            if (scenario == "changed-after-preflight" && reads == 20)
            {
                File.WriteAllText(Path.Combine(staging, release.Files[0].RelativePath), "changed");
            }

            return scenario == "existing" && name == release.Files[^1].Name || scenario == "race" && uploads > 0
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => BlobPublishing.PublishAsync(release, staging, client, publication =>
        {
            progress = publication;
            return Task.CompletedTask;
        })).ConfigureAwait(false);
        Assert.AreEqual(scenario == "race" ? 1 : 0, uploads);
        Assert.AreEqual(scenario == "staging" ? 0 : scenario == "race" ? 21 : 20, reads);
        Assert.AreEqual("in-progress", progress!.Status);
        Assert.DoesNotContain(file => file.Status.StartsWith("verified-", StringComparison.Ordinal), progress.Files);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailsExplicitlyOnStorageAuthorizationErrors(bool duringUpload)
    {
        await WriteCandidatesAsync().ConfigureAwait(false);
        ReleaseRecord release = await CreateReleaseAsync().ConfigureAwait(false);
        string staging = Path.Combine(_root, "staging");
        await ReleasePreparation.StageAsync(release, _root, staging).ConfigureAwait(false);
        int uploads = 0;
        using HttpClient client = new(new FixtureHandler(request =>
        {
            if (request.Method == HttpMethod.Put)
            {
                uploads++;
            }

            return new HttpResponseMessage(duringUpload && request.Method == HttpMethod.Get ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden);
        }));
        HttpRequestException failure = await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            BlobPublishing.PublishAsync(release, staging, client, _ => Task.CompletedTask)).ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.Forbidden, failure.StatusCode);
        Assert.AreEqual(duringUpload ? 1 : 0, uploads);
    }

    [TestMethod]
    [DataRow(403)]
    [DataRow(404)]
    [DataRow(302)]
    public async Task DoesNotBindAkaTargetsWhenPublicDownloadsAreUnavailable(int status)
    {
        await WriteCandidatesAsync().ConfigureAwait(false);
        ReleaseRecord release = await CreateReleaseAsync().ConfigureAwait(false);
        using HttpClient client = new(new FixtureHandler(request =>
        {
            Assert.IsNull(request.Headers.Authorization);
            return new HttpResponseMessage((HttpStatusCode)status);
        }));
        await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            CdnVerification.VerifyAsync(release, BlobDetails(release), Path.Combine(_root, "downloads"), client)).ConfigureAwait(false);
        Assert.IsFalse(File.Exists(Path.Combine(_root, "cdn-release.json")));
    }

    [TestMethod]
    [DataRow(408)]
    [DataRow(429)]
    [DataRow(503)]
    public async Task RetriesTransientStorageFailuresWithBoundedBackoff(int status)
    {
        int attempts = 0;
        List<TimeSpan> delays = [];
        using HttpClient client = new(new FixtureHandler(_ =>
        {
            attempts++;
            return new HttpResponseMessage((HttpStatusCode)status);
        }));
        using HttpResponseMessage response = await BlobPublishing.SendAsync(client,
            () => new HttpRequestMessage(HttpMethod.Get, "https://dotnetorigin.blob.core.windows.net/dotnetup/fixture"),
            delay =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        Assert.AreEqual(6, attempts);
        Assert.AreEqual((HttpStatusCode)status, response.StatusCode);
        Assert.AreSequenceEqual(new[] { 2d, 4d, 8d, 10d, 10d }, delays.Select(delay => delay.TotalSeconds).ToArray());
    }

    [TestMethod]
    [DataRow("dotnetup/../file")]
    [DataRow("dotnetup/version/file?query")]
    [DataRow("dotnetup/version/subdirectory/file")]
    [DataRow("dotnetup/version/file\\other")]
    [DataRow("other/version/file")]
    public void RejectsUnsafeBlobPaths(string path)
    {
        Assert.ThrowsExactly<InvalidDataException>(() => BlobPublishing.GetUrl(new ReleaseAsset("file", path, "", "")));
    }

    [TestMethod]
    [DataRow("get-dotnetup.sh", "application/octet-stream", "public, max-age=900")]
    [DataRow("get-dotnetup.ps1", "application/octet-stream", "public, max-age=900")]
    [DataRow("dotnetup-win-x64.exe", "application/octet-stream", "public, max-age=864000")]
    [DataRow("dotnetup-linux-x64", "application/octet-stream", "public, max-age=86400")]
    [DataRow("dotnetup-win-x64.exe.sha512", "text/plain", "public, max-age=900")]
    public void AppliesReleaseBlobHeaderConventions(string name, string contentType, string cacheControl)
    {
        Assert.AreEqual(contentType, BlobPublishing.ContentType(name));
        Assert.AreEqual(cacheControl, BlobPublishing.CacheControl(name));
    }

    private Task<ReleaseRecord> CreateReleaseAsync(string variant = "preview") =>
        ReleasePreparation.CreateAsync(_root, variant, BuildId, Commit, Build());

    private static JsonElement Build() => JsonSerializer.SerializeToElement(new
    {
        id = BuildId,
        buildNumber = "20261104.2",
        sourceVersion = Commit,
        sourceBranch = "refs/heads/release/dnup",
        definition = new { id = 1544 },
        repository = new { name = "dotnet-sdk" },
        status = "completed",
        result = "succeeded",
        templateParameters = new { runTestBuild = "False" },
        tags = new[] { "PME Signed" }
    });

    private string ManifestPath(string rid, string variant = "preview", string version = "0.3.0-preview.1.26554.2") =>
        Path.Combine(_root, rid switch
        {
            "win-x64" => "Build_Windows_x64_Dotnetup",
            "win-arm64" => "Build_Windows_arm64_Dotnetup",
            "linux-x64" => "Build_Linux_x64_Dotnetup",
            "linux-arm64" => "Build_Linux_arm64_Dotnetup",
            "linux-musl-x64" => "Build_Linux_x64_DotnetupMusl",
            "linux-musl-arm64" => "Build_Linux_arm64_DotnetupMusl",
            "osx-x64" => "Build_Mac_x64_Dotnetup",
            "osx-arm64" => "Build_Mac_arm64_Dotnetup",
            _ => throw new ArgumentOutOfRangeException(nameof(rid))
        }, variant, version, "release.json");

    private async Task WriteCandidatesAsync(string variant = "preview", string version = "0.3.0-preview.1.26554.2")
    {
        foreach (string rid in s_rids)
        {
            string path = ManifestPath(rid, variant, version);
            string directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            await ReleaseFiles.WriteJsonAsync(path, new CandidateManifest(1, BuildId.ToString(System.Globalization.CultureInfo.InvariantCulture), Commit, variant, version, rid, variant == "ga")).ConfigureAwait(false);
            string[] names = rid == "win-x64" ? ["dotnetup-win-x64.exe", "get-dotnetup.ps1", "get-dotnetup.sh"]
                : [$"dotnetup-{rid}{(rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : "")}"];
            foreach (string name in names)
            {
                string payload = Path.Combine(directory, name);
                await File.WriteAllTextAsync(payload, $"signed {variant} fixture {name}", TestContext.CancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(payload + ".sha512", Convert.ToHexString(SHA512.HashData(await File.ReadAllBytesAsync(payload, TestContext.CancellationToken).ConfigureAwait(false))), TestContext.CancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static BlobPublication BlobDetails(ReleaseRecord release) =>
        new(release.AzureDevOpsBuildId, release.BuildNumber, release.Commit, release.Variant, release.Version,
            DateTimeOffset.UtcNow, "storage-verified",
            release.Files.Select(file => new BlobPublishedAsset(file.Name, file.Sha512, BlobPublishing.GetUrl(file), "verified-upload")).ToArray());

    private HttpClient DownloadClient(ReleaseRecord release, bool corrupt = false) => new(new FixtureHandler(request =>
    {
        Assert.AreEqual(HttpMethod.Get, request.Method);
        Assert.IsNull(request.Headers.Authorization);
        string name = Path.GetFileName(request.RequestUri!.AbsolutePath);
        Assert.AreEqual($"https://builds.dotnet.microsoft.com/dotnetup/{release.Version}/{name}", request.RequestUri.AbsoluteUri);
        byte[] content = File.ReadAllBytes(Path.Combine(_root, release.Files.Single(file => file.Name == name).SourceRelativePath));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(corrupt && name.EndsWith(".sha512", StringComparison.Ordinal) ? [1] : content) };
    }));

    private sealed class FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class AsyncFixtureHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request);
    }
}
