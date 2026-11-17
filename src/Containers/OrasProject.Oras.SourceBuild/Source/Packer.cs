// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Packer.cs
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

using OrasProject.Oras.Oci;
using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Serialization;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras;

/// <summary>
/// Packer provides helpers to pack content into OCI image manifests.
/// </summary>
public static partial class Packer
{
    /// <summary>
    /// ErrInvalidDateTimeFormat is returned
    /// when "org.opencontainers.artifact.created" or "org.opencontainers.image.created" is provided,
    /// but its value is not in RFC 3339 format.
    /// Reference: https://www.rfc-editor.org/rfc/rfc3339#section-5.6
    /// </summary>
    private const string _errInvalidDateTimeFormat = "invalid date and time format";

    /// <summary>
    /// ErrMissingArtifactType is returned
    /// when ManifestVersion is Version1_1 and artifactType is empty
    /// and the config media type is set to "application/vnd.oci.empty.v1+json".
    /// </summary>
    private const string _errMissingArtifactType = "missing artifact type";

    /// <summary>
    /// MediaTypeUnknownConfig is the default config media type used when none is specified.
    /// </summary>
    public const string MediaTypeUnknownConfig = "application/vnd.unknown.config.v1+json";

    /// <summary>
    /// MediaTypeUnknownArtifact is the default artifact type used when none is specified.
    /// </summary>
    public const string MediaTypeUnknownArtifact = "application/vnd.unknown.artifact.v1";

    /// <summary>
    /// ManifestVersion represents the manifest version used for PackManifest
    /// </summary>
    public enum ManifestVersion
    {
        /// <summary>
        /// Version1_0 represents the OCI Image Manifest defined in image-spec v1.0.2.
        /// Reference: https://github.com/opencontainers/image-spec/blob/v1.0.2/manifest.md
        /// </summary>
        Version1_0 = 1,

        /// <summary>
        /// Version1_1 represents the OCI Image Manifest defined in image-spec v1.1.1.
        /// Reference: https://github.com/opencontainers/image-spec/blob/v1.1.1/manifest.md
        /// </summary>
        Version1_1 = 2
    }

