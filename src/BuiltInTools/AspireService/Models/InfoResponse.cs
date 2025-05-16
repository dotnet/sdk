﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.WebTools.AspireServer.Models;

/// <summary>
/// Response when asked for /info
/// </summary>
internal class InfoResponse
{
    public const string Url = "/info";

    [JsonPropertyName("protocols_supported")]
    public string[]? ProtocolsSupported { get; set; }

    public static InfoResponse Instance = new () {ProtocolsSupported = new string[]
    {
        RunSessionRequest.OurProtocolVersion,
        RunSessionRequest.SupportedProtocolVersion
    }};
}
