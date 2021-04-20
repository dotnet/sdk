// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IVariableConfig
    {
        IReadOnlyDictionary<string, string> Sources { get; }

        IReadOnlyList<string> Order { get; }

        string FallbackFormat { get; }

        bool Expand { get; }
    }
}
