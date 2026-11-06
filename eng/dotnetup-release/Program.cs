// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.DotNet.Dotnetup.Release;

try
{
    // Prepare validates a selected official build and stages its existing signed bytes without publishing.
    if (args is ["prepare", var source, var variant, var buildId, var commit, var buildPath, var staging, var metadata] &&
        int.TryParse(buildId, NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id > 0)
    {
        JsonElement build = await ReleaseFiles.ReadJsonAsync<JsonElement>(buildPath);
        ReleaseRecord release = await ReleasePreparation.CreateAsync(source, variant, id, commit, build);
        await ReleasePreparation.StageAsync(release, source, staging);

        // Retain the selected build's inventory and per-RID manifests as evidence for later steps.
        Directory.CreateDirectory(metadata);
        await ReleaseFiles.WriteJsonAsync(Path.Combine(metadata, "release.json"), release);

        foreach (SourceCandidate candidate in release.Candidates)
        {
            string directory = Path.Combine(metadata, "candidates", candidate.Rid);
            Directory.CreateDirectory(directory);
            File.Copy(Path.Combine(source, candidate.SourceManifest), Path.Combine(directory, "release.json"));
        }

        // Observe current aliases without following redirects or modifying them; proposed targets stay unresolved.
        using HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        AkaChangePlan plan = await AkaLinkPlanning.CreateAsync(release, client);
        await ReleaseFiles.WriteJsonAsync(Path.Combine(metadata, "aka-ms-plan.json"), plan);

        Console.WriteLine($"Selected {variant} dotnetup {release.Version}, build {id}, commit {release.Commit}.");
        Console.WriteLine($"Source: https://dev.azure.com/dnceng/internal/_build/results?buildId={id}");
        Console.WriteLine("Eligibility: successful, non-test, PME-signed build of pipeline 1544 on release/dnup.");

        long bytes = release.Files.Sum(file => new FileInfo(Path.Combine(staging, file.RelativePath)).Length);
        Console.WriteLine($"Staged {release.Files.Length} verified files, {bytes} bytes under {staging}. Aka.ms writes are disabled.");
    }
    else if (args is ["publish", var stagingDirectory, var publishMetadata])
    {
        // AzureCLI supplies a storage-scoped token for this process; the token is never written to metadata.
        string token = Environment.GetEnvironmentVariable("BLOB_ACCESS_TOKEN")
            ?? throw new InvalidOperationException("BLOB_ACCESS_TOKEN must contain an Azure Storage access token.");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("BLOB_ACCESS_TOKEN must not be empty.");
        }

        ReleaseRecord release = await ReleaseFiles.ReadJsonAsync<ReleaseRecord>(Path.Combine(publishMetadata, "release.json"));
        using HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(300) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Persist publication progress so failures can be investigated and the same build safely retried.
        await BlobPublishing.PublishAsync(release, stagingDirectory, client,
            publication => ReleaseFiles.WriteJsonAsync(Path.Combine(publishMetadata, "blob-publication.json"), publication));

        Console.WriteLine("All blob uploads verified against the signed build. Public download verification is still required.");
    }
    else if (args is ["verify", var metadataDirectory, var detailsPath, var downloads])
    {
        ReleaseRecord release = await ReleaseFiles.ReadJsonAsync<ReleaseRecord>(Path.Combine(metadataDirectory, "release.json"));
        BlobPublication details = await ReleaseFiles.ReadJsonAsync<BlobPublication>(detailsPath);

        // A separate unauthenticated client proves the exact blob URLs work for public acquisition.
        using HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(300) };
        CdnRelease cdn = await CdnVerification.VerifyAsync(release, details, downloads, client);
        await ReleaseFiles.WriteJsonAsync(Path.Combine(metadataDirectory, "cdn-release.json"), cdn);

        // Bind alias plans only after every public download matches; recording targets does not enable writes.
        AkaChangePlan plan = await ReleaseFiles.ReadJsonAsync<AkaChangePlan>(Path.Combine(metadataDirectory, "aka-ms-plan.json"));
        await ReleaseFiles.WriteJsonAsync(Path.Combine(metadataDirectory, "aka-ms-plan.json"), AkaLinkPlanning.WithVerifiedTargets(plan, cdn));

        Console.WriteLine("All public blob downloads verified. Aka.ms proposed targets recorded; no links were changed.");
    }
    else
    {
        Console.Error.WriteLine("Usage: dotnetup-release prepare <candidates> <preview|daily> <build-id> <commit> <source-build.json> <staging> <metadata>");
        Console.Error.WriteLine("       dotnetup-release publish <staging> <metadata> (requires BLOB_ACCESS_TOKEN)");
        Console.Error.WriteLine("       dotnetup-release verify <metadata> <blob-publication.json> <downloads>");
        return 1;
    }

    return 0;
}
catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or KeyNotFoundException or
    JsonException or InvalidOperationException or HttpRequestException or OperationCanceledException or System.Security.Cryptography.CryptographicException)
{
    Console.Error.WriteLine($"Release failed: {exception.Message}");
    return 1;
}
