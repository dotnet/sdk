// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Serializes <see cref="DotnetAccessMode"/> as its lowercase name (<c>none</c> / <c>shell</c> /
/// <c>everywhere</c>) via the built-in string-enum converter with the camel-case naming policy.
/// Integer values are not accepted, and any unrecognized string is surfaced as a corrupt config by
/// the caller. See the design doc (documentation/general/dotnetup/designs/dotnetup-env.md,
/// "Config schema").
/// </summary>
internal sealed class DotnetAccessModeJsonConverter : JsonStringEnumConverter<DotnetAccessMode>
{
    public DotnetAccessModeJsonConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    {
    }
}
