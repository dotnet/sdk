// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Microsoft.DotNet.Dotnetup.Release;

internal static class BlobPublishing
{
    private const string BlobRoot = "https://dotnetorigin.blob.core.windows.net/dotnetup/";

    internal static string GetUrl(ReleaseAsset asset)
    {
        // Restrict all storage requests to safe versioned paths on the fixed publishing account and container.
        string[] segments = asset.RelativePath.Split('/');
        if (segments is not ["dotnetup", var version, var name] ||
            name != asset.Name ||
            segments.Any(segment => segment.Length == 0 ||
            segment is "." or ".." ||
            segment.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_'))))
        {
            throw new InvalidDataException($"Invalid versioned blob path: {asset.RelativePath}");
        }

        // The staging prefix becomes the container name, not a second dotnetup directory inside that container.
        return BlobRoot + $"{version}/{name}";
    }

    internal static string GetCdnUrl(ReleaseAsset asset)
    {
        // Reuse storage path validation, but never expose the authenticated origin URL as a public alias target.
        _ = GetUrl(asset);
        return "https://builds.dotnet.microsoft.com/" + asset.RelativePath;
    }

    internal static async Task<BlobPublication> PublishAsync(
        ReleaseRecord release, string staging, HttpClient client, Func<BlobPublication, Task> recordProgress)
    {
        // Start the journal before storage operations; a partial release must remain distinguishable from success.
        BlobPublishedAsset[] assets = [.. release.Files.Select(file => new BlobPublishedAsset(file.Name, file.Sha512, GetUrl(file), "pending"))];
        BlobPublication publication = new(release.AzureDevOpsBuildId, release.BuildNumber, release.Commit, release.Variant, release.Version,
            DateTimeOffset.UtcNow, "in-progress", [.. assets]);
        await recordProgress(publication).ConfigureAwait(false);

        // The prepare step is not sufficient: staged bytes may have changed before this process started.
        foreach (ReleaseAsset file in release.Files)
        {
            if (await ReleaseFiles.HashAsync(Path.Combine(staging, file.RelativePath)).ConfigureAwait(false) != file.Sha512)
            {
                throw new InvalidDataException($"Staged file changed before blob publishing: {file.Name}");
            }
        }

        // Detect conflicting versions before writing any missing files.
        bool[] exists = new bool[assets.Length];
        for (int index = 0; index < assets.Length; index++)
        {
            exists[index] = await VerifyBlobAsync(client, assets[index], allowMissing: true).ConfigureAwait(false);
        }

        for (int index = 0; index < assets.Length; index++)
        {
            BlobPublishedAsset asset = assets[index];
            if (!exists[index])
            {
                // Mark the attempt before uploading; an interrupted response may leave a blob present but unverified.
                assets[index] = asset with { Status = "uploading" };
                await recordProgress(publication with { UpdatedAt = DateTimeOffset.UtcNow, Files = [.. assets] }).ConfigureAwait(false);
                string path = Path.Combine(staging, release.Files[index].RelativePath);
                using HttpResponseMessage response = await SendAsync(client, () =>
                {
                    HttpRequestMessage request = CreateRequest(HttpMethod.Put, asset.DownloadUrl);
                    request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);
                    request.Headers.Add("x-ms-blob-type", "BlockBlob");
                    request.Headers.Add("x-ms-blob-content-type", ContentType(asset.Name));
                    request.Headers.Add("x-ms-blob-cache-control", CacheControl(asset.Name));
                    request.Headers.Add("x-ms-blob-content-disposition", "attachment");

                    // Each retry opens and rehashes a fresh stream, then rewinds it to upload the exact staged bytes.
                    FileStream stream = File.OpenRead(path);
                    if (Convert.ToHexStringLower(SHA512.HashData(stream)) != asset.Sha512)
                    {
                        stream.Dispose();
                        request.Dispose();
                        throw new InvalidDataException($"Staged file changed before blob upload: {asset.Name}");
                    }

                    stream.Position = 0;
                    request.Content = new StreamContent(stream);
                    return request;
                }).ConfigureAwait(false);

                // 409/412 can mean a racing publisher or a lost successful response; accept only matching stored bytes.
                if (response.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict))
                {
                    throw new HttpRequestException($"Blob upload failed for {asset.Name}: HTTP {(int)response.StatusCode}.", null, response.StatusCode);
                }

                await VerifyBlobAsync(client, asset, allowMissing: false).ConfigureAwait(false);
            }

            assets[index] = asset with { Status = exists[index] ? "verified-existing" : "verified-upload" };
            Console.WriteLine($"Blob {assets[index].Status}: {asset.DownloadUrl}");
            await recordProgress(publication with { UpdatedAt = DateTimeOffset.UtcNow, Files = [.. assets] }).ConfigureAwait(false);
        }

        // Storage verification proves integrity, not public availability; anonymous verification is a separate step.
        publication = publication with { UpdatedAt = DateTimeOffset.UtcNow, Status = "storage-verified", Files = [.. assets] };
        await recordProgress(publication).ConfigureAwait(false);
        return publication;
    }

