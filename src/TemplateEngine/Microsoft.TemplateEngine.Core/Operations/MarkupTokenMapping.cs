// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class MarkupTokenMapping
    {
        public MarkupTokenMapping(int openOpenElementToken, int openCloseElementToken, int closeCloseElementToken, int selfClosingElementEndToken)
        {
            OpenOpenElementToken = openOpenElementToken;
            OpenCloseElementToken = openCloseElementToken;
            CloseElementTagToken = closeCloseElementToken;
            SelfClosingElementEndToken = selfClosingElementEndToken;
        }

        public int CloseElementTagToken { get; }

        public int OpenCloseElementToken { get; }

        public int OpenOpenElementToken { get; }

        public int SelfClosingElementEndToken { get; }
    }
}