    /// <summary>
    /// MediaTypeRegex checks the format of media types.
    /// References:
    /// - https://github.com/opencontainers/image-spec/blob/v1.1.1/schema/defs-descriptor.json#L7
    /// - https://datatracker.ietf.org/doc/html/rfc6838#section-4.2
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}(\+json)?$", RegexOptions.Compiled)]
    private static partial Regex MediaTypeRegex();

    /// <summary>
    /// PackManifest generates an OCI Image Manifest based on the given parameters
    /// and pushes the packed manifest to a content storage/registry using pusher.
    /// The version of the manifest to be packed is determined by manifestVersion
    /// (Recommended value: Version1_1).
    /// - If manifestVersion is <b><c>Version1_1</c></b>
    ///   artifactType MUST NOT be empty unless PackManifestOptions.ConfigDescriptor is specified.
    /// - If manifestVersion is <b><c>Version1_0</c></b>
    ///   if PackManifestOptions.ConfigDescriptor is null, artifactType will be used as the
    ///   config media type; if artifactType is empty,
    ///   "application/vnd.unknown.config.v1+json" will be used.
    ///   if PackManifestOptions.ConfigDescriptor is NOT null, artifactType will be ignored.
    ///
    /// artifactType and PackManifestOptions.ConfigDescriptor.MediaType MUST comply with RFC 6838.
    ///
    /// Each time when PackManifest is called, if a time stamp is not specified, a new time
    /// stamp is generated in the manifest annotations with the key ocispec.AnnotationCreated
    /// (i.e. "org.opencontainers.image.created"). To make PackManifest reproducible,
    /// set the key ocispec.AnnotationCreated to a fixed value in
    /// opts.Annotations. The value MUST conform to RFC 3339.
    ///
    /// If succeeded, returns a descriptor of the packed manifest.
    /// </summary>
    /// <param name="pusher"></param>
    /// <param name="version"></param>
    /// <param name="artifactType"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static async Task<Descriptor> PackManifestAsync(
            IPushable pusher,
            ManifestVersion version,
            string? artifactType,
            PackManifestOptions options = default,
            CancellationToken cancellationToken = default)
    {
        switch (version)
        {
            case ManifestVersion.Version1_0:
                return await PackManifestV1_0Async(pusher, artifactType, options, cancellationToken).ConfigureAwait(false);
            case ManifestVersion.Version1_1:
                return await PackManifestV1_1Async(pusher, artifactType, options, cancellationToken).ConfigureAwait(false);
            default:
                throw new NotSupportedException($"ManifestVersion({version}) is not supported");
        }
    }

    /// <summary>
    /// Pack version 1.0 manifest
    /// </summary>
    /// <param name="pusher"></param>
    /// <param name="artifactType"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private static async Task<Descriptor> PackManifestV1_0Async(IPushable pusher, string? artifactType, PackManifestOptions options = default, CancellationToken cancellationToken = default)
    {
        if (options.Subject != null)
        {
            throw new NotSupportedException("Subject is not supported for manifest version 1.0.");
        }

        Descriptor configDescriptor;

        if (options.Config != null)
        {
            ValidateMediaType(options.Config.MediaType);
            configDescriptor = options.Config;
        }
        else
        {
            if (string.IsNullOrEmpty(artifactType))
            {
                artifactType = MediaTypeUnknownConfig;
            }
            ValidateMediaType(artifactType);
            configDescriptor = await PushCustomEmptyConfigAsync(pusher, artifactType, options.ConfigAnnotations, cancellationToken).ConfigureAwait(false);
        }

        var annotations = EnsureAnnotationCreated(options.ManifestAnnotations, "org.opencontainers.image.created");
        var manifest = new Manifest
        {
            SchemaVersion = 2,
            MediaType = Oci.MediaType.ImageManifest,
            Config = configDescriptor,
            Layers = options.Layers ?? new List<Descriptor>(),
            Annotations = annotations
        };

        return await PushManifestAsync(pusher, manifest, manifest.MediaType, manifest.Config.MediaType, manifest.Annotations, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pack version 1.1 manifest
    /// </summary>
    /// <param name="pusher"></param>
    /// <param name="artifactType"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="MissingArtifactTypeException"></exception>
    private static async Task<Descriptor> PackManifestV1_1Async(IPushable pusher, string? artifactType, PackManifestOptions options = default, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(artifactType) && (options.Config == null || options.Config.MediaType == MediaType.EmptyJson))
        {
            throw new MissingArtifactTypeException(_errMissingArtifactType);
        }
        else if (!string.IsNullOrEmpty(artifactType))
        {
            ValidateMediaType(artifactType);
        }

        Descriptor configDescriptor;

        if (options.Config != null)
        {
            ValidateMediaType(options.Config.MediaType);
            configDescriptor = options.Config;
        }
        else
        {
            configDescriptor = Descriptor.Empty;
            var configBytes = "{}"u8.ToArray();
            await PushIfNotExistAsync(pusher, configDescriptor, configBytes, cancellationToken).ConfigureAwait(false);
        }

        IList<Descriptor> layers;
        if (options.Layers != null && options.Layers.Count > 0)
        {
            layers = options.Layers;
        }
        else
        {
            // use the empty descriptor as the single layer    
            layers = [Descriptor.Empty];
        }

        var annotations = EnsureAnnotationCreated(options.ManifestAnnotations, "org.opencontainers.image.created");

        var manifest = new Manifest
        {
            SchemaVersion = 2,
            MediaType = MediaType.ImageManifest,
            ArtifactType = artifactType,
            Subject = options.Subject,
            Config = configDescriptor,
            Layers = layers,
            Annotations = annotations
        };

        return await PushManifestAsync(pusher, manifest, manifest.MediaType, manifest.ArtifactType, manifest.Annotations, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Save manifest to local or remote storage
    /// </summary>
    /// <param name="pusher"></param>
    /// <param name="manifest"></param>
    /// <param name="mediaType"></param>
    /// <param name="artifactType"></param>
    /// <param name="annotations"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private static async Task<Descriptor> PushManifestAsync(IPushable pusher, object manifest, string mediaType, string? artifactType, IDictionary<string, string>? annotations, CancellationToken cancellationToken = default)
    {
        var manifestJson = OciJsonSerializer.SerializeToUtf8Bytes(manifest);
        var manifestDesc = Descriptor.Create(manifestJson, mediaType);
        manifestDesc.ArtifactType = artifactType;
        manifestDesc.Annotations = annotations;

        using var memoryStream = new MemoryStream(manifestJson);
        await pusher.PushAsync(manifestDesc, memoryStream, cancellationToken).ConfigureAwait(false);
        return manifestDesc;
    }

    /// <summary>
    /// Validate manifest media type
    /// </summary>
    /// <param name="mediaType"></param>
    /// <exception cref="InvalidMediaTypeException"></exception>
    private static void ValidateMediaType(string mediaType)
    {
        if (!MediaTypeRegex().IsMatch(mediaType))
        {
            throw new InvalidMediaTypeException($"{mediaType} is an invalid media type");
        }
    }

    /// <summary>
    /// Push an empty configure with unknown media type to storage
    /// </summary>
    /// <param name="pusher"></param>
    /// <param name="mediaType"></param>
    /// <param name="annotations"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private static async Task<Descriptor> PushCustomEmptyConfigAsync(IPushable pusher, string mediaType, IDictionary<string, string>? annotations, CancellationToken cancellationToken = default)
    {
        var configBytes = "{}"u8.ToArray();
        var configDescriptor = Descriptor.Create(configBytes, mediaType);
        configDescriptor.Annotations = annotations;

        await PushIfNotExistAsync(pusher, configDescriptor, configBytes, cancellationToken).ConfigureAwait(false);
        return configDescriptor;
    }

    /// <summary>
    /// Push data to local or remote storage
    /// </summary>
    /// <param name="pusher"></param>
    /// <param name="descriptor"></param>
    /// <param name="data"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private static async Task PushIfNotExistAsync(IPushable pusher, Descriptor descriptor, byte[] data, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream(data);
        await pusher.PushAsync(descriptor, memoryStream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validate the value of the key in annotations should have correct timestamp format. 
    /// If the key is missed, the key and current timestamp is added to the annotations
    /// </summary>
    /// <param name="annotations"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="InvalidDateTimeFormatException"></exception>
    private static IDictionary<string, string> EnsureAnnotationCreated(IDictionary<string, string>? annotations, string key)
    {
        if (annotations == null)
        {
            annotations = new Dictionary<string, string>();
        }

        if (annotations.TryGetValue(key, out string? value))
        {
            if (!DateTime.TryParse(value, out _))
            {
                throw new InvalidDateTimeFormatException(_errInvalidDateTimeFormat);
            }

            return annotations;
        }

        var copiedAnnotations = new Dictionary<string, string>(annotations);
        copiedAnnotations[key] = DateTime.UtcNow.ToString("o");

        return copiedAnnotations;
    }
}
