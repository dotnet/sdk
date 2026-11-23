// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Encapsulates all logic for reading and modifying global.json files,
/// including walking the directory tree, parsing SDK version info, and
/// updating the version string while preserving formatting and encoding.
/// </summary>
public static class GlobalJsonHelper
{
    private const int MaxMalformedDetailLength = 256;

    /// <summary>
    /// Reads UTF-8 global.json files, allowing comments but not trailing commas, like the muxer.
    /// SDK version and roll-forward validation belongs to the channel resolver.
    /// </summary>
    internal static GlobalJsonContents ReadGlobalJson(string globalJsonPath)
    {
        return ReadGlobalJsonFile(globalJsonPath).Contents;
    }

    /// <summary>
    /// Searches for a global.json file starting from <paramref name="initialDirectory"/>
    /// and walking up the directory tree. Returns the path if found and, only when
    /// <paramref name="parseContents"/> is set, the parsed contents. Callers must opt in to
    /// parsing so a malformed global.json does not fail commands that never use its contents.
    /// </summary>
    /// <exception cref="DotnetInstallException">
    /// <paramref name="parseContents"/> is set and the file cannot be read or parsed.
    /// See <see cref="WrapGlobalJsonErrors"/>.
    /// </exception>
    public static GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory, bool parseContents)
    {
        string? globalJsonPath = FindGlobalJsonPath(initialDirectory);
        if (globalJsonPath is null)
        {
            return new GlobalJsonInfo();
        }

        return new GlobalJsonInfo
        {
            GlobalJsonPath = globalJsonPath,
            GlobalJsonContents = parseContents ? WrapGlobalJsonErrors(globalJsonPath, () => ReadGlobalJson(globalJsonPath)) : null
        };
    }

    /// <summary>
    /// Runs a global.json operation, converting failures to user-facing errors.
    /// </summary>
    /// <exception cref="DotnetInstallException">
    /// <see cref="DotnetInstallErrorCode.MalformedGlobalJson"/> when the file is not valid global.json, or
    /// <see cref="DotnetInstallErrorCode.ContextResolutionFailed"/> when it cannot be read or written.
    /// </exception>
    private static T WrapGlobalJsonErrors<T>(string globalJsonPath, Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (JsonException ex)
        {
            throw CreateMalformedGlobalJsonException(globalJsonPath, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw CreateUnreadableGlobalJsonException(globalJsonPath, ex);
        }
    }

    /// <summary>Creates the user-facing error for a global.json file that cannot be read or written.</summary>
    internal static DotnetInstallException CreateUnreadableGlobalJsonException(string? globalJsonPath, Exception ex) =>
        new(DotnetInstallErrorCode.ContextResolutionFailed,
            string.Format(CultureInfo.InvariantCulture, Strings.InstallSpecRequirementError, globalJsonPath, ex.Message), ex);

    /// <summary>
    /// Creates the user-facing error for a global.json file that is not valid JSON or has invalid SDK settings.
    /// The parse detail is truncated so a large invalid value cannot flood the console.
    /// </summary>
    internal static DotnetInstallException CreateMalformedGlobalJsonException(string globalJsonPath, JsonException ex)
    {
        string detail = ex.Message.Length <= MaxMalformedDetailLength
            ? ex.Message
            : string.Concat(ex.Message.AsSpan(0, MaxMalformedDetailLength), "...");
        return new DotnetInstallException(
            DotnetInstallErrorCode.MalformedGlobalJson,
            string.Format(CultureInfo.InvariantCulture, Strings.MalformedGlobalJson, globalJsonPath, detail),
            ex);
    }

    private static string? FindGlobalJsonPath(string initialDirectory)
    {
        string? directory = initialDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            string globalJsonPath = Path.Combine(directory, "global.json");
            if (File.Exists(globalJsonPath))
            {
                return globalJsonPath;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    /// <summary>
    /// Updates the SDK version in a global.json file. Does nothing if
    /// <paramref name="sdkVersion"/> is null or the file does not exist.
    /// The file must be UTF-8 JSON with optional comments and no trailing commas.
    /// </summary>
    /// <exception cref="DotnetInstallException">
    /// The file cannot be read, parsed, or written, or contains duplicate SDK versions; it is left unchanged
    /// unless the write itself fails. See <see cref="WrapGlobalJsonErrors"/>.
    /// </exception>
    public static void UpdateGlobalJson(string globalJsonPath, string? sdkVersion = null)
    {
        if (sdkVersion is null)
        {
            return;
        }

        if (!File.Exists(globalJsonPath))
        {
            return;
        }

        WrapGlobalJsonErrors(globalJsonPath, () =>
        {
            var (_, fileText, encoding) = ReadGlobalJsonFile(globalJsonPath);
            return WriteGlobalJsonSdkVersion(globalJsonPath, sdkVersion, fileText, encoding);
        });
    }

    private static bool WriteGlobalJsonSdkVersion(string globalJsonPath, string sdkVersion, string fileText, Encoding encoding)
    {
        var updatedText = ReplaceGlobalJsonSdkVersion(fileText, sdkVersion);
        if (updatedText is null)
        {
            return false;
        }
        File.WriteAllText(globalJsonPath, updatedText, encoding);
        return true;
    }

    private static (GlobalJsonContents Contents, string Text, Encoding Encoding) ReadGlobalJsonFile(string globalJsonPath)
    {
        var bytes = File.ReadAllBytes(globalJsonPath);
        bool hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom, throwOnInvalidBytes: true);
        string text;
        try
        {
            text = encoding.GetString(bytes.AsSpan(hasBom ? Encoding.UTF8.Preamble.Length : 0));
        }
        catch (DecoderFallbackException ex)
        {
            throw new JsonException(ex.Message, ex);
        }

        var contents = JsonSerializer.Deserialize(text, GlobalJsonContentsJsonContext.Default.GlobalJsonContents)
            ?? throw new JsonException(Strings.GlobalJsonInvalidContents);
        return (contents, text, encoding);
    }

    /// <summary>
    /// Updates the SDK version in a global.json file only when
    /// <paramref name="latestVersion"/> is newer than the version currently
    /// recorded in the file (or when the current version cannot be parsed).
    /// </summary>
    /// <returns><see langword="true"/> if the file was updated; otherwise <see langword="false"/>.</returns>
    /// <exception cref="DotnetInstallException">
    /// The file cannot be read, parsed, or written, or contains duplicate SDK versions; it is left unchanged
    /// unless the write itself fails. See <see cref="WrapGlobalJsonErrors"/>.
    /// </exception>
    public static bool UpdateGlobalJsonIfNewer(string globalJsonPath, ReleaseVersion latestVersion) =>
        WrapGlobalJsonErrors(globalJsonPath, () =>
        {
            var (contents, fileText, encoding) = ReadGlobalJsonFile(globalJsonPath);
            string? currentVersionString = contents.Sdk?.Version;

            if (currentVersionString is not null
                && ReleaseVersion.TryParse(currentVersionString, out var currentVersion)
                && latestVersion <= currentVersion)
            {
                return false;
            }

            return WriteGlobalJsonSdkVersion(globalJsonPath, latestVersion.ToString(), fileText, encoding);
        });

    /// <summary>
    /// Replaces the "version" value inside the "sdk" section of a global.json string,
    /// preserving all existing formatting. Uses <see cref="Utf8JsonReader"/> to find
    /// exact token positions and validate the entire document before returning a replacement.
    /// </summary>
    public static string? ReplaceGlobalJsonSdkVersion(string jsonText, string newVersion)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(jsonText);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

        int depth = 0;
        bool inSdkObject = false;
        bool expectingVersionValue = false;
        bool seenVersionProperty = false;
        bool nextValueIsSdk = false;
        string? updatedText = null;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    if (nextValueIsSdk) { inSdkObject = true; nextValueIsSdk = false; }
                    expectingVersionValue = false;
                    depth++;
                    break;

                case JsonTokenType.EndObject:
                    if (inSdkObject && depth == 2) { inSdkObject = false; }
                    expectingVersionValue = false;
                    depth--;
                    break;

                case JsonTokenType.PropertyName:
                    if (depth == 1 && reader.ValueTextEquals("sdk"u8)) { nextValueIsSdk = true; }
                    else if (inSdkObject && depth == 2 && reader.ValueTextEquals("version"u8))
                    {
                        if (seenVersionProperty)
                        {
                            throw new JsonException(Strings.GlobalJsonDuplicateSdkVersion);
                        }
                        seenVersionProperty = true;
                        expectingVersionValue = true;
                    }
                    break;

                case JsonTokenType.String:
                    nextValueIsSdk = false;
                    if (expectingVersionValue)
                    {
                        updatedText ??= SpliceUtf8Token(bytes, (int)reader.TokenStartIndex, (int)reader.BytesConsumed, newVersion);
                        expectingVersionValue = false;
                    }
                    break;

                default:
                    nextValueIsSdk = false;
                    expectingVersionValue = false;
                    break;
            }
        }

        return updatedText;
    }

    private static string SpliceUtf8Token(byte[] bytes, int tokenStart, int bytesConsumed, string newVersion)
    {
        // TokenStartIndex is a UTF-8 byte offset, so splice on the byte array
        // rather than on the C# string to handle non-ASCII characters correctly.
        int tokenLength = bytesConsumed - tokenStart;
        byte[] replacementBytes = System.Text.Encoding.UTF8.GetBytes($"\"{newVersion}\"");

        byte[] result = new byte[bytes.Length - tokenLength + replacementBytes.Length];
        bytes.AsSpan(0, tokenStart).CopyTo(result);
        replacementBytes.CopyTo(result.AsSpan(tokenStart));
        bytes.AsSpan(tokenStart + tokenLength).CopyTo(result.AsSpan(tokenStart + replacementBytes.Length));

        return System.Text.Encoding.UTF8.GetString(result);
    }
}
