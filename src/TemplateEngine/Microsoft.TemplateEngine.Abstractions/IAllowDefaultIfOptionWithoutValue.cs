// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    [Obsolete("Use DefaultIfOptionWithoutValue property of ITemplateParameter interface instead.")]
    public interface IAllowDefaultIfOptionWithoutValue
    {
        string DefaultIfOptionWithoutValue { get; set; }
    }
}
