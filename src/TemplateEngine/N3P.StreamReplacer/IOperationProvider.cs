using System.Text;

namespace N3P.StreamReplacer
{
    public interface IOperationProvider
    {
        IOperation GetOperation(Encoding encoding);
    }
}