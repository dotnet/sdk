// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/UriFactory.cs
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

using OrasProject.Oras.Registry.Exceptions;
using System;
using System.Web;

namespace OrasProject.Oras.Registry.Remote;

internal class UriFactory : UriBuilder
{
    private readonly Reference _reference;
    private readonly Uri _base;

    public UriFactory(Reference reference, bool plainHttp = false)
    {
        _reference = reference;
        var scheme = plainHttp ? "http" : "https";
        _base = new Uri($"{scheme}://{_reference.Host}");
    }

    public UriFactory(RepositoryOptions options) : this(options.Reference, options.PlainHttp) { }

    /// <summary>
    /// Builds the URL for accessing the base API.
    /// Format: {scheme}://{registry}/v2/
    /// Reference: https://docs.docker.com/registry/spec/api/#base
    /// </summary>
    public Uri BuildRegistryBase() => new UriBuilder(_base)
    {
        Path = "/v2/"
    }.Uri;

    /// <summary>
    /// Builds the URL for accessing the catalog API.
    /// Format: {scheme}://{registry}/v2/_catalog
    /// Reference: https://docs.docker.com/registry/spec/api/#catalog
    /// </summary>
    public Uri BuildRegistryCatalog() => new UriBuilder(_base)
    {
        Path = "/v2/_catalog"
    }.Uri;

    /// <summary>
    /// Builds the URL for accessing the tag list API.
    /// Format: {scheme}://{registry}/v2/{repository}/tags/list
    /// Reference: https://docs.docker.com/registry/spec/api/#tags
    /// </summary>
    public Uri BuildRepositoryTagList()
    {
        var builder = NewRepositoryBaseBuilder();
        builder.Path += "/tags/list";
        return builder.Uri;
    }

    /// <summary>
    /// Builds the URL for accessing the manifest API.
    /// Format: {scheme}://{registry}/v2/{repository}/manifests/{digest_or_tag}
    /// Reference: https://docs.docker.com/registry/spec/api/#manifest
    /// </summary>
    public Uri BuildRepositoryManifest()
    {
        if (string.IsNullOrEmpty(_reference.Repository))
        {
            throw new InvalidReferenceException("Missing manifest reference");
        }
        var builder = NewRepositoryBaseBuilder();
        builder.Path += $"/manifests/{_reference.ContentReference}";
        return builder.Uri;
    }

    /// <summary>
    /// Builds the URL for accessing the blob API.
    /// Format: {scheme}://{registry}/v2/{repository}/blobs/{digest}
    /// Reference: https://docs.docker.com/registry/spec/api/#blob
    /// </summary>
    public Uri BuildRepositoryBlob()
    {
        var builder = NewRepositoryBaseBuilder();
        builder.Path += $"/blobs/{_reference.Digest}";
        return builder.Uri;
    }

    /// <summary>
    /// Builds the URL for accessing the blob upload API.
    /// Format: {scheme}://{registry}/v2/{repository}/blobs/uploads/
    /// Reference: https://docs.docker.com/registry/spec/api/#initiate-blob-upload
    /// </summary>
    public Uri BuildRepositoryBlobUpload()
    {
        var builder = NewRepositoryBaseBuilder();
        builder.Path += "/blobs/uploads/";
        return builder.Uri;
    }

    /// <summary>
    /// BuildReferrersUrl builds the URL for accessing referrers API
    /// Format: {scheme}://{registry}/v2/{repository}/referrers/{digest}
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#listing-referrers
    /// </summary>
    /// <returns>The URL for listing all referrers of the referenced content.</returns>
    public Uri BuildReferrersUrl()
    {
        return BuildReferrersUrl(string.Empty);
    }

    /// <summary>
    /// BuildReferrersUrl builds the URL for accessing referrers API
    /// Format: {scheme}://{registry}/v2/{repository}/referrers/{digest}?artifactType={artifactType}
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#listing-referrers
    /// </summary>
    /// <param name="artifactType">The artifact type used to filter the referrers; ignored when empty.</param>
    /// <returns>The URL for listing referrers of the referenced content, optionally filtered by artifact type.</returns>
    public Uri BuildReferrersUrl(string artifactType)
    {
        var builder = NewRepositoryBaseBuilder();
        builder.Path += $"/referrers/{_reference.ContentReference}";
        if (!string.IsNullOrEmpty(artifactType))
        {
            var query = HttpUtility.ParseQueryString(builder.Query);
            query.Add("artifactType", artifactType);
            builder.Query = query.ToString();
        }

        return builder.Uri;
    }

    /// <summary>
    /// Generates a UriBuilder with the base endpoint of the remote repository.
    /// Format: {scheme}://{registry}/v2/{repository}
    /// </summary>
    /// <returns>Repository-scoped base UriBuilder</returns>
    protected UriBuilder NewRepositoryBaseBuilder()
    {
        if (string.IsNullOrEmpty(_reference.Repository))
        {
            throw new InvalidReferenceException("Missing repository");
        }
        var builder = new UriBuilder(_base)
        {
            Path = $"/v2/{_reference.Repository}"
        };
        return builder;
    }
}
