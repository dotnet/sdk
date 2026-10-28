// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Operations
{
    internal class MarkupTokenMapping
    {
        public MarkupTokenMapping(
            int openOpenElementToken,
            int openCloseElementToken,
            int closeCloseElementToken,
            int selfClosingElementEndToken,
            int openCommentToken,
            int closeCommentToken)
        {
            OpenOpenElementToken = openOpenElementToken;
            OpenCloseElementToken = openCloseElementToken;
            CloseElementTagToken = closeCloseElementToken;
            SelfClosingElementEndToken = selfClosingElementEndToken;
            OpenCommentToken = openCommentToken;
            CloseCommentToken = closeCommentToken;
        }

        public int CloseElementTagToken { get; }

        public int OpenCloseElementToken { get; }

        public int OpenOpenElementToken { get; }

        public int SelfClosingElementEndToken { get; }

        public int OpenCommentToken { get; }

        public int CloseCommentToken { get; }
    }
}
