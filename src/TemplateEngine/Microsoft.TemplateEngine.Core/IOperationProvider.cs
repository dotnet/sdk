using System.Text;

namespace Microsoft.TemplateEngine.Core
{
    public interface IOperationProvider
    {
        IOperation GetOperation(Encoding encoding, IProcessorState processorState);
    }
}