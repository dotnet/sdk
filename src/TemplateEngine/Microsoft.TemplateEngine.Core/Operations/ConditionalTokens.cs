// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class ConditionalTokens
    {
        public ConditionalTokens()
        {
            IfTokens = Array.Empty<ITokenConfig>();
            ElseTokens = Array.Empty<ITokenConfig>();
            ElseIfTokens = Array.Empty<ITokenConfig>();
            EndIfTokens = Array.Empty<ITokenConfig>();
            ActionableIfTokens = Array.Empty<ITokenConfig>();
            ActionableElseTokens = Array.Empty<ITokenConfig>();
            ActionableElseIfTokens = Array.Empty<ITokenConfig>();
            ActionableOperations = Array.Empty<string>();
        }

        public IReadOnlyList<ITokenConfig> IfTokens { get; set; }

        public IReadOnlyList<ITokenConfig> ElseTokens { get; set; }

        public IReadOnlyList<ITokenConfig> ElseIfTokens { get; set; }

        public IReadOnlyList<ITokenConfig> EndIfTokens { get; set; }

        public IReadOnlyList<ITokenConfig> ActionableIfTokens { get; set; }

        public IReadOnlyList<ITokenConfig> ActionableElseTokens { get; set; }

        public IReadOnlyList<ITokenConfig> ActionableElseIfTokens { get; set; }

        public IReadOnlyList<string> ActionableOperations { get; set; }
    }
}
