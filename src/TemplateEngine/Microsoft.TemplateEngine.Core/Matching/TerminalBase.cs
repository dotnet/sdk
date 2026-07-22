// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Matching
{
    public abstract class TerminalBase
    {
        protected TerminalBase(int tokenLength, int start, int end)
        {
            Start = start;
            End = end != -1 ? end : (tokenLength - 1);
            Length = tokenLength;
        }

        /// <summary>
        /// Start position of the token.
        /// </summary>
        public int Start { get; protected set; }

        /// <summary>
        /// End position of the token.
        /// </summary>
        public int End { get; protected set; }

        /// <summary>
        /// Length of the token.
        /// </summary>
        public int Length { get; }
    }
}
