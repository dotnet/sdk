// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Matching
{
    public class OperationTerminal : TerminalBase
    {
        public IOperation Operation { get; }

        public int Token { get; }
        
        public OperationTerminal(IOperation operation, int token, int tokenLength, int start = 0, int end = -1)
            : base(tokenLength, start, end)
        {
            Operation = operation;
            Token = token;
        }
    }
}
