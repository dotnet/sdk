// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

internal static class HttpClientExtensions
{
    public static async Task DownloadFileAsync(this HttpClient client, Uri uri, string path, ITestOutputHelper outputHelper)
    {
        outputHelper.WriteLine($"Downloading {uri}");
        
        await Utilities.RetryAsync(
            async () =>
            {
                using Stream stream = await client.GetStreamAsync(uri);
                using FileStream fileStream = new(path, FileMode.OpenOrCreate);
                await stream.CopyToAsync(fileStream);
            },
            outputHelper);
    }
}
