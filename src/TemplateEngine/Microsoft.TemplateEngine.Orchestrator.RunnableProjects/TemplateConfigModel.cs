// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    /// <summary>
    /// The class represents model of template.json.
    /// </summary>
    internal class TemplateConfigModel
    {
        private const string NameSymbolName = "name";
        private readonly ILogger? _logger;
        private IReadOnlyDictionary<string, string> _tags = new Dictionary<string, string>();
        private Dictionary<string, BaseSymbol> _symbols = new Dictionary<string, BaseSymbol>();
        private IReadOnlyList<PostActionModel> _postActions = new List<PostActionModel>();
        private string? _author;
        private string? _name;
        private string? _description;
        private string? _sourceName;

        internal TemplateConfigModel()
        {
            Symbols = Array.Empty<BaseSymbol>();
        }

        private TemplateConfigModel(JObject source, ILogger? logger, ISimpleConfigModifiers? configModifiers = null, string? filename = null)
        {
            _logger = logger;

            //TODO: improve validation not to allow null values here
            Identity = source.ToString(nameof(Identity));
            Name = source.ToString(nameof(Name));

            Author = source.ToString(nameof(Author));
            Classifications = source.ArrayAsStrings(nameof(Classifications));
            DefaultName = source.ToString(nameof(DefaultName));
            Description = source.ToString(nameof(Description)) ?? string.Empty;
            GroupIdentity = source.ToString(nameof(GroupIdentity));
            Precedence = source.ToInt32(nameof(Precedence));
            Guids = source.ArrayAsGuids(nameof(Guids));

            SourceName = source.ToString(nameof(SourceName));
            PlaceholderFilename = source.ToString(nameof(PlaceholderFilename))!;
            GeneratorVersions = source.ToString(nameof(GeneratorVersions));
            ThirdPartyNotices = source.ToString(nameof(ThirdPartyNotices));
            PreferNameDirectory = source.ToBool(nameof(PreferNameDirectory));

            ShortNameList = JTokenStringOrArrayToCollection(source.Get<JToken>("ShortName"), Array.Empty<string>());
            Forms = SetupValueFormMapForTemplate(source);

            var sources = new List<ExtendedFileSource>();
            Sources = sources;
            foreach (JObject item in source.Items<JObject>(nameof(Sources)))
            {
                ExtendedFileSource src = new ExtendedFileSource();
                sources.Add(src);
                src.CopyOnly = item.Get<JToken>(nameof(src.CopyOnly));
                src.Exclude = item.Get<JToken>(nameof(src.Exclude));
                src.Include = item.Get<JToken>(nameof(src.Include));
                src.Condition = item.ToString(nameof(src.Condition));
                src.Rename = item.Get<JObject>(nameof(src.Rename))?.ToStringDictionary().ToDictionary(x => x.Key, x => x.Value);

                List<SourceModifier> modifiers = new List<SourceModifier>();
                src.Modifiers = modifiers;
                foreach (JObject entry in item.Items<JObject>(nameof(src.Modifiers)))
                {
                    SourceModifier modifier = new SourceModifier
                    {
                        Condition = entry.ToString(nameof(modifier.Condition)),
                        CopyOnly = entry.Get<JToken>(nameof(modifier.CopyOnly)),
                        Exclude = entry.Get<JToken>(nameof(modifier.Exclude)),
                        Include = entry.Get<JToken>(nameof(modifier.Include)),
                        Rename = entry.Get<JObject>(nameof(modifier.Rename))
                    };
                    modifiers.Add(modifier);
                }

                src.Source = item.ToString(nameof(src.Source));
                src.Target = item.ToString(nameof(src.Target));
            }

            IBaselineInfo? baseline = null;
            BaselineInfo = BaselineInfoFromJObject(source.PropertiesOf("baselines"));

            if (!string.IsNullOrEmpty(configModifiers?.BaselineName))
            {
                BaselineInfo.TryGetValue(configModifiers!.BaselineName, out baseline);
            }

            Dictionary<string, BaseSymbol> symbols = new(StringComparer.Ordinal);
            // create a name symbol. If one is explicitly defined in the template, it'll override this.
            NameSymbol = SetupDefaultNameSymbol(SourceName);
            symbols[NameSymbol.Name] = NameSymbol;

            // tags are being deprecated from template configuration, but we still read them for backwards compatibility.
            // They're turned into symbols here, which eventually become tags.
            _tags = source.ToStringDictionary(StringComparer.OrdinalIgnoreCase, "tags");
            foreach (KeyValuePair<string, string> tagInfo in _tags)
            {
                if (!symbols.ContainsKey(tagInfo.Key))
                {
                    symbols[tagInfo.Key] = ParameterSymbol.FromDeprecatedConfigTag(tagInfo.Key, tagInfo.Value);
                }
            }
            foreach (JProperty prop in source.PropertiesOf(nameof(Symbols)))
            {
                if (prop.Value is not JObject obj)
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(prop.Name))
                {
                    continue;
                }

                string? defaultOverride = null;
                if (baseline?.DefaultOverrides != null)
                {
                    baseline.DefaultOverrides.TryGetValue(prop.Name, out defaultOverride);
                }

                BaseSymbol modelForSymbol = SymbolModelConverter.GetModelForObject(prop.Name, obj, logger, defaultOverride);

                if (modelForSymbol != null)
                {
                    // The symbols dictionary comparer is Ordinal, making symbol names case-sensitive.
                    if (string.Equals(prop.Name, NameSymbolName, StringComparison.Ordinal)
                            && symbols.TryGetValue(prop.Name, out BaseSymbol existingSymbol)
                            && existingSymbol is ParameterSymbol existingParameterSymbol
                            && modelForSymbol is ParameterSymbol modelForParameterSymbol)
                    {
                        // "name" symbol is explicitly defined above. If it's also defined in the template.json, it gets special handling here.
                        symbols[prop.Name] = new ParameterSymbol(modelForParameterSymbol, existingParameterSymbol.Forms);
                    }
                    else
                    {
                        // last in wins (in the odd case where a template.json defined a symbol multiple times)
                        symbols[prop.Name] = modelForSymbol;
                    }
                }
            }
            foreach (var symbol in ImplicitBindSymbols)
            {
                if (!symbols.ContainsKey(symbol.Name))
                {
                    symbols[symbol.Name] = symbol;
                }
            }
            _symbols = symbols;
            _postActions = PostActionModel.LoadListFromJArray(source.Get<JArray>("PostActions"), _logger, filename);
            PrimaryOutputs = PrimaryOutputModel.ListFromJArray(source.Get<JArray>(nameof(PrimaryOutputs)));

            // Custom operations at the global level
            JToken? globalCustomConfigData = source[nameof(GlobalCustomOperations)];
            if (globalCustomConfigData != null)
            {
                GlobalCustomOperations = CustomFileGlobModel.FromJObject((JObject)globalCustomConfigData, string.Empty);
            }

            // Custom operations for specials
            IReadOnlyDictionary<string, JToken> allSpecialOpsConfig = source.ToJTokenDictionary(StringComparer.OrdinalIgnoreCase, nameof(SpecialCustomOperations));
            List<CustomFileGlobModel> specialCustomSetup = new List<CustomFileGlobModel>();

            foreach (KeyValuePair<string, JToken> globConfigKeyValue in allSpecialOpsConfig)
            {
                string globName = globConfigKeyValue.Key;
                JToken globData = globConfigKeyValue.Value;

                CustomFileGlobModel globModel = CustomFileGlobModel.FromJObject((JObject)globData, globName);
                specialCustomSetup.Add(globModel);
            }

            SpecialCustomOperations = specialCustomSetup;

            List<TemplateConstraintInfo> constraints = new List<TemplateConstraintInfo>();
            foreach (JProperty prop in source.PropertiesOf(nameof(Constraints)))
            {
                if (prop.Value is not JObject obj)
                {
                    _logger?.LogWarning(LocalizableStrings.SimpleConfigModel_Error_Constraints_InvalidSyntax, nameof(Constraints).ToLowerInvariant());
                    continue;
                }

                string? type = obj.ToString(nameof(TemplateConstraintInfo.Type));
                if (string.IsNullOrWhiteSpace(type))
                {
                    _logger?.LogWarning(LocalizableStrings.SimpleConfigModel_Error_Constraints_MissingType, obj.ToString(), nameof(TemplateConstraintInfo.Type).ToLowerInvariant());
                    continue;
                }
                obj.TryGetValue(nameof(TemplateConstraintInfo.Args), StringComparison.OrdinalIgnoreCase, out JToken? args);
                constraints.Add(new TemplateConstraintInfo(type!, args.ToJSONString()));
            }
            Constraints = constraints;
        }

        public string? Author
        {
            get
            {
                return _author;
            }

            init
            {
                _author = value;
            }
        }

        public string? DefaultName { get; init; }

        public string? Description
        {
            get
            {
                return _description;
            }

            init
            {
                _description = value;
            }
        }

        public string? GroupIdentity { get; init; }

        public int Precedence { get; init; }

        public string? Name
        {
            get
            {
                return _name;
            }

            init
            {
                _name = value;
            }
        }

        public string? ThirdPartyNotices { get; init; }

        public bool PreferNameDirectory { get; init; }

        public IReadOnlyDictionary<string, string> Tags
        {
            get
            {
                return _tags;
            }

            init
            {
                _tags = value;
            }
        }

        public IReadOnlyList<string> ShortNameList { get; init; } = Array.Empty<string>();

        public IReadOnlyList<PostActionModel> PostActionModels
        {
            get
            {
                return _postActions;
            }

            init
            {
                _postActions = value;
            }
        }

        public IReadOnlyList<PrimaryOutputModel> PrimaryOutputs { get; init; } = Array.Empty<PrimaryOutputModel>();

        public string? GeneratorVersions { get; init; }

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; init; } = new Dictionary<string, IBaselineInfo>();

        public string? Identity { get; init; }

        internal IReadOnlyList<string> Classifications { get; init; } = Array.Empty<string>();

        internal IReadOnlyList<Guid> Guids { get; init; } = Array.Empty<Guid>();

        internal string? SourceName
        {
            get
            {
                return _sourceName;
            }

            init
            {
                _sourceName = value;
                NameSymbol = SetupDefaultNameSymbol(value);
                _symbols[NameSymbolName] = NameSymbol;
            }
        }

        internal IReadOnlyList<ExtendedFileSource> Sources { get; init; } = Array.Empty<ExtendedFileSource>();

        internal IReadOnlyList<TemplateConstraintInfo> Constraints { get; init; } = Array.Empty<TemplateConstraintInfo>();

        internal IEnumerable<BaseSymbol> Symbols

        {
            get
            {
                return _symbols.Values;
            }

            init
            {
                _symbols = value.ToDictionary(s => s.Name, s => s);
                _symbols[NameSymbolName] = NameSymbol;
                foreach (var symbol in ImplicitBindSymbols)
                {
                    if (!_symbols.ContainsKey(symbol.Name))
                    {
                        _symbols[symbol.Name] = symbol;
                    }
                }
            }
        }

        internal IReadOnlyDictionary<string, IValueForm> Forms { get; init; } = new Dictionary<string, IValueForm>();

        internal string? PlaceholderFilename { get; init; }

        internal CustomFileGlobModel? GlobalCustomOperations { get; init; }

        internal IReadOnlyList<CustomFileGlobModel> SpecialCustomOperations { get; init; } = Array.Empty<CustomFileGlobModel>();

        internal BaseSymbol NameSymbol { get; private set; } = SetupDefaultNameSymbol(null);

        private static IReadOnlyList<BindSymbol> ImplicitBindSymbols { get; } = SetupImplicitBindSymbols();

        internal static TemplateConfigModel FromJObject(JObject source, ILogger? logger = null, ISimpleConfigModifiers? configModifiers = null, string? filename = null)
        {
            return new TemplateConfigModel(source, logger, configModifiers, filename);
        }

        //TODO: create convertors to get proper json format if needed.
        internal JObject ToJObject()
        {
            return JObject.FromObject(this);
        }

        /// <summary>
        /// Localizes this <see cref="TemplateConfigModel"/> with given localization model.
        /// </summary>
        /// <param name="locModel">Localization model containing the localized strings.</param>
        /// <remarks>This method works on a best-effort basis. If the given model is invalid or incompatible,
        /// erroneous data will be skipped. No errors will be logged. Use <see cref="Localize(ILocalizationModel)"/>
        /// to validate localization models before calling this method.</remarks>
        internal void Localize(ILocalizationModel locModel)
        {
            _author = locModel.Author ?? Author;
            _name = locModel.Name ?? Name;
            _description = locModel.Description ?? Description;

            foreach (var postAction in _postActions)
            {
                if (postAction.Id != null && locModel.PostActions.TryGetValue(postAction.Id, out IPostActionLocalizationModel postActionLocModel))
                {
                    postAction.Localize(postActionLocModel);
                }
            }
        }

        private static IReadOnlyList<string> JTokenStringOrArrayToCollection(JToken? token, string[] defaultSet)
        {
            if (token == null)
            {
                return defaultSet;
            }

            if (token.Type == JTokenType.String)
            {
                string tokenValue = token.ToString();
                return new List<string>() { tokenValue };
            }

            return token.ArrayAsStrings();
        }

        private static BaseSymbol SetupDefaultNameSymbol(string? sourceName)
        {
            string? replaces = string.IsNullOrWhiteSpace(sourceName) ? null : sourceName;
            return new ParameterSymbol(NameSymbolName, replaces)
            {
                Description = "The default name symbol",
                DataType = "string",
                Forms = SymbolValueFormsModel.NameForms,
                Precedence = new TemplateParameterPrecedence(PrecedenceDefinition.Implicit)
            };
        }

        private static IReadOnlyDictionary<string, IValueForm> SetupValueFormMapForTemplate(JObject source)
        {
            Dictionary<string, IValueForm> formMap = new Dictionary<string, IValueForm>(StringComparer.Ordinal);

            // setup all the built-in default forms.
            // name of the form is form identifier
            // this is only possible for the forms that don't need configuration
            foreach (KeyValuePair<string, IValueFormFactory> builtInForm in ValueFormRegistry.FormLookup)
            {
                formMap[builtInForm.Key] = builtInForm.Value.Create();
            }

            // setup the forms defined by the template configuration.
            // if any have the same name as a default, the default is overridden.
            IReadOnlyDictionary<string, JToken> templateDefinedforms = source.ToJTokenDictionary(StringComparer.OrdinalIgnoreCase, nameof(Forms));

            foreach (KeyValuePair<string, JToken> form in templateDefinedforms)
            {
                if (form.Value is JObject o)
                {
                    formMap[form.Key] = ValueFormRegistry.GetForm(form.Key, o);
                }
            }

            return formMap;
        }

        private static IReadOnlyDictionary<string, IBaselineInfo> BaselineInfoFromJObject(IEnumerable<JProperty> baselineJProperties)
        {
            Dictionary<string, IBaselineInfo> allBaselines = new Dictionary<string, IBaselineInfo>();

            foreach (JProperty property in baselineJProperties)
            {
                JObject? obj = property.Value as JObject;

                if (obj == null)
                {
                    continue;
                }

                var defaultOverrides = obj.Get<JObject>(nameof(Utils.BaselineInfo.DefaultOverrides))?.ToStringDictionary() ?? new Dictionary<string, string>();

                BaselineInfo baseline = new BaselineInfo(defaultOverrides, obj.ToString(nameof(baseline.Description)));

                allBaselines[property.Name] = baseline;
            }

            return allBaselines;
        }

        private static IReadOnlyList<BindSymbol> SetupImplicitBindSymbols()
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (!isWindows)
            {
                return Array.Empty<BindSymbol>();
            }
            //on Windows we implicitly bind OS to avoid likely breaking change.
            //this environment variable is commonly used in conditions when using run script post action.
            return new[] { new BindSymbol("OS", "env:OS") };
        }
    }
}
