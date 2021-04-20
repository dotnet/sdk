// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class ConditionalTokens
    {
        public ConditionalTokens()
        {
            IfTokens = Empty<ITokenConfig>.List.Value;
            ElseTokens = Empty<ITokenConfig>.List.Value;
            ElseIfTokens = Empty<ITokenConfig>.List.Value;
            EndIfTokens = Empty<ITokenConfig>.List.Value;
            ActionableIfTokens = Empty<ITokenConfig>.List.Value;
            ActionableElseTokens = Empty<ITokenConfig>.List.Value;
            ActionableElseIfTokens = Empty<ITokenConfig>.List.Value;
            ActionableOperations = Empty<string>.List.Value;
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
