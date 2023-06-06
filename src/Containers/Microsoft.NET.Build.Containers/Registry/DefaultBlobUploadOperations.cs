// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;
using System.Net;
using System.Net.Http.Headers;

namespace Microsoft.NET.Build.Containers.Registry;

internal class DefaultBlobUploadOperations : IBlobUploadOperations
{
    private HttpClient Client { get; }
    public DefaultBlobUploadOperations(HttpClient client)
    {
        Client = client;
    }

    public async Task<StartUploadInformation> StartAsync(string repositoryName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri startUploadUri = new Uri($"/v2/{repositoryName}/blobs/uploads/");

        HttpResponseMessage pushResponse = await Client.PostAsync(startUploadUri, content: null, cancellationToken).ConfigureAwait(false);

        if (pushResponse.StatusCode != HttpStatusCode.Accepted)
        {
            var headers = pushResponse.Headers.ToString();
            var detail = await pushResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"POST {startUploadUri}", pushResponse.StatusCode, headers + Environment.NewLine + detail);
            throw new ApplicationException(errorMessage);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var chunkSize = pushResponse.ParseRangeAmount() ?? pushResponse.ParseOCIChunkMinSizeAmount();
        var location = pushResponse.GetNextLocation();
        return new(chunkSize, location);
    }

    public async Task<HttpResponseMessage> UploadChunkAsync(Uri uploadUri, HttpContent content, CancellationToken token)
    {
        return await Client.PatchAsync(uploadUri, content, token).ConfigureAwait(false);
    }

    public async Task<FinalizeUploadInformation> UploadAtomicallyAsync(Uri uploadUri, Stream contents, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        StreamContent content = new StreamContent(contents);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = contents.Length;
        var patchResponse = await Client.PatchAsync(uploadUri, content, token).ConfigureAwait(false);

        token.ThrowIfCancellationRequested();
        // Fail the upload if the response code is not Accepted (202) or if uploading to Amazon ECR which returns back Created (201).
        if (!(patchResponse.StatusCode == HttpStatusCode.Accepted || uploadUri.IsAmazonECRRegistry() && patchResponse.StatusCode == HttpStatusCode.Created))
        {
            var headers = patchResponse.Headers.ToString();
            var detail = await patchResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"Whole PATCH {uploadUri}", patchResponse.StatusCode, headers + Environment.NewLine + detail);
            throw new ApplicationException(errorMessage);
        }
        return new(patchResponse.GetNextLocation());
    }

    public async Task<HttpResponseMessage> GetStatusAsync(Uri uploadUri, CancellationToken token)
    {
        return await Client.GetAsync(uploadUri, token).ConfigureAwait(false);
    }

    public async Task CompleteAsync(Uri uploadUri, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // PUT with digest to finalize
        UriBuilder builder = new(uploadUri);
        builder.Query += $"&digest={Uri.EscapeDataString(digest)}";
        var putUri = builder.Uri;
        HttpResponseMessage finalizeResponse = await Client.PutAsync(putUri, null, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (finalizeResponse.StatusCode != HttpStatusCode.Created)
        {
            var headers = finalizeResponse.Headers.ToString();
            var detail = await finalizeResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PUT {putUri}", finalizeResponse.StatusCode, headers + Environment.NewLine + detail);
            throw new ApplicationException(errorMessage);
        }
    }

    public async Task<bool> TryMount(string destinationRepository, string sourceRepository, string digest)
    {
        // Blob wasn't there; can we tell the server to get it from the base image?
        HttpResponseMessage pushResponse = await Client.PostAsync(new Uri($"/v2/{destinationRepository}/blobs/uploads/?mount={digest}&from={sourceRepository}"), content: null).ConfigureAwait(false);
        return pushResponse.StatusCode == HttpStatusCode.Created;
    }
}
