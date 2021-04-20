// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Edge.BuiltInManagedProvider
{
    /// <summary>
    /// Used just to serialize/deserilize data to/from settings.json file.
    /// </summary>
    internal sealed class GlobalSettingsData
    {
        public IReadOnlyList<TemplatePackageData> Packages { get; set; }

        /// <summary>
        /// If older TemplateEngine loads this file and saves it back
        /// it will include new settings that new TemplateEngine depends on,
        /// without this field, data would be lost in process of loading and saving.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken> _additionalData { get; set; }
    }
}
