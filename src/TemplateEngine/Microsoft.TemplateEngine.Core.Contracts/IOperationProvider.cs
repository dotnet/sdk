using System.Text;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IOperationProvider
    {
        IOperation GetOperation(Encoding encoding, IProcessorState processorState);
    }
}