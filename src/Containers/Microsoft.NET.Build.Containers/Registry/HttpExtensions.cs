// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.Registry;

internal static class HttpExtensions
{

    public static HttpRequestMessage AcceptManifestFormats(this HttpRequestMessage request)
    {
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new("application/json"));
        request.Headers.Accept.Add(new(SchemaTypes.DockerManifestListV2));
        request.Headers.Accept.Add(new(SchemaTypes.DockerManifestV2));
        return request;
    }

    /// <summary>
    /// servers tell us the total Range they've processed via the range headers. we use this to determine where
    /// the next chunk should start.
    /// </summary>
    public static int? ParseRangeAmount(this HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Range", out var rangeValues))
        {
            var range = rangeValues.First();
            var parts = range.Split('-', 2);
            if (parts.Length == 2 && int.TryParse(parts[1], out var amountRead))
            {
                // github returns a 0 range, this leads to bad behavior
                if (amountRead <= 0)
                {
                    return null;
                }
                return amountRead;
            }
        }
        return null;
    }

    /// <summary>
    /// the OCI-Chunk-Min-Length header can be returned on the start of a blob upload. this tells us the minimum
    /// chunk size the server will accept. we use this to help determine if we should chunk or not.
    /// </summary>
    public static int? ParseOCIChunkMinSizeAmount(this HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("OCI-Chunk-Min-Length", out var minLengthValues))
        {
            var minLength = minLengthValues.First();
            if (int.TryParse(minLength, out var amountRead))
            {
                return amountRead;
            }
        }
        return null;
    }

    /// <summary>
    /// servers send the Location header on each response, which tells us where to send the next chunk.
    /// </summary>
    public static Uri GetNextLocation(this HttpResponseMessage response)
    {
        if (response.Headers.Location is { IsAbsoluteUri: true })
        {
            return response.Headers.Location;
        }
        else
        {
            // if we don't trim the BaseUri and relative Uri of slashes, you can get invalid urls.
            // Uri constructor does this on our behalf.
            return new Uri(response.RequestMessage!.RequestUri!, response.Headers.Location?.OriginalString ?? "");
        }
    }

    public static bool IsAmazonECRRegistry(this Uri uri)
    {
        // If this the registry is to public ECR the name will contain "public.ecr.aws".
        if (uri.Authority.Contains("public.ecr.aws"))
        {
            return true;
        }

        // If the registry is to a private ECR the registry will start with an account id which is a 12 digit number and will container either
        // ".ecr." or ".ecr-" if pushed to a FIPS endpoint.
        var accountId = uri.Authority.Split('.')[0];
        if ((uri.Authority.Contains(".ecr.") || uri.Authority.Contains(".ecr-")) && accountId.Length == 12 && long.TryParse(accountId, out _))
        {
            return true;
        }

        return false;
    }
}
