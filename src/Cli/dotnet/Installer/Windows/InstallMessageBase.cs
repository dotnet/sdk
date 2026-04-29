// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Cli.Installer.Windows;

/// <summary>
/// Provides common functionality for install messages.
/// </summary>
internal abstract class InstallMessageBase
{
    /// <summary>
    /// Default serialization settings for a message.
    /// </summary>
    protected static JsonSerializerSettings DefaultSerializerSettings;

    /// <summary>
    /// Serializes the message to a JSON string.
    /// </summary>
    /// <returns>The serialized message.</returns>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Newtonsoft.Json is not used in AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Newtonsoft.Json is not used in trimmed scenarios.")]
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this, DefaultSerializerSettings);
    }

    /// <summary>
    /// Converts the message to an array of UTF-8 encoded bytes.
    /// </summary>
    /// <returns>A byte array representation of the serialized message.</returns>
    public byte[] ToByteArray()
    {
        return Encoding.UTF8.GetBytes(ToString());
    }

    static InstallMessageBase()
    {
        DefaultSerializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore
        };
    }
}
