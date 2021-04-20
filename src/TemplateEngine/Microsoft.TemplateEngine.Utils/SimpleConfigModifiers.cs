// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class SimpleConfigModifiers : ISimpleConfigModifiers
    {
        public string BaselineName { get; set; }
    }
}
