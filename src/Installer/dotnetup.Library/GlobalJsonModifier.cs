// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Encapsulates all logic for reading and modifying global.json files,
/// including walking the directory tree, parsing SDK version info, and
/// updating the version string while preserving formatting and encoding.
/// </summary>
public static class GlobalJsonModifier
{
    /// <summary>
    /// Searches for a global.json file starting from <paramref name="initialDirectory"/>
    /// and walking up the directory tree. Returns the parsed contents and path if found.
    /// </summary>
    public static GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory)
    {
        string? directory = initialDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            string globalJsonPath = Path.Combine(directory, "global.json");
            if (File.Exists(globalJsonPath))
            {
                using var stream = GlobalJsonFileHelper.OpenAsUtf8Stream(globalJsonPath);
                var contents = JsonSerializer.Deserialize(
                    stream,
                    GlobalJsonContentsJsonContext.Default.GlobalJsonContents);
                return new GlobalJsonInfo
                {
                    GlobalJsonPath = globalJsonPath,
                    GlobalJsonContents = contents
                };
            }

            var parent = Directory.GetParent(directory);
            if (parent == null)
            {
                break;
            }

            directory = parent.FullName;
        }

        return new GlobalJsonInfo();
    }

    /// <summary>
    /// Updates the SDK version in a global.json file. Does nothing if
    /// <paramref name="sdkVersion"/> is null or the file does not exist.
    /// </summary>
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

        var (fileText, encoding) = GlobalJsonFileHelper.ReadFileWithEncodingDetection(globalJsonPath);
        var updatedText = ReplaceGlobalJsonSdkVersion(fileText, sdkVersion);
        if (updatedText is not null)
        {
            File.WriteAllText(globalJsonPath, updatedText, encoding);
        }
    }

    /// <summary>
    /// Updates the SDK version in a global.json file only when
    /// <paramref name="latestVersion"/> is newer than the version currently
    /// recorded in the file (or when the current version cannot be parsed).
    /// </summary>
    /// <returns><see langword="true"/> if the file was updated; otherwise <see langword="false"/>.</returns>
    public static bool UpdateGlobalJsonIfNewer(string globalJsonPath, ReleaseVersion latestVersion)
    {
        string? currentVersionString = null;
        try
        {
            using var stream = GlobalJsonFileHelper.OpenAsUtf8Stream(globalJsonPath);
            var contents = JsonSerializer.Deserialize(stream, GlobalJsonContentsJsonContext.Default.GlobalJsonContents);
            currentVersionString = contents?.Sdk?.Version;
        }
        catch
        {
            // If we can't read the current version, proceed with the update.
        }

        if (currentVersionString is not null
            && ReleaseVersion.TryParse(currentVersionString, out var currentVersion)
            && latestVersion <= currentVersion)
        {
            return false;
        }

        UpdateGlobalJson(globalJsonPath, latestVersion.ToString());
        return true;
    }

    /// <summary>
    /// Merges the SDK properties required for project-local SDK resolution into global.json.
    /// Unknown root sections are preserved; comments are accepted while reading but are not preserved on write.
    /// </summary>
    internal static void ApplyLocalSdkSetup(string globalJsonPath, LocalSdkSetupSettings settings)
    {
        ArgumentNullException.ThrowIfNull(globalJsonPath);
        ArgumentNullException.ThrowIfNull(settings);

        var (root, encoding) = ReadOrCreateGlobalJsonObject(globalJsonPath);
        var sdkObject = GetOrCreateSdkObject(root, globalJsonPath);

        sdkObject["version"] = settings.SdkVersion;
        SetOrRemove(sdkObject, "rollForward", settings.RollForward, settings.UpdateRollForward);
        SetOrRemove(sdkObject, "allowPrerelease", settings.AllowPrerelease, settings.UpdateAllowPrerelease);
        sdkObject["paths"] = new JsonArray(".dotnet", "$host$");

        string updatedJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        string? directory = Path.GetDirectoryName(globalJsonPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(globalJsonPath, updatedJson, encoding);
    }

    /// <summary>
    /// Replaces the "version" value inside the "sdk" section of a global.json string,
    /// preserving all existing formatting. Uses <see cref="Utf8JsonReader"/> to find
    /// exact token positions.
    /// </summary>
    public static string? ReplaceGlobalJsonSdkVersion(string jsonText, string newVersion)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(jsonText);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        int depth = 0;
        bool inSdkObject = false;
        bool foundVersionProperty = false;
        bool nextValueIsSdk = false;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    if (nextValueIsSdk) { inSdkObject = true; nextValueIsSdk = false; }
                    foundVersionProperty = false;
                    depth++;
                    break;

                case JsonTokenType.EndObject:
                    if (inSdkObject && depth == 2) { inSdkObject = false; }
                    foundVersionProperty = false;
                    depth--;
                    break;

                case JsonTokenType.PropertyName:
                    if (depth == 1 && reader.ValueTextEquals("sdk"u8)) { nextValueIsSdk = true; }
                    else if (inSdkObject && depth == 2 && reader.ValueTextEquals("version"u8)) { foundVersionProperty = true; }
                    break;

                case JsonTokenType.String:
                    nextValueIsSdk = false;
                    if (foundVersionProperty)
                    {
                        return SpliceUtf8Token(bytes, (int)reader.TokenStartIndex, (int)reader.BytesConsumed, newVersion);
                    }
                    break;

                default:
                    nextValueIsSdk = false;
                    foundVersionProperty = false;
                    break;
            }
        }

        return null;
    }

    private static (JsonObject Root, System.Text.Encoding Encoding) ReadOrCreateGlobalJsonObject(string globalJsonPath)
    {
        if (!File.Exists(globalJsonPath))
        {
            return ([], System.Text.Encoding.UTF8);
        }

        var (fileText, encoding) = GlobalJsonFileHelper.ReadFileWithEncodingDetection(globalJsonPath);
        try
        {
            JsonNode? rootNode = JsonNode.Parse(
                fileText,
                nodeOptions: null,
                documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

            if (rootNode is JsonObject rootObject)
            {
                return (rootObject, encoding);
            }
        }
        catch (JsonException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.UserConfigurationCorrupted,
                $"The global.json file at '{globalJsonPath}' is not valid JSON: {ex.Message}",
                ex);
        }

        throw new DotnetInstallException(
            DotnetInstallErrorCode.UserConfigurationCorrupted,
            $"The global.json file at '{globalJsonPath}' must contain a JSON object.");
    }

    private static JsonObject GetOrCreateSdkObject(JsonObject root, string globalJsonPath)
    {
        if (root["sdk"] is null)
        {
            var newSdkObject = new JsonObject();
            root["sdk"] = newSdkObject;
            return newSdkObject;
        }

        if (root["sdk"] is JsonObject sdkObject)
        {
            return sdkObject;
        }

        throw new DotnetInstallException(
            DotnetInstallErrorCode.UserConfigurationCorrupted,
            $"The sdk section in global.json at '{globalJsonPath}' must be a JSON object.");
    }

    private static void SetOrRemove(JsonObject sdkObject, string propertyName, string? value, bool update)
    {
        if (!update)
        {
            return;
        }

        if (value is null)
        {
            sdkObject.Remove(propertyName);
            return;
        }

        sdkObject[propertyName] = value;
    }

    private static void SetOrRemove(JsonObject sdkObject, string propertyName, bool? value, bool update)
    {
        if (!update)
        {
            return;
        }

        if (value is null)
        {
            sdkObject.Remove(propertyName);
            return;
        }

        sdkObject[propertyName] = value;
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
