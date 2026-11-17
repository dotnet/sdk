// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/ResolveOptions.cs
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
/// Options for resolving references to descriptors.
/// </summary>
/// <remarks>
/// These options apply only to reference-based resolve operations
/// (e.g., <c>ResolveAsync(string reference, ResolveOptions options, ...)</c>).
/// </remarks>
public class ResolveOptions : RequestOptions
{
}
