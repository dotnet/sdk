using System.Text;

namespace Microsoft.TemplateEngine.Abstractions.Engine
{
    public interface IOperationProvider
    {
        IOperation GetOperation(Encoding encoding, IProcessorState processorState);
    }
}