// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public static class TemplateParameterExtensions
    {
        public static bool IsChoice(this ITemplateParameter parameter)
        {
            return parameter.DataType?.Equals("choice", StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }

}
