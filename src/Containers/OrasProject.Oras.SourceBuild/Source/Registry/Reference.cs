// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Reference.cs
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
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OrasProject.Oras.Registry;

/// <summary>
/// Reference represents a reference to a registry, repository, and content within the repository.
/// 
/// This class provides functionality to parse and validate references, which can be in the form of a tag or digest.
/// It also provides properties to access the registry, repository, and content reference details.
/// 
/// Note: Use the <see cref="Parse"/> method to create an instance of this class from a string reference.
/// </summary>
public partial class Reference
{
    private string _registry;
    private string? _repository;

    // This can be the tag or digest part of the reference.
    private string? _reference;

    /// <summary>
    /// ContentReferenceType is used to identify the type of <see cref="ContentReference"/>
    /// </summary>
    private enum ContentReferenceType
    {
        Empty,
        Tag,
        Digest
    }

    private ContentReferenceType _contentReferenceType;

    /// <summary>
    /// repositoryRegexp is adapted from the distribution implementation. The
    /// repository name set under OCI distribution spec is a subset of the docker
    /// repositoryRegexp is adapted from the distribution implementation. The
    /// spec. For maximum compatability, the docker spec is verified client-side.
    /// Further checks are left to the server-side.
    /// References:
    /// - https://github.com/distribution/distribution/blob/v2.7.1/reference/regexp.go#L53
    /// - https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#pulling-manifests
    /// </summary>
    [GeneratedRegex(@"^[a-z0-9]+(?:(?:[._]|__|[-]*)[a-z0-9]+)*(?:/[a-z0-9]+(?:(?:[._]|__|[-]*)[a-z0-9]+)*)*$", RegexOptions.Compiled)]
    private static partial Regex RepositoryRegex();

    /// <summary>
    /// tagRegexp checks the tag name.
    /// The docker and OCI spec have the same regular expression.
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#pulling-manifests
    /// </summary>
    [GeneratedRegex(@"^[\w][\w.-]{0,127}$", RegexOptions.Compiled)]
    private static partial Regex TagRegex();

    /// <summary>
    /// Registry is the name of the registry. It is usually the domain name of the registry optionally with a port.
    /// </summary>
    public string Registry
    {
        get => _registry;
        set => _registry = ValidateRegistry(value);
    }

    /// <summary>
    /// Repository is the name of the repository.
    /// </summary>
    public string? Repository
    {
        get => _repository;
        set => _repository = value == null ? null : ValidateRepository(value);
    }

    /// <summary>
    /// ContentReference is the reference of the object in the repository.
    /// This can be a digest or a tag.
    /// The property can be set to null to indicate an empty content reference.
    /// </summary>
    /// <exception cref="InvalidReferenceException">
    /// If the ContentReference is not a digest.
    /// </exception>
    public string? ContentReference
    {
        get => _reference;
        set
        {
            if (value == null)
            {
                _reference = value;
                _contentReferenceType = ContentReferenceType.Empty;
                return;
            }

            if (value.Contains(':'))
            {
                _reference = ValidateReferenceAsDigest(value);
                _contentReferenceType = ContentReferenceType.Digest;
                return;
            }

            _reference = ValidateReferenceAsTag(value);
            _contentReferenceType = ContentReferenceType.Tag;
        }
    }

    /// <summary>
    /// Host returns the host name of the registry
    /// </summary>
    public string Host => _registry == "docker.io" ? "registry-1.docker.io" : _registry;

    /// <summary>
    /// Gets the digest component of the reference.
    /// This property returns the digest part of the reference if it exists.
    /// </summary>
    /// <exception cref="InvalidReferenceException">If the reference is not a digest.</exception>
    public string Digest
    {
        get
        {
            EnsureNotEmpty();

            if (_contentReferenceType != ContentReferenceType.Digest)
            {
                throw new InvalidReferenceException("Not a digest");
            }

            return _reference!;
        }
    }

