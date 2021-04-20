// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class MarkupTokens
    {
        public MarkupTokens(ITokenConfig openOpenElementToken, ITokenConfig openCloseElementToken, ITokenConfig closeElementTagToken, ITokenConfig selfClosingElementEndToken, ITokenConfig openConditionExpression, ITokenConfig closeConditionExpression)
        {
            OpenOpenElementToken = openOpenElementToken;
            OpenCloseElementToken = openCloseElementToken;
            CloseElementTagToken = closeElementTagToken;
            SelfClosingElementEndToken = selfClosingElementEndToken;
            OpenConditionExpression = openConditionExpression;
            CloseConditionExpression = closeConditionExpression;
        }

        public ITokenConfig CloseConditionExpression { get; }

        public ITokenConfig CloseElementTagToken { get; }

        public ITokenConfig OpenCloseElementToken { get; }

        public ITokenConfig OpenConditionExpression { get; }

        public ITokenConfig OpenOpenElementToken { get; }

        public ITokenConfig SelfClosingElementEndToken { get; }
    }
}
