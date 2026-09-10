// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

/// <summary>
/// Ensures that the project has a <c>dotnet watch</c> browser tools key pair in its intermediate
/// output and reports the public half.
///
/// The browser authenticates the <c>dotnet watch</c> provider with a key that the application pins
/// at build time, so the key has to be produced by the build rather than handed to it: a provider
/// that supplied its own key would be authenticating itself. The build therefore owns the key pair,
/// writes the two halves to separate files, and only the public half is ever pinned into an
/// application asset.
///
/// The task is incremental. An existing pair is reused whenever both documents parse, have the
/// current version and algorithm, and describe the same key, which keeps the generated configuration
/// module byte identical across rebuilds and keeps downstream static web asset incrementality
/// intact. Anything else - a missing file, a truncated file, a document from an older SDK, or two
/// files that do not belong to each other - regenerates both halves.
///
/// The private half is never logged, never returned as an output, and never becomes a static web
/// asset. Both files are reported through <c>FileWrites</c> by the caller so that <c>Clean</c>
/// removes them.
/// </summary>
public class EnsureDotNetWatchBrowserToolsKey : Task
{
    /// <summary>
    /// Path of the JSON document that holds the base64 X.509 SubjectPublicKeyInfo.
    /// </summary>
    [Required]
    public string PublicKeyPath { get; set; }

    /// <summary>
    /// Path of the JSON document that holds the private key components. Consumed only by
    /// <c>dotnet watch</c>.
    /// </summary>
    [Required]
    public string PrivateKeyPath { get; set; }

    /// <summary>
    /// Base64 X.509 SubjectPublicKeyInfo of the key pair. Safe to pin into build output.
    /// </summary>
    [Output]
    public string PublicKey { get; set; }

    /// <summary>
    /// True when a new key pair had to be created. Only used for diagnostics and tests.
    /// </summary>
    [Output]
    public bool Regenerated { get; set; }

    public override bool Execute()
    {
        try
        {
            if (TryReuseExistingKeyPair(out var existingPublicKey))
            {
                PublicKey = existingPublicKey;
                Regenerated = false;
                Log.LogMessage(MessageImportance.Low, "Reusing the existing dotnet-watch browser tools key pair.");
                return true;
            }

            var (publicKey, parameters) = BrowserToolsKeyPair.Create();

            var directory = Path.GetDirectoryName(PrivateKeyPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            directory = Path.GetDirectoryName(PublicKeyPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // The private half is written first. If the process is interrupted between the two
            // writes the public document is missing, which the reuse check treats as invalid, so an
            // interrupted run can never leave a public key that no private key matches.
            File.WriteAllBytes(PrivateKeyPath, BrowserToolsKeyPair.CreatePrivateKeyDocument(publicKey, parameters));
            File.WriteAllBytes(PublicKeyPath, BrowserToolsKeyPair.CreatePublicKeyDocument(publicKey));

            PublicKey = publicKey;
            Regenerated = true;

            Log.LogMessage(MessageImportance.Low, "Generated a new dotnet-watch browser tools key pair.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The message of an IO exception contains paths only, never key material.
            Log.LogError("Unable to create the dotnet-watch browser tools key pair: {0}", ex.Message);
            return false;
        }
    }

    private bool TryReuseExistingKeyPair(out string publicKey)
    {
        publicKey = null;

        var publicDocument = BrowserToolsKeyPair.TryReadPublicKeyFile(PublicKeyPath);
        if (!publicDocument.IsValid)
        {
            Log.LogMessage(MessageImportance.Low, "Regenerating the dotnet-watch browser tools key pair because the public key document is not usable: {0}.", publicDocument.Reason);
            return false;
        }

        var privateDocument = BrowserToolsKeyPair.TryReadPrivateKeyFile(PrivateKeyPath);
        if (!privateDocument.IsValid)
        {
            Log.LogMessage(MessageImportance.Low, "Regenerating the dotnet-watch browser tools key pair because the private key document is not usable: {0}.", privateDocument.Reason);
            return false;
        }

        if (!string.Equals(publicDocument.PublicKey, privateDocument.PublicKey, StringComparison.Ordinal))
        {
            Log.LogMessage(MessageImportance.Low, "Regenerating the dotnet-watch browser tools key pair because the two documents describe different keys.");
            return false;
        }

        // The recorded public key is not trusted on its own: it is re-derived from the private
        // components so that a hand edited or partially written pair cannot pin a key that the
        // provider is unable to use.
        if (!BrowserToolsKeyPair.TryDerivePublicKey(privateDocument.Parameters, out var derivedPublicKey) ||
            !string.Equals(derivedPublicKey, publicDocument.PublicKey, StringComparison.Ordinal))
        {
            Log.LogMessage(MessageImportance.Low, "Regenerating the dotnet-watch browser tools key pair because the private key does not match the public key.");
            return false;
        }

        publicKey = publicDocument.PublicKey;
        return true;
    }
}
