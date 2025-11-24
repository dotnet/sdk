// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

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
                    return LaunchProfileSettings.Failure(CliCommandStrings.LaunchProfilesCollectionIsNotAJsonObject);
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
                        throw new GracefulException(CliCommandStrings.DuplicateCaseInsensitiveLaunchProfileNames,
                            string.Join(",\n", caseInsensitiveProfileMatches.Select(p => $"\t{p.Name}").ToArray()));
                    }
                    else if (!caseInsensitiveProfileMatches.Any())
                    {
                        return LaunchProfileSettings.Failure(string.Format(CliCommandStrings.LaunchProfileDoesNotExist, profileName));
                    }
                    else
                    {
                        profileObject = profilesObject.GetProperty(caseInsensitiveProfileMatches.First().Name);
                    }

                    if (profileObject.ValueKind != JsonValueKind.Object)
                    {
                        return LaunchProfileSettings.Failure(CliCommandStrings.LaunchProfileIsNotAJsonObject);
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
                    return LaunchProfileSettings.Failure(CliCommandStrings.UsableLaunchProfileCannotBeLocated);
                }

                if (!profileObject.TryGetProperty(CommandNameKey, out var finalCommandNameElement)
                    || finalCommandNameElement.ValueKind != JsonValueKind.String)
                {
                    return LaunchProfileSettings.Failure(CliCommandStrings.UsableLaunchProfileCannotBeLocated);
                }

                string? commandName = finalCommandNameElement.GetString();
                if (!TryLocateHandler(commandName, out LaunchProfileParser? provider))
                {
                    return LaunchProfileSettings.Failure(string.Format(CliCommandStrings.LaunchProfileHandlerCannotBeLocated, commandName));
                }

                return provider.ParseProfile(launchSettingsPath, selectedProfileName, profileObject.GetRawText());
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return LaunchProfileSettings.Failure(string.Format(CliCommandStrings.DeserializationExceptionMessage, launchSettingsPath, ex.Message));
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
