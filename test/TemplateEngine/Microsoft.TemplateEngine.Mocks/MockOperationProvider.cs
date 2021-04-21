// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockOperationProvider : IOperationProvider
    {
        private readonly MockOperation _operation;

        public MockOperationProvider(MockOperation operation)
        {
            _operation = operation;
        }

        public string Id => _operation.Id;

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            return _operation;
        }
    }
}
