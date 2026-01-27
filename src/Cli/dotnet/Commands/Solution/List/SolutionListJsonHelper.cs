// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Cli.Commands.Solution.List;


public enum SolutionListOutputFormat
{
    text = 0,
    json = 1
}

internal static class JsonHelper
{
    public static readonly JsonSerializerOptions NoEscapeSerializerOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
