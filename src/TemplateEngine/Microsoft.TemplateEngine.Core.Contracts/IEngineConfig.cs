// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IEngineConfig
    {
        IEngineEnvironmentSettings EnvironmentSettings { get; }

        IReadOnlyList<string> LineEndings { get; }

        string VariableFormatString { get; }

        IVariableCollection Variables { get; }

        IReadOnlyList<string> Whitespaces { get; }

        IDictionary<string, bool> Flags { get; }
    }
}
