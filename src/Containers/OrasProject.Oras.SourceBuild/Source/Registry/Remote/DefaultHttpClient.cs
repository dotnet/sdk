// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/DefaultHttpClient.cs
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
using System.Net.Http;

namespace OrasProject.Oras.Registry.Remote;

/// <summary>
/// DefaultHttpClient is to provide singleton lazy-loading HttpClient instances across the application context
/// to reduce the creation of HTTP client connection pools.
/// Provides both a standard instance and a no-redirect instance for scenarios requiring redirect control.
/// </summary>
internal class DefaultHttpClient
{
    private static readonly Lazy<HttpClient> _client =
        new(() => new HttpClient());

    /// <summary>
    /// Singleton HttpClient instance configured to not follow redirects.
    /// Used for scenarios where redirect locations need to be captured (e.g., blob location URLs).
    /// </summary>
    private static readonly Lazy<HttpClient> _noRedirectClient =
        new(() => new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }));

    internal static HttpClient Instance => _client.Value;

    /// <summary>
    /// Gets the HttpClient instance configured to not follow redirects.
    /// </summary>
    internal static HttpClient NoRedirectInstance => _noRedirectClient.Value;
}