    // Match release storage conventions while preserving the existing checksum sidecars verbatim.
    internal static string ContentType(string name) =>
        name.EndsWith(".sha512", StringComparison.Ordinal) ? "text/plain" : "application/octet-stream";

    internal static string CacheControl(string name) =>
        name.EndsWith(".sha512", StringComparison.Ordinal) || name.EndsWith(".ps1", StringComparison.Ordinal) || name.EndsWith(".sh", StringComparison.Ordinal)
            ? "public, max-age=900"
            : name.EndsWith(".exe", StringComparison.Ordinal) ? "public, max-age=864000" : "public, max-age=86400";

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        HttpRequestMessage request = new(method, url);
        request.Headers.Add("x-ms-version", "2023-11-03");
        request.Headers.Add("x-ms-date", DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture));
        return request;
    }

    private static async Task<bool> VerifyBlobAsync(HttpClient client, BlobPublishedAsset asset, bool allowMissing)
    {
        using HttpResponseMessage response = await SendAsync(client, () => CreateRequest(HttpMethod.Get, asset.DownloadUrl)).ConfigureAwait(false);
        // Missing blobs are expected during preflight only; authorization and other failures must stop publishing.
        if (allowMissing && response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new HttpRequestException($"Blob verification failed for {asset.Name}: HTTP {(int)response.StatusCode}.", null, response.StatusCode);
        }

        // Hash the full stored body rather than trusting server metadata or an existence-only check.
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(300));
        using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        string hash = Convert.ToHexStringLower(await SHA512.HashDataAsync(stream, timeout.Token).ConfigureAwait(false));
        if (hash != asset.Sha512)
        {
            throw new InvalidDataException($"Blob content conflicts with the signed build asset; refusing to overwrite: {asset.Name}");
        }

        return true;
    }

    internal static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, Func<HttpRequestMessage> createRequest,
        Func<TimeSpan, Task>? delay = null)
    {
        // Recreate requests for bounded retries so upload streams restart; do not retry permanent authorization failures.
        for (int attempt = 0; ; attempt++)
        {
            using HttpRequestMessage request = createRequest();
            try
            {
                HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (attempt >= 5 || !IsTransient(response.StatusCode))
                {
                    return response;
                }

                Console.WriteLine($"Download/upload attempt {attempt + 1} failed for {request.RequestUri}: HTTP {(int)response.StatusCode}. Retrying.");
                response.Dispose();
            }
            catch (HttpRequestException exception) when (attempt < 5 && (exception.StatusCode is null || IsTransient(exception.StatusCode.Value)))
            {
                Console.WriteLine($"Download/upload attempt {attempt + 1} failed for {request.RequestUri}: {exception.Message}. Retrying.");
            }
            catch (OperationCanceledException) when (attempt < 5)
            {
                Console.WriteLine($"Download/upload attempt {attempt + 1} timed out for {request.RequestUri}. Retrying.");
            }

            await (delay ?? Task.Delay)(TimeSpan.FromSeconds(Math.Min(2 * Math.Pow(2, attempt), 10))).ConfigureAwait(false);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;
}
