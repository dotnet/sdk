using System.Text;

namespace Mutant.Chicken.Core
{
    public interface IOperationProvider
    {
        IOperation GetOperation(Encoding encoding, IProcessorState processorState);
    }
}