// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.GlobalSettings;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    /// <summary>
    /// Used just to serialize/deserilize data to/from settings.json file.
    /// </summary>
    internal sealed class GlobalSettingsData
    {
        public IReadOnlyList<TemplatePackageData> Packages { get; set; }

        /// <summary>
        /// If older TemplateEngine loads this file and save it back
        /// it will include new settings that new TemplateEngine depends on
        /// without this field, data would be lost in process of loading and saving
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken> _additionalData { get; set;  }
    }
}
