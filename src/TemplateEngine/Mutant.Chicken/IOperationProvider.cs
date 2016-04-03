using System.Text;

namespace Mutant.Chicken
{
    public interface IOperationProvider
    {
        IOperation GetOperation(Encoding encoding, IProcessorState processorState);
    }
}