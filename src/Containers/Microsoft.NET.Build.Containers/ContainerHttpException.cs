// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.Build.Containers;

internal sealed class ContainerHttpException : Exception
{
    private const string ErrorPrefix = "Containerize: error CONTAINER004:";
    string? jsonResponse;
    string? uri;
    public ContainerHttpException(string message, string? targetUri, string? jsonResp)
            : base($"{ErrorPrefix} {message}\nURI: {targetUri ?? "Unknown"}\nJson Response: {jsonResp ?? "None."}")
    {
        jsonResponse = jsonResp;
        uri = targetUri;
    }
}
