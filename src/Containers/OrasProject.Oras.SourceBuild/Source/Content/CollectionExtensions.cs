// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/CollectionExtensions.cs
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

using System;
using System.Collections.Generic;

namespace OrasProject.Oras.Content;

internal static class CollectionExtensions
{
    /// <summary>
    /// Returns <paramref name="list"/> unchanged, or an empty list when it is null.
    /// </summary>
    /// <remarks>
    /// Some non-conformant registries send a JSON <c>null</c> for a collection field
    /// that the spec expects to be an array (e.g. <c>"manifests": null</c> or
    /// <c>"layers": null</c>), which System.Text.Json leaves as a null property. This
    /// lets a consuming API treat that null as "no elements" at the point of use,
    /// without mutating the deserialized model — the model stays a faithful
    /// representation of the wire, while each API decides how to treat the value.
    /// </remarks>
    internal static IList<T> NullToEmpty<T>(this IList<T>? list) => list ?? Array.Empty<T>();
}
