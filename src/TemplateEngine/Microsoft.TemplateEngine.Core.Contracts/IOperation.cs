// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IOperation
    {
        IReadOnlyList<IToken> Tokens { get; }

        string Id { get; }

        bool IsInitialStateOn { get; }

        int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target);
    }
}
