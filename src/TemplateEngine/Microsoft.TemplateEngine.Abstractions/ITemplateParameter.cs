// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ITemplateParameter
    {
        string Documentation { get; }

        string Name { get; }

        TemplateParameterPriority Priority { get; }

        string Type { get; }

        bool IsName { get; }

        string DefaultValue { get; }

        string DataType { get; }

        IReadOnlyDictionary<string, ParameterChoice> Choices { get; }
    }
}
