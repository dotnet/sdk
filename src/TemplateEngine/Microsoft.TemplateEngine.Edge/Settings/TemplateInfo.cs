// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal partial class TemplateInfo : ITemplateInfo, ITemplateInfoHostJsonCache
    {
        internal const string CurrentVersion = "1.0.0.7";
        private static readonly Guid RunnableProjectGeneratorId = new Guid("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3");

#pragma warning disable CS0618 // Type or member is obsolete
        private IReadOnlyDictionary<string, ICacheTag>? _tags;
        private IReadOnlyDictionary<string, ICacheParameter>? _cacheParameters;
#pragma warning restore CS0618 // Type or member is obsolete

        internal TemplateInfo(string identity, string name, IEnumerable<string> shortNames, string mountPointUri, string configPlace)
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                throw new ArgumentException($"'{nameof(identity)}' cannot be null or whitespace.", nameof(identity));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or whitespace.", nameof(name));
            }

            if (shortNames is null)
            {
                throw new ArgumentNullException(nameof(shortNames));
            }
            if (!shortNames.Any())
            {
                throw new ArgumentException($"'{nameof(shortNames)}' should contain at least one entry.", nameof(shortNames));
            }
            if (shortNames.Any(name => string.IsNullOrWhiteSpace(name)))
            {
                throw new ArgumentException($"'{nameof(shortNames)}' should not contain empty values.", nameof(shortNames));
            }

            if (string.IsNullOrWhiteSpace(mountPointUri))
            {
                throw new ArgumentException($"'{nameof(mountPointUri)}' cannot be null or whitespace.", nameof(mountPointUri));
            }

            if (string.IsNullOrWhiteSpace(configPlace))
            {
                throw new ArgumentException($"'{nameof(configPlace)}' cannot be null or whitespace.", nameof(configPlace));
            }

            Identity = identity;
            Name = name;
            MountPointUri = mountPointUri;
            ConfigPlace = configPlace;
            ShortNameList = shortNames.ToList();
        }

        /// <summary>
        /// Localization copy-constructor.
        /// </summary>
        /// <param name="template">unlocalized template.</param>
        /// <param name="localizationInfo">localization information.</param>
        /// <param name="logger"></param>
        internal TemplateInfo(ITemplate template, ILocalizationLocator? localizationInfo, ILogger logger)
        {
            if (template is null)
            {
                throw new ArgumentNullException(nameof(template));
            }
            GeneratorId = template.GeneratorId;
            ConfigPlace = template.ConfigPlace;
            MountPointUri = template.MountPointUri;
            TagsCollection = template.TagsCollection;
            Classifications = template.Classifications;
            GroupIdentity = template.GroupIdentity;
            Precedence = template.Precedence;
            Identity = template.Identity;
            DefaultName = template.DefaultName;
            HostConfigPlace = template.HostConfigPlace;
            ThirdPartyNotices = template.ThirdPartyNotices;
            BaselineInfo = template.BaselineInfo;
            ShortNameList = template.ShortNameList;

            LocaleConfigPlace = localizationInfo?.ConfigPlace;

            Author = localizationInfo?.Author ?? template.Author;
            Description = localizationInfo?.Description ?? template.Description;

            Name = localizationInfo?.Name ?? template.Name;
            Parameters = LocalizeParameters(template, localizationInfo);

            if (template.GeneratorId == RunnableProjectGeneratorId && HostConfigPlace != null)
            {
                try
                {
                    using (var sr = new StreamReader(template.TemplateSourceRoot.FileInfo(HostConfigPlace).OpenRead()))
                    using (var jsonTextReader = new JsonTextReader(sr))
                    {
                        HostData = JObject.Load(jsonTextReader);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        LocalizableStrings.TemplateInfo_Warning_FailedToReadHostData,
                        template.MountPointUri,
                        template.HostConfigPlace);
                }
            }
        }

        [JsonProperty]
        public IReadOnlyList<ITemplateParameter> Parameters { get; private set; } = new List<ITemplateParameter>();

        [JsonProperty]
        public string MountPointUri { get; }

        [JsonProperty]
        public string? Author { get; private set; }

        [JsonProperty]
        public IReadOnlyList<string> Classifications { get; private set; } = new List<string>();

        [JsonProperty]
        public string? DefaultName { get; private set; }

        [JsonProperty]
        public string? Description { get; private set; }

        [JsonProperty]
        public string Identity { get; }

        [JsonProperty]
        public Guid GeneratorId { get; private set; }

        [JsonProperty]
        public string? GroupIdentity { get; private set; }

        [JsonProperty]
        public int Precedence { get; private set; }

        [JsonProperty]
        public string Name { get; }

        [JsonIgnore]
        [Obsolete]
        string ITemplateInfo.ShortName
        {
            get
            {
                if (ShortNameList.Count > 0)
                {
                    return ShortNameList[0];
                }

                return string.Empty;
            }
        }

        public IReadOnlyList<string> ShortNameList { get; } = new List<string>();

        [JsonIgnore]
        [Obsolete]
        IReadOnlyDictionary<string, ICacheTag> ITemplateInfo.Tags
        {
            get
            {
                if (_tags == null)
                {
                    Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();
                    foreach (KeyValuePair<string, string> tag in TagsCollection)
                    {
                        tags[tag.Key] = new CacheTag(null, null, new Dictionary<string, ParameterChoice> { { tag.Value, new ParameterChoice(null, null) } }, tag.Value);
                    }
                    foreach (ITemplateParameter parameter in Parameters.Where(p => p.DataType.Equals("choice", StringComparison.OrdinalIgnoreCase)))
                    {
                        IReadOnlyDictionary<string, ParameterChoice> choices = parameter.Choices ?? new Dictionary<string, ParameterChoice>();
                        tags[parameter.Name] = new CacheTag(parameter.DisplayName, parameter.Documentation, choices, parameter.DefaultValue);
                    }
                    return _tags = tags;
                }
                return _tags;
            }
        }

        [JsonIgnore]
        [Obsolete]
        IReadOnlyDictionary<string, ICacheParameter> ITemplateInfo.CacheParameters
        {
            get
            {
                if (_cacheParameters == null)
                {
                    Dictionary<string, ICacheParameter> cacheParameters = new Dictionary<string, ICacheParameter>();
                    foreach (ITemplateParameter parameter in Parameters.Where(p => !p.DataType.Equals("choice", StringComparison.OrdinalIgnoreCase)))
                    {
                        cacheParameters[parameter.Name] = new CacheParameter()
                        {
                             DataType = parameter.DataType,
                             DefaultValue = parameter.DefaultValue,
                             Description = parameter.Documentation,
                             DefaultIfOptionWithoutValue = parameter.DefaultIfOptionWithoutValue,
                             DisplayName = parameter.DisplayName

                        };
                    }
                    return _cacheParameters = cacheParameters;
                }
                return _cacheParameters;
            }
        }

        [JsonProperty]
        public string ConfigPlace { get; }

        [JsonProperty]
        public string? LocaleConfigPlace { get; private set; }

        [JsonProperty]
        public string? HostConfigPlace { get; private set; }

        [JsonProperty]
        public string? ThirdPartyNotices { get; private set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; private set; } = new Dictionary<string, IBaselineInfo>();

        [JsonProperty]
        public IReadOnlyDictionary<string, string> TagsCollection { get; private set; } = new Dictionary<string, string>();

        [JsonIgnore]
        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

        public JObject? HostData { get; private set; }

        public static TemplateInfo FromJObject(JObject entry)
        {
            return TemplateInfoReader.FromJObject(entry);
        }

        private static IReadOnlyList<ITemplateParameter> LocalizeParameters(ITemplateInfo template, ILocalizationLocator? localizationInfo)
        {
            //we would like to copy the parameters to format supported for serialization as we cannot be sure that ITemplateInfo supports serialization in needed format.
            List<ITemplateParameter> localizedParameters = new List<ITemplateParameter>();
            foreach (ITemplateParameter parameter in template.Parameters)
            {
                IParameterSymbolLocalizationModel? localization = null;
                Dictionary<string, ParameterChoice>? localizedChoices = null;
                if (localizationInfo != null)
                {
                    if (!localizationInfo.ParameterSymbols.TryGetValue(parameter.Name, out localization))
                    {
                        // There is no localization for this symbol. Use the symbol as is.
                        localizedParameters.Add(parameter);
                        continue;
                    }
                    if (parameter.IsChoice() && parameter.Choices != null)
                    {
                        localizedChoices = new Dictionary<string, ParameterChoice>();
                        foreach (KeyValuePair<string, ParameterChoice> templateChoice in parameter.Choices)
                        {
                            ParameterChoice localizedChoice = new ParameterChoice(
                                templateChoice.Value.DisplayName,
                                templateChoice.Value.Description);

                            if (localization.Choices.TryGetValue(templateChoice.Key, out ParameterChoiceLocalizationModel locModel))
                            {
                                localizedChoice.Localize(locModel);
                            }
                            localizedChoices.Add(templateChoice.Key, localizedChoice);
                        }
                    }
                }

                TemplateParameter localizedParameter = new TemplateParameter(
                    name: parameter.Name,
                    displayName: localization?.DisplayName ?? parameter.DisplayName,
                    description: localization?.Description ?? parameter.Description,
                    defaultValue: parameter.DefaultValue,
                    defaultIfOptionWithoutValue: parameter.DefaultIfOptionWithoutValue,
                    datatype: parameter.DataType,
                    priority: parameter.Priority,
                    type: parameter.Type,
                    choices: localizedChoices ?? parameter.Choices);

                localizedParameters.Add(localizedParameter);
            }
            return localizedParameters;
        }
    }
}
