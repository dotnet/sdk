// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/Digest.cs
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

using OrasProject.Oras.Content.Exceptions;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace OrasProject.Oras.Content;

internal static partial class Digest
{
    // Regular expression pattern for validating digest strings
    // The pattern matches the following format:
    // <algorithm>:<base64url-encoded digest>
    // Explanation:
    // - ^[a-z0-9]+: The algorithm part, consisting of lowercase letters
    //          and digits, followed by a colon.
    // - (?:[.+_-][a-z0-9]+)*: Optional segments of a dot, plus, underscore,
    //           or hyphen followed by lowercase letters and digits.
    // - :[a-zA-Z0-9=_-]+$: The digest part, consisting of base64url-encoded
    //          characters (letters, digits, equals, underscore, hyphen).
    // For more details, refer to the distribution specification:
    // https://github.com/opencontainers/image-spec/blob/v1.1.1/descriptor.md#digests
    [GeneratedRegex(@"^[a-z0-9]+(?:[.+_-][a-z0-9]+)*:[a-zA-Z0-9=_-]+$", RegexOptions.Compiled)]
    private static partial Regex DigestRegex();

    // List of registered and supported algorithms as per the specification
    private static readonly HashSet<string> _supportedAlgorithms = ["sha256", "sha512"];

    /// <summary>
    /// Verifies the digest header and throws an exception if it is invalid.
    /// </summary>
    /// <param name="digest"></param>
    internal static string Validate(string digest)
    {
        return TryValidate(digest, out var error)
            ? digest
            : throw new InvalidDigestException(error);
    }

    internal static bool TryValidate(string digest, out string error)
    {
        if (string.IsNullOrEmpty(digest))
        {
            error = "Digest is null or empty";
            return false;
        }

        if (!DigestRegex().IsMatch(digest))
        {
            error = $"Invalid digest: {digest}";
            return false;
        }

        var algorithm = digest.Split(':')[0];
        if (!_supportedAlgorithms.Contains(algorithm))
        {
            error = $"Unrecognized, unregistered or unsupported digest algorithm: {algorithm}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Generates a SHA-256 digest from a byte array.
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    internal static string ComputeSha256(byte[] content)
    {
        var hash = SHA256.HashData(content);
        var output = $"sha256:{Convert.ToHexString(hash)}";
        return output.ToLower();
    }
}
