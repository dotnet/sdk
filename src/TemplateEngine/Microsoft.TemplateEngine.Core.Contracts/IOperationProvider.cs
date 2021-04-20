// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IOperationProvider
    {
        string Id { get; }

        IOperation GetOperation(Encoding encoding, IProcessorState processorState);
    }
}
