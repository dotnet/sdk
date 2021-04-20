// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IHostTemplateModel
    {
        // standard template parameter -> host specific template parameter
        IReadOnlyDictionary<string, string> ParameterMap { get; }
    }
}
