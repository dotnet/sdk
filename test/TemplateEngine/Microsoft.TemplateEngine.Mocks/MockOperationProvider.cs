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

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            return _operation;
        }

        public string Id => _operation.Id;
    }
}