    /// <summary>
    /// Gets the Tag component of the reference.
    /// This property returns the tag part of the reference if it exists.
    /// </summary>
    /// <exception cref="InvalidReferenceException">If the reference is not a tag.</exception>
    public string Tag
    {
        get
        {
            EnsureNotEmpty();

            if (_contentReferenceType != ContentReferenceType.Tag)
            {
                throw new InvalidReferenceException("Not a tag");
            }

            return _reference!;
        }
    }

    /// <summary>
    /// Used only internally to create an empty reference 
    /// </summary>
    private Reference()
    {
        _registry = string.Empty;
        _repository = null;
        _reference = null;
        _contentReferenceType = ContentReferenceType.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reference"/> class by copying another instance.
    /// </summary>
    /// <param name="other">The other <see cref="Reference"/> instance to copy.</param>
    /// <exception cref="ArgumentNullException">If the other instance is null.</exception>
    public Reference(Reference other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _registry = other._registry;
        _repository = other._repository;
        _reference = other._reference;
        _contentReferenceType = other._contentReferenceType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reference"/> class with the specified registry.
    /// </summary>
    /// <param name="registry">The registry name.</param>
    /// <exception cref="InvalidReferenceException">If the registry name is invalid.</exception>
    public Reference(string registry) => _registry = ValidateRegistry(registry);

    /// <summary>
    /// Initializes a new instance of the <see cref="Reference"/> class with the specified registry and repository.
    /// </summary>
    /// <param name="registry">The registry name.</param>
    /// <param name="repository">The repository name.</param>
    /// <exception cref="InvalidReferenceException">If the registry or repository name is invalid.</exception>
    public Reference(string registry, string? repository) : this(registry)
        => _repository = ValidateRepository(repository);

    /// <summary>
    /// Initializes a new instance of the <see cref="Reference"/> class with the specified registry, repository, and reference.
    /// </summary>
    /// <param name="registry">The registry name.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="reference">The reference name.</param>
    /// <exception cref="InvalidReferenceException">If the registry, repository, or reference name is invalid.</exception>
    public Reference(string registry, string? repository, string? reference) : this(registry, repository)
        => ContentReference = reference;

    /// <summary>
    /// Parses the given reference string into a <see cref="Reference"/> object.
    /// The reference string can be in one of the following forms:
    /// <list type="bullet">
    /// <item>
    /// <description><c>&lt;registry&gt;/&lt;repository&gt;:&lt;tag&gt;</c> - A repository with a tag.</description>
    /// </item>
    /// <item>
    /// <description><c>&lt;registry&gt;/&lt;repository&gt;@&lt;digest&gt;</c> - A repository with a digest.</description>
    /// </item>
    /// <item>
    /// <description><c>&lt;registry&gt;/&lt;repository&gt;</c> - A repository without a tag or digest.</description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="reference">The reference string to parse.</param>
    /// <returns>The parsed <see cref="Reference"/> object.</returns>
    /// <exception cref="InvalidReferenceException">If the reference string is invalid.</exception>
    public static Reference Parse(string reference)
    {
        return TryParseReference(reference, out var parsedReference, out string error) ?
            parsedReference :
            throw new InvalidReferenceException(error);
    }

    /// <summary>
    /// Tries to parse the given reference string into a <see cref="Reference"/> object.
    /// </summary>
    /// <param name="reference">The reference string to parse.</param>
    /// <param name="parsedReference">When this method returns, contains the parsed <see cref="Reference"/> object if the parsing succeeded, or null if it failed.</param>
    /// <returns>True if the parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string reference, [NotNullWhen(true)] out Reference? parsedReference)
    {
        return TryParseReference(reference, out parsedReference, out string _);
    }

    private static bool TryParseReference(string reference,
                                            [NotNullWhen(true)] out Reference? parsedReference,
                                            out string error)
    {
        parsedReference = null;
        var parts = reference.Split('/', 2);

        if (parts.Length == 1)
        {
            error = "Missing repository";
            return false;
        }

        // Check for registry part
        var registry = parts[0];
        if (!TryValidateRegistry(registry, out error))
        {
            return false;
        }

        var path = parts[1]; // repository:tag or repository@digest

        // Check for digest for form <repository>@<digest>
        var index = path.IndexOf('@');
        if (index != -1)
        {
            var repository = path[..index];
            var contentReference = path[(index + 1)..]; // digest part
            if (!Content.Digest.TryValidate(contentReference, out error))
            {
                return false;
            }

            // Check if the repository contains a tag
            // for <repository>:<tag>@<digest> form
            var tagIndex = repository.IndexOf(':');
            if (tagIndex != -1)
            {
                // If the repository contains a tag, remove it
                // and validate repository name without the tag.
                repository = repository[..tagIndex];
            }

            if (!TryValidateRepository(repository, out error))
            {
                return false;
            }

            var instance = new Reference()
            {
                _registry = registry,
                _repository = repository,
                _reference = contentReference,
                _contentReferenceType = ContentReferenceType.Digest
            };

            parsedReference = instance;
            error = string.Empty;
            return true;
        }

        // Check for tag for form <repository>:<tag>
        index = path.IndexOf(':');
        if (index != -1)
        {
            var repository = path[..index];
            if (!TryValidateRepository(repository, out error))
            {
                return false;
            }

            var contentReference = path[(index + 1)..]; // tag part
            if (!TryValidateTag(contentReference, out error))
            {
                return false;
            }

            var instance = new Reference()
            {
                _registry = registry,
                _repository = repository,
                _reference = contentReference,
                _contentReferenceType = ContentReferenceType.Tag
            };
            parsedReference = instance;
            error = string.Empty;
            return true;
        }

        // No tag or digest and only respository name
        if (!TryValidateRepository(path, out error))
        {
            return false;
        }

        parsedReference = new Reference()
        {
            _registry = registry,
            _repository = path,
            _contentReferenceType = ContentReferenceType.Empty
        };

        error = string.Empty;
        return true;
    }

    private void EnsureNotEmpty()
    {
        if (_contentReferenceType == ContentReferenceType.Empty)
        {
            throw new InvalidReferenceException("Empty reference");
        }
    }

    private static string ValidateRegistry(string registry)
    {
        return TryValidateRegistry(registry, out string error) ?
            registry : throw new InvalidReferenceException(error);
    }

    private static bool TryValidateRegistry(string registry, out string error)
    {
        if (string.IsNullOrEmpty(registry))
        {
            error = "Registry is null or empty";
            return false;
        }

        string uriStringWithScheme = "dummy://" + registry;
        if (!Uri.IsWellFormedUriString(uriStringWithScheme, UriKind.Absolute))
        {
            error = $"Invalid registry: {registry} - not a valid URI";
            return false;

        }

        // Check if the authority part of the URI matches the registry
        // This is a workaround for the fact that Uri does not support
        // validating the authority part of the URI when the scheme is not
        // recognized. We add a dummy scheme to the URI to validate it.
        // The authority part of the URI is the part after the scheme and
        // before the path. For example, in the URI "dummy://registry/repo",
        // the authority part is "registry".
        // "foo@example.com" would be an example of where authority is not 
        // the the registry value provided.
        if (new Uri(uriStringWithScheme).Authority != registry)
        {
            error = $"Invalid registry: {registry} - not a valid authority";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string? ValidateRepository(string? repository)
    {
        return TryValidateRepository(repository, out string error) ?
            repository : throw new InvalidReferenceException(error);
    }

    private static bool TryValidateRepository(string? repository, out string error)
    {
        if (string.IsNullOrEmpty(repository))
        {
            error = "Repository is null or empty";
            return false;
        }
        if (!RepositoryRegex().IsMatch(repository))
        {
            error = $"Invalid repository: {repository}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string ValidateReferenceAsDigest(string reference) => Content.Digest.Validate(reference);

    private static string ValidateReferenceAsTag(string reference)
    {
        return TryValidateTag(reference, out string error) ?
            reference : throw new InvalidReferenceException(error);
    }

    private static bool TryValidateTag(string reference, out string error)
    {
        if (string.IsNullOrEmpty(reference))
        {
            error = "Tag is null or empty";
            return false;
        }
        if (!TagRegex().IsMatch(reference))
        {
            error = $"Invalid tag: {reference}";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
