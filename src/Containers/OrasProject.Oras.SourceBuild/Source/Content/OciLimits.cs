// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/OciLimits.cs
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

namespace OrasProject.Oras.Content;

/// <summary>
/// OciLimits defines shared constants for OCI content size limits.
/// The OCI Distribution Spec states that registries SHOULD enforce a
/// manifest size limit. 4 MiB aligns with oras-go's default.
/// See: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md
/// </summary>
internal static class OciLimits
{
    /// <summary>
    /// MaxManifestBytes specifies the default limit on the size of
    /// serialized manifests and metadata responses. 4 MiB.
    /// </summary>
    internal const long MaxManifestBytes = 4 * 1024 * 1024;
}
