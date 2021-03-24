// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.GlobalSettings
{
    public class TemplatePackageData
    {
        public IReadOnlyDictionary<string, string> Details { get; set; }

        public Guid InstallerId { get; set; }

        public DateTime LastChangeTime { get; set; }

        public string MountPointUri { get; set; }
    }
}
