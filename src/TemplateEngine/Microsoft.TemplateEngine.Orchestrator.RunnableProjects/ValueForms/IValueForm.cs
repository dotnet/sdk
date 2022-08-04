// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal interface IValueForm
    {
        string Identifier { get; }

        string Name { get; }

        string? Process(string? value, IReadOnlyDictionary<string, IValueForm> otherForms);
    }
}
