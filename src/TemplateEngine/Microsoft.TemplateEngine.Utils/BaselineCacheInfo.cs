// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class BaselineCacheInfo : IBaselineInfo
    {
        public string Description { get; set; }

        public IReadOnlyDictionary<string, string> DefaultOverrides { get; set; }
    }
}
