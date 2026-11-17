// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/ManifestStore.cs
// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Serialization;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;
using OrasProject.Oras.Registry.Remote.Auth;
using Index = OrasProject.Oras.Oci.Index;

namespace OrasProject.Oras.Registry.Remote;

/// <summary>
/// ManifestStore provides access to the manifests of a remote repository.
/// </summary>
public class ManifestStore(Repository repository) : IManifestStore
{
    private Repository Repository { get; } = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>
    /// Fetches the content identified by the descriptor.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    /// <exception cref="Exception"></exception>
    public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        ScopeManager.SetActionsForRepository(
            Repository.Options.Client,
            Repository.Options.Reference,
            Repository.Options.PartitionId,
            Scope.Action.Pull);
        var remoteReference = Repository.ParseReferenceFromDigest(target.Digest);
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryManifest();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd(target.MediaType);

        var response = await Repository.Options.Client.SendAsync(
            request,
            partitionId: Repository.Options.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        try
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    break;
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"Digest {target.Digest} not found");
                default:
                    throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
            }
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType != target.MediaType)
            {
                throw new HttpIOException(HttpRequestError.InvalidResponse, $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: mismatch response Content-Type {mediaType}: expect {target.MediaType}");
            }
            var size = response.Content.Headers.ContentLength;
            if (size != null && size != target.Size)
            {
                throw new HttpIOException(HttpRequestError.InvalidResponse, $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: mismatch Content-Length");
            }
            response.VerifyContentDigest(target.Digest);
            return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Fetches the manifest identified by the reference.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
        => await FetchAsync(reference, options: new FetchOptions(), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Fetches the manifest identified by the reference with additional options.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="options">Options for the fetch operation.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a custom header is a content header.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown when a custom header name or value has an invalid format.
    /// </exception>
    public async Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(
        string reference,
        FetchOptions options,
        CancellationToken cancellationToken = default)
    {
        ScopeManager.SetActionsForRepository(
            Repository.Options.Client,
            Repository.Options.Reference,
            Repository.Options.PartitionId,
            Scope.Action.Pull);
        var remoteReference = Repository.ParseReference(reference);
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryManifest();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd(Repository.ManifestAcceptHeader());
        request.AddHeaders(options.Headers);
        var response = await Repository.Options.Client.SendAsync(
            request,
            partitionId: Repository.Options.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        try
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    Descriptor desc;
                    if (response.Content.Headers.ContentLength == null)
                    {
                        var resolveOptions = new ResolveOptions { Headers = options.Headers };
                        desc = await ResolveAsync(reference, resolveOptions, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        desc = await response.GenerateDescriptorAsync(remoteReference, Repository.Options.MaxMetadataBytes, cancellationToken).ConfigureAwait(false);
                    }
                    return (desc, await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"{request.Method} {request.RequestUri}: manifest unknown");
                default:
                    throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Returns true if the described content exists.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        try
        {
            await ResolveAsync(target.Digest, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Pushes the manifest content, matching the expected descriptor.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="content"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default)
        => await PushAsync(expected, content, expected.Digest, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Pushes the manifest content with a reference tag.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="content"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PushAsync(Descriptor expected, Stream content, string reference, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        await PushWithIndexingAsync(expected, content, remoteReference, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// PushWithIndexingAsync pushes the given manifest to the repository with indexing support.
    /// If referrer support is not enabled, the function will first push the content, then process and update 
    /// the referrers index before pushing the content again. It handles both image manifests and index manifests.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="content"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task PushWithIndexingAsync(Descriptor expected, Stream content, Reference reference,
        CancellationToken cancellationToken = default)
    {
        switch (expected.MediaType)
        {
            case MediaType.ImageManifest:
            case MediaType.ImageIndex:
                if (Repository.ReferrersState == Referrers.ReferrersState.Supported)
                {
                    // Push the manifest straightaway when the registry supports referrers API
                    await DoPushAsync(expected, content, reference, cancellationToken).ConfigureAwait(false);
                    return;
                }

                var contentBytes = await content.ReadAllAsync(expected, cancellationToken).ConfigureAwait(false);
                using (var contentDuplicate = new MemoryStream(contentBytes))
                {
                    // Push the manifest when ReferrerState is Unknown or NotSupported
                    await DoPushAsync(expected, contentDuplicate, reference, cancellationToken).ConfigureAwait(false);
                }
                if (Repository.ReferrersState == Referrers.ReferrersState.Supported)
                {
                    // Early exit when the registry supports Referrers API
                    // No need to index referrers list
                    return;
                }

                using (var contentDuplicate = new MemoryStream(contentBytes))
                {
                    // 1. Index the referrers list using referrers tag schema when manifest contains a subject field
                    //    And the ReferrerState is not supported
                    // 2. Or do nothing when the manifest does not contain a subject field when ReferrerState is not supported/unknown
                    await ProcessReferrersAndPushIndex(expected, contentDuplicate, cancellationToken).ConfigureAwait(false);
                }
                break;
            default:
                await DoPushAsync(expected, content, reference, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// ProcessReferrersAndPushIndex processes the referrers for the given descriptor by deserializing its content
    /// (either as an image manifest or image index), extracting relevant metadata
    /// such as the subject, artifact type, and annotations, and then updates the 
    /// referrers index if applicable.
    /// </summary>
    /// <param name="desc"></param>
    /// <param name="content"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task ProcessReferrersAndPushIndex(Descriptor desc, Stream content, CancellationToken cancellationToken = default)
    {
        Descriptor? subject;
        switch (desc.MediaType)
        {
            case MediaType.ImageIndex:
                var indexManifest = await OciJsonSerializer
                    .DeserializeAsync<Index>(content, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new JsonException("Failed to deserialize index");
                if (indexManifest.Subject == null)
                {
                    return;
                }
                subject = indexManifest.Subject;
                desc.ArtifactType = indexManifest.ArtifactType;
                desc.Annotations = indexManifest.Annotations;
                break;
            case MediaType.ImageManifest:
                var imageManifest = await OciJsonSerializer
                    .DeserializeAsync<Manifest>(content, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new JsonException("Failed to deserialize manifest");
                if (imageManifest.Subject == null)
                {
                    return;
                }
                subject = imageManifest.Subject;
                desc.ArtifactType = string.IsNullOrEmpty(imageManifest.ArtifactType)
                    ? imageManifest.Config.MediaType
                    : imageManifest.ArtifactType;
                desc.Annotations = imageManifest.Annotations;
                break;
            default:
                return;
        }

        // In this case, the manifest contains a subject field and OCI-Subject Header is not set after pushing the manifest to the registry,
        // which indicates that the registry does not support referrers API
        Repository.SetReferrersState(false);
        await UpdateReferrersIndex(subject, new Referrers.ReferrerChange(desc, Referrers.ReferrerOperation.Add), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// UpdateReferrersIndex updates the referrers index for a given subject by applying the specified referrer changes.
    /// If the referrers index is updated, the new index is pushed to the repository. If referrers 
    /// garbage collection is not skipped, the old index is deleted.
    /// References:
    ///  - https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#pushing-manifests-with-subject
    ///  - https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#deleting-manifests
    /// </summary>
    /// <param name="subject"></param>
    /// <param name="referrerChange"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task UpdateReferrersIndex(Descriptor subject,
        Referrers.ReferrerChange referrerChange, CancellationToken cancellationToken = default)
    {
        // 1. pull the original referrers index list using referrers tag schema
        var referrersTag = Referrers.BuildReferrersTag(subject);
        var (oldDesc, oldReferrers) = await Repository.PullReferrersIndexList(referrersTag, cancellationToken).ConfigureAwait(false);

        // 2. apply the referrer change to referrers list
        var (updatedReferrers, updateRequired) =
            Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        if (!updateRequired)
        {
            return;
        }

        // 3. push the updated referrers list using referrers tag schema
        if (updatedReferrers.Count > 0 || Repository.Options.SkipReferrersGc)
        {
            // push a new index in either case:
            // 1. the referrers list has been updated with a non-zero size
            // 2. OR the updated referrers list is empty but referrers GC
            //    is skipped, in this case an empty index should still be pushed
            //    as the old index won't get deleted
            var (indexDesc, indexContent) = Index.GenerateIndex(updatedReferrers);
            using var content = new MemoryStream(indexContent);
            await DoPushAsync(indexDesc, content, Repository.ParseReference(referrersTag), cancellationToken).ConfigureAwait(false);
        }

        if (Repository.Options.SkipReferrersGc || Descriptor.IsNullOrInvalid(oldDesc))
        {
            // Skip the delete process if SkipReferrersGc is set to true or the old Descriptor is empty or null
            return;
        }

        // 4. delete the dangling original referrers index, if applicable
        await DeleteAsync(oldDesc!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pushes the manifest content, matching the expected descriptor.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="stream"></param>
    /// <param name="remoteReference"></param>
    /// <param name="cancellationToken"></param>
    private async Task DoPushAsync(Descriptor expected, Stream stream, Reference remoteReference, CancellationToken cancellationToken = default)
    {
        // pushing usually requires both pull and push actions.
        // Reference: https://github.com/distribution/distribution/blob/v2.7.1/registry/handlers/app.go#L921-L930
        ScopeManager.SetActionsForRepository(
            Repository.Options.Client,
            Repository.Options.Reference,
            Repository.Options.PartitionId,
            Scope.Action.Pull,
            Scope.Action.Push);
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryManifest();
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StreamContent(stream)
        };
        request.Content.Headers.ContentLength = expected.Size;
        request.Content.Headers.Add("Content-Type", expected.MediaType);
        var client = Repository.Options.Client;
        using var response = await client.SendAsync(
            request,
            partitionId: Repository.Options.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }
        response.CheckOciSubjectHeader(Repository);
        response.VerifyContentDigest(expected.Digest);
    }

    /// <summary>
    /// Resolves the given reference to its descriptor.
    /// </summary>
    /// <param name="reference">The tag or digest to resolve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The descriptor of the resolved manifest.</returns>
    public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        => await ResolveAsync(reference, new ResolveOptions(), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// ResolveAsync resolves a reference to a descriptor with additional options.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="options">Options for the resolve operation.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a custom header is a content header.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown when a custom header name or value has an invalid format.
    /// </exception>
    public async Task<Descriptor> ResolveAsync(
        string reference,
        ResolveOptions options,
        CancellationToken cancellationToken = default)
    {
        ScopeManager.SetActionsForRepository(
            Repository.Options.Client,
            Repository.Options.Reference,
            Repository.Options.PartitionId,
            Scope.Action.Pull);
        var remoteReference = Repository.ParseReference(reference);
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryManifest();
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        request.Headers.Accept.ParseAdd(Repository.ManifestAcceptHeader());
        request.AddHeaders(options.Headers);
        using var response = await Repository.Options.Client.SendAsync(
            request,
            partitionId: Repository.Options.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await response.GenerateDescriptorAsync(remoteReference, Repository.Options.MaxMetadataBytes, cancellationToken).ConfigureAwait(false),
            HttpStatusCode.NotFound => throw new NotFoundException($"Reference {reference} not found"),
            _ => throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// Tags a manifest descriptor with a reference string.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        using var contentStream = await FetchAsync(descriptor, cancellationToken).ConfigureAwait(false);
        await DoPushAsync(descriptor, contentStream, remoteReference, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the manifest content identified by the descriptor.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
        => await DeleteWithIndexing(target, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// DeleteWithIndexing deletes the specified target (Descriptor) from the repository,
    /// handling referrer indexing if necessary.
    /// </summary>
    /// <param name="target">The target descriptor to delete.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed. Defaults to default.</param>
    /// <returns></returns>
    private async Task DeleteWithIndexing(Descriptor target, CancellationToken cancellationToken = default)
    {
        switch (target.MediaType)
        {
            case MediaType.ImageManifest:
            case MediaType.ImageIndex:
                if (Repository.ReferrersState == Referrers.ReferrersState.Supported)
                {
                    // referrers API is available, no client-side indexing needed
                    await Repository.DeleteAsync(target, true, cancellationToken).ConfigureAwait(false);
                    return;
                }

                target.LimitSize(Repository.Options.MaxMetadataBytes);
                var manifest = await Repository.FetchAllAsync(target, cancellationToken).ConfigureAwait(false);
                using (var manifestStream = new MemoryStream(manifest))
                {
                    await IndexReferrersForDelete(target, manifestStream, cancellationToken).ConfigureAwait(false);
                }
                break;
        }
        await Repository.DeleteAsync(target, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// IndexReferrersForDelete indexes referrers for manifests with a subject field on manifest delete.
    /// References:
    ///   - Latest spec: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#deleting-manifests
    /// </summary>
    /// <param name="target"></param>
    /// <param name="manifestContent"></param>
    /// <param name="cancellationToken"></param>
    private async Task IndexReferrersForDelete(Descriptor target, Stream manifestContent, CancellationToken cancellationToken = default)
    {
        Descriptor subject;
        switch (target.MediaType)
        {
            case MediaType.ImageManifest:
                var imageManifest = await OciJsonSerializer
                    .DeserializeAsync<Manifest>(manifestContent, cancellationToken)
                    .ConfigureAwait(false);
                if (imageManifest?.Subject == null)
                {
                    // no subject, no indexing needed
                    return;
                }
                subject = imageManifest.Subject;
                break;
            case MediaType.ImageIndex:
                var imageIndex = await OciJsonSerializer
                    .DeserializeAsync<Index>(manifestContent, cancellationToken)
                    .ConfigureAwait(false);
                if (imageIndex?.Subject == null)
                {
                    // no subject, no indexing needed
                    return;
                }
                subject = imageIndex.Subject;
                break;
            default:
                return;
        }

        var isReferrersSupported = await Repository.PingReferrersAsync(cancellationToken).ConfigureAwait(false);
        if (isReferrersSupported)
        {
            // referrers API is available, no client-side indexing needed
            return;
        }
        await UpdateReferrersIndex(subject, new Referrers.ReferrerChange(target, Referrers.ReferrerOperation.Delete), cancellationToken)
            .ConfigureAwait(false);
    }
}
