// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.HotReload;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.NET.Build.Tasks;

public sealed class EnsureHotReloadWebSocketKey : Task
{
    [Required]
    public string PublicKeyPath { get; set; }

    [Required]
    public string PrivateKeyPath { get; set; }

    [Output]
    public string PublicKey { get; set; }

    public override bool Execute()
    {
        try
        {
            (PublicKey, _) = HotReloadKeyPair.Ensure(PublicKeyPath, PrivateKeyPath,
                message => Log.LogMessage(MessageImportance.Low, message));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.LogErrorFromException(ex, showStackTrace: false);
            return false;
        }
    }
}
