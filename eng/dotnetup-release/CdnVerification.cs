// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Dotnetup.Release;

internal static class CdnVerification
{
    // Public verification must consume a completed storage journal for this exact selected source build.
    internal static async Task<CdnRelease> VerifyAsync(ReleaseRecord release, BlobPublication details, string directory, HttpClient client)
    {
        if (details.Status != "storage-verified" ||
            details.AzureDevOpsBuildId != release.AzureDevOpsBuildId ||
            details.BuildNumber != release.BuildNumber ||
            details.Commit != release.Commit ||
            details.Variant != release.Variant ||
            details.Version != release.Version)
        {
            throw new InvalidDataException("Blob publishing must be complete and match the selected release.");
        }

        // Require the exact approved inventory and fixed blob URLs before making any public requests.
        Dictionary<string, string> urls = new(StringComparer.Ordinal);
        foreach (BlobPublishedAsset file in details.Files)
        {
            ReleaseAsset? asset = release.Files.SingleOrDefault(asset => asset.Name == file.Name);
            if (asset is null ||
                file.Sha512 != asset.Sha512 ||
                file.DownloadUrl != BlobPublishing.GetUrl(asset) ||
                file.Status is not ("verified-existing" or "verified-upload") ||
                !urls.TryAdd(file.Name, file.DownloadUrl))
            {
                throw new InvalidDataException($"Unexpected, duplicate, unverified, or incorrectly laid out blob download: {file.DownloadUrl}");
            }
        }

        if (urls.Count != release.Files.Length)
        {
            throw new InvalidDataException($"Expected {release.Files.Length} blob downloads; found {urls.Count}.");
        }

        // Use a fresh directory so previous downloads cannot be mistaken for this attempt's results.
        if (Directory.Exists(directory) || File.Exists(directory))
        {
            throw new IOException($"The download directory must not already exist: {directory}");
        }

        Directory.CreateDirectory(directory);
        List<CdnAsset> assets = [];
        // Verify payloads and checksum files independently; one failure prevents a verified release from being returned.
        foreach (ReleaseAsset file in release.Files)
        {
            string path = Path.Combine(directory, file.Name);
            ReleaseFiles.Debug($"Downloading {file.Name}: {urls[file.Name]}");
            await DownloadAsync(client, urls[file.Name], path).ConfigureAwait(false);

            if (await ReleaseFiles.HashAsync(path).ConfigureAwait(false) != file.Sha512)
            {
                throw new InvalidDataException($"CDN download does not match the signed build asset: {file.Name}");
            }

            Console.WriteLine($"Verified CDN file {file.Name}: {urls[file.Name]}");
            ReleaseFiles.Debug($"SHA-512 {file.Name}: {file.Sha512}");
            assets.Add(new CdnAsset(file.Name, file.Sha512, urls[file.Name]));
        }

        return new CdnRelease(release.AzureDevOpsBuildId, release.BuildNumber, release.Commit, release.Variant, release.Version, [.. assets]);
    }

    private static async Task DownloadAsync(HttpClient client, string url, string path)
    {
        // The caller supplies an unauthenticated, nonredirecting client; only a direct public 200 is accepted.
        using HttpResponseMessage response = await BlobPublishing.SendAsync(client, () => new HttpRequestMessage(HttpMethod.Get, url)).ConfigureAwait(false);
        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            throw new HttpRequestException($"Public blob download failed for {url}: HTTP {(int)response.StatusCode}.", null, response.StatusCode);
        }

        using FileStream output = File.Create(path);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(300));
        await response.Content.CopyToAsync(output, timeout.Token).ConfigureAwait(false);
    }
}
