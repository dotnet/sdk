using System.Text;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IOperationProvider
    {
        string Id { get; }

        IOperation GetOperation(Encoding encoding, IProcessorState processorState);
    }
}
