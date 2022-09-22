// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    /// <summary>
    /// Defines the sources. Corresponds to the element of "sources" JSON array.
    /// </summary>
    public sealed class ExtendedFileSource : ConditionedConfigurationElement
    {
        internal static readonly Dictionary<string, string> RenameDefaults = new Dictionary<string, string>(StringComparer.Ordinal);

        internal ExtendedFileSource() { }

        /// <summary>
        /// Defines the files to be just copied when instantiating template. Operations will not be applied to them.
        /// Can be glob. If single value is given, it can be also a filename where from to read this configuration.
        /// </summary>
        public IReadOnlyList<string> CopyOnly { get; internal init; } = Array.Empty<string>();

        /// <summary>
        /// Defines the files to be include when instantiating template. Operations will be applied to them.
        /// Can be glob. If single value is given, it can be also a filename where from to read this configuration.
        /// </summary>
        public IReadOnlyList<string> Include { get; internal init; } = Array.Empty<string>();

        /// <summary>
        /// Defines the files to be excluded when instantiating template. These files will not be processed during template instantiation.
        /// Can be glob. If single value is given, it can be also a filename where from to read this configuration.
        /// </summary>
        public IReadOnlyList<string> Exclude { get; internal init; } = Array.Empty<string>();

        /// <summary>
        /// Defines the files to be renamed when instantiating the template. The key is a source file name, the value is the final file name.
        /// </summary>
        public IReadOnlyDictionary<string, string> Rename { get; internal init; } = RenameDefaults;

        /// <summary>
        /// Defines the path to the source files to apply the settings to. The path is relative to the directory containing the .template.config folder.
        /// Default value: "./".
        /// </summary>
        public string Source { get; internal init; } = "./";

        /// <summary>
        /// Defines the path the content should be written to. The path is relative to the directory the user has specified to instantiate the template to.
        /// Default value: "./".
        /// </summary>
        public string Target { get; internal init; } = "./";

        /// <summary>
        /// A list of additional source information which gets added to the top-level source information, based on evaluation the corresponding "source.modifiers.condition".
        /// </summary>
        public IReadOnlyList<SourceModifier> Modifiers { get; internal init; } = Array.Empty<SourceModifier>();

        internal static ExtendedFileSource FromJObject(JObject jObject)
        {
            List<SourceModifier> modifiers = new List<SourceModifier>();
            ExtendedFileSource src = new ExtendedFileSource()
            {
                CopyOnly = jObject.ToStringReadOnlyList(nameof(CopyOnly)),
                Exclude = jObject.ToStringReadOnlyList(nameof(Exclude)),
                Include = jObject.ToStringReadOnlyList(nameof(Include)),
                Condition = jObject.ToString(nameof(Condition)),
                Rename = jObject.Get<JObject>(nameof(Rename))?.ToStringDictionary().ToDictionary(x => x.Key, x => x.Value) ?? RenameDefaults,
                Modifiers = modifiers,
                Source = jObject.ToString(nameof(Source)) ?? "./",
                Target = jObject.ToString(nameof(Target)) ?? "./"
            };

            foreach (JObject entry in jObject.Items<JObject>(nameof(src.Modifiers)))
            {
                SourceModifier modifier = new SourceModifier
                {
                    Condition = entry.ToString(nameof(modifier.Condition)),
                    CopyOnly = entry.ToStringReadOnlyList(nameof(CopyOnly)),
                    Exclude = entry.ToStringReadOnlyList(nameof(Exclude)),
                    Include = entry.ToStringReadOnlyList(nameof(Include)),
                    Rename = entry.Get<JObject>(nameof(Rename))?.ToStringDictionary().ToDictionary(x => x.Key, x => x.Value) ?? RenameDefaults,
                };
                modifiers.Add(modifier);
            }

            return src;
        }
    }
}
