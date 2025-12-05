// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Microsoft.DotNet.ProjectTools;

internal sealed class LaunchSettingsManager
{
    private const string ProfilesKey = "profiles";
    private const string CommandNameKey = "commandName";
    private static readonly IReadOnlyDictionary<string, LaunchProfileParser> _providers;

    public static IEnumerable<string> SupportedProfileTypes => _providers.Keys;

    static LaunchSettingsManager()
    {
        _providers = new Dictionary<string, LaunchProfileParser>
        {
            { ProjectLaunchSettingsParser.CommandName, ProjectLaunchSettingsParser.Instance },
            { ExecutableLaunchSettingsParser.CommandName, ExecutableLaunchSettingsParser.Instance }
        };
    }

    public static LaunchProfileSettings ReadProfileSettingsFromFile(string launchSettingsPath, string? profileName = null)
    {
        try
        {
            var launchSettingsJsonContents = File.ReadAllText(launchSettingsPath);

            var jsonDocumentOptions = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            using (var document = JsonDocument.Parse(launchSettingsJsonContents, jsonDocumentOptions))
            {
                var model = document.RootElement;

                if (model.ValueKind != JsonValueKind.Object || !model.TryGetProperty(ProfilesKey, out var profilesObject) || profilesObject.ValueKind != JsonValueKind.Object)
                {
                    return LaunchProfileSettings.Failure(Resources.LaunchProfilesCollectionIsNotAJsonObject);
                }

                var selectedProfileName = profileName;
                JsonElement profileObject;
                if (string.IsNullOrEmpty(profileName))
                {
                    var firstProfileProperty = profilesObject.EnumerateObject().FirstOrDefault(IsDefaultProfileType);
                    selectedProfileName = firstProfileProperty.Value.ValueKind == JsonValueKind.Object ? firstProfileProperty.Name : null;
                    profileObject = firstProfileProperty.Value;
                }
                else // Find a profile match for the given profileName
                {
                    IEnumerable<JsonProperty> caseInsensitiveProfileMatches = [.. profilesObject
                        .EnumerateObject() // p.Name shouldn't fail, as profileObject enumerables here are only created from an existing JsonObject
                        .Where(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase))];

                    if (caseInsensitiveProfileMatches.Count() > 1)
                    {
                        return LaunchProfileSettings.Failure(string.Format(Resources.DuplicateCaseInsensitiveLaunchProfileNames,
                            string.Join(",\n", caseInsensitiveProfileMatches.Select(p => $"\t{p.Name}"))));
                    }

                    if (!caseInsensitiveProfileMatches.Any())
                    {
                        return LaunchProfileSettings.Failure(string.Format(Resources.LaunchProfileDoesNotExist, profileName));
                    }

                    profileObject = profilesObject.GetProperty(caseInsensitiveProfileMatches.First().Name);

                    if (profileObject.ValueKind != JsonValueKind.Object)
                    {
                        return LaunchProfileSettings.Failure(Resources.LaunchProfileIsNotAJsonObject);
                    }
                }

                if (profileObject.ValueKind == default)
                {
                    foreach (var prop in profilesObject.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            if (prop.Value.TryGetProperty(CommandNameKey, out var commandNameElement) && commandNameElement.ValueKind == JsonValueKind.String)
                            {
                                if (commandNameElement.GetString() is { } commandNameElementKey && _providers.ContainsKey(commandNameElementKey))
                                {
                                    profileObject = prop.Value;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (profileObject.ValueKind == default)
                {
                    return LaunchProfileSettings.Failure(Resources.UsableLaunchProfileCannotBeLocated);
                }

                if (!profileObject.TryGetProperty(CommandNameKey, out var finalCommandNameElement)
                    || finalCommandNameElement.ValueKind != JsonValueKind.String)
                {
                    return LaunchProfileSettings.Failure(Resources.UsableLaunchProfileCannotBeLocated);
                }

                string? commandName = finalCommandNameElement.GetString();
                if (!TryLocateHandler(commandName, out LaunchProfileParser? provider))
                {
                    return LaunchProfileSettings.Failure(string.Format(Resources.LaunchProfileHandlerCannotBeLocated, commandName));
                }

                return provider.ParseProfile(launchSettingsPath, selectedProfileName, profileObject.GetRawText());
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return LaunchProfileSettings.Failure(string.Format(Resources.DeserializationExceptionMessage, launchSettingsPath, ex.Message));
        }
    }

    private static bool TryLocateHandler(string? commandName, [NotNullWhen(true)]out LaunchProfileParser? provider)
    {
        if (commandName == null)
        {
            provider = null;
            return false;
        }

        return _providers.TryGetValue(commandName, out provider);
    }

    private static bool IsDefaultProfileType(JsonProperty profileProperty)
    {
        if (profileProperty.Value.ValueKind != JsonValueKind.Object
            || !profileProperty.Value.TryGetProperty(CommandNameKey, out var commandNameElement)
            || commandNameElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var commandName = commandNameElement.GetString();
        return commandName != null && _providers.ContainsKey(commandName);
    }
}
