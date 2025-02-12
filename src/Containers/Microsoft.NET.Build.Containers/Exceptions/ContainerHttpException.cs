// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;

namespace Microsoft.NET.Build.Containers;

internal sealed class ContainerHttpException : Exception
{
    private const string ErrorPrefix = "Containerize: error CONTAINER004:";
    string? uri;
    public ContainerHttpException(string message, string? targetUri, HttpStatusCode? statusCode)
            : base($"{ErrorPrefix} {message}\nURI: {targetUri ?? "Unknown"}\nHTTP status code: {statusCode?.ToString() ?? "Unknown"}")
    {
        uri = targetUri;
    }
}
