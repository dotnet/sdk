// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/FetchOptions.cs
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
/// Options for fetching content by reference.
/// </summary>
/// <remarks>
/// These options apply only to reference-based fetch operations
/// (e.g., <c>FetchAsync(string reference, FetchOptions options, ...)</c>).
/// Descriptor-based fetch operations used by graph operations like
/// <c>CopyAsync</c> do not currently support per-operation options.
/// </remarks>
public class FetchOptions : RequestOptions
{
}
