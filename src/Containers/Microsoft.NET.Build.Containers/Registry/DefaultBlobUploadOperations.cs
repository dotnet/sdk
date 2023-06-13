// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Registry;

internal class DefaultBlobUploadOperations : IBlobUploadOperations
{
    private readonly ILogger _logger;

    public DefaultBlobUploadOperations(HttpClient client, ILogger logger)
    {
        Client = client;
        _logger = logger;
    }

    private HttpClient Client { get; }

    public async Task CompleteAsync(Uri uploadUri, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // PUT with digest to finalize
        UriBuilder builder = new(uploadUri.IsAbsoluteUri ? uploadUri : new Uri(Client.BaseAddress!, uploadUri));
        builder.Query += $"&digest={Uri.EscapeDataString(digest)}";
        Uri putUri = builder.Uri;
        HttpResponseMessage finalizeResponse = await Client.PutAsync(putUri, null, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (finalizeResponse.StatusCode != HttpStatusCode.Created)
        {
            await finalizeResponse.LogHttpResponse(_logger, cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PUT {putUri}", finalizeResponse.StatusCode);
            throw new ApplicationException(errorMessage);
        }
    }

    public async Task<HttpResponseMessage> GetStatusAsync(Uri uploadUri, CancellationToken cancellationToken)
    {
        return await Client.GetAsync(uploadUri.IsAbsoluteUri ? uploadUri : new Uri(Client.BaseAddress!, uploadUri), cancellationToken).ConfigureAwait(false);
    }

    public async Task<StartUploadInformation> StartAsync(string repositoryName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri startUploadUri = new Uri(Client.BaseAddress!, $"/v2/{repositoryName}/blobs/uploads/");

        HttpResponseMessage pushResponse = await Client.PostAsync(startUploadUri, content: null, cancellationToken).ConfigureAwait(false);

        if (pushResponse.StatusCode != HttpStatusCode.Accepted)
        {
            await pushResponse.LogHttpResponse(_logger, cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"POST {startUploadUri}", pushResponse.StatusCode);
            throw new ApplicationException(errorMessage);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var chunkSize = pushResponse.ParseRangeAmount() ?? pushResponse.ParseOCIChunkMinSizeAmount();
        var location = pushResponse.GetNextLocation();
        return new(chunkSize, location);
    }

    public async Task<bool> TryMountAsync(string destinationRepository, string sourceRepository, string digest, CancellationToken cancellationToken)
    {
        // Blob wasn't there; can we tell the server to get it from the base image?
        HttpResponseMessage pushResponse = await Client.PostAsync(new Uri(Client.BaseAddress!, $"/v2/{destinationRepository}/blobs/uploads/?mount={digest}&from={sourceRepository}"), content: null, cancellationToken).ConfigureAwait(false);
        return pushResponse.StatusCode == HttpStatusCode.Created;
    }

    public async Task<FinalizeUploadInformation> UploadAtomicallyAsync(Uri uploadUri, Stream content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StreamContent httpContent = new(content);
        httpContent.Headers.ContentLength = content.Length;

        HttpRequestMessage patchMessage = GetPatchHttpRequest(uploadUri, httpContent);
        HttpResponseMessage patchResponse = await Client.SendAsync(patchMessage, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        // Fail the upload if the response code is not Accepted (202) or if uploading to Amazon ECR which returns back Created (201).
        if (!(patchResponse.StatusCode == HttpStatusCode.Accepted || uploadUri.IsAmazonECRRegistry() && patchResponse.StatusCode == HttpStatusCode.Created))
        {
            await patchResponse.LogHttpResponse(_logger, cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PATCH {uploadUri}", patchResponse.StatusCode);
            throw new ApplicationException(errorMessage);
        }
        return new(patchResponse.GetNextLocation());
    }

    public Task<HttpResponseMessage> UploadChunkAsync(Uri uploadUri, HttpContent content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpRequestMessage patchMessage = GetPatchHttpRequest(uploadUri, content);
        return Client.SendAsync(patchMessage, cancellationToken);
    }

    private HttpRequestMessage GetPatchHttpRequest(Uri uploadUri, HttpContent httpContent)
    {
        Uri finalUri = uploadUri.IsAbsoluteUri ? uploadUri : new Uri(Client.BaseAddress!, uploadUri);
        HttpRequestMessage patchMessage = new(HttpMethod.Patch, finalUri)
        {
            Content = httpContent
        };
        patchMessage.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        return patchMessage;
    }
}
