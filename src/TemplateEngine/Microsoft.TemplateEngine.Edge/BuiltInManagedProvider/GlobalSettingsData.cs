// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Installer;

namespace Microsoft.TemplateEngine.Edge.BuiltInManagedProvider
{
    /// <summary>
    /// Used just to serialize/deserilize data to/from settings.json file.
    /// </summary>
    internal sealed class GlobalSettingsData
    {
        public IReadOnlyList<TemplatePackageData> Packages { get; set; }
    }
}
