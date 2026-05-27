// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Matching
{
    public class OperationTerminal : TerminalBase
    {
        public OperationTerminal(IOperation operation, int token, int tokenLength, int start = 0, int end = -1)
            : base(tokenLength, start, end)
        {
            Operation = operation;
            Token = token;
        }

        /// <summary>
        /// Operation to perform. The tokens that operation matches are part of <see cref="IOperation"/> itself.
        /// </summary>
        public IOperation Operation { get; }

        /// <summary>
        /// This is not an actual token to match, but index of token defined in <see cref="IOperation"/>.
        /// </summary>
        public int Token { get; }
    }
}
