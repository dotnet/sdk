using System.IO;

namespace Mutant.Chicken
{
    public interface IProcessor
    {
        bool Run(Stream source, Stream target);

        bool Run(Stream source, Stream target, int bufferSize);

        bool Run(Stream source, Stream target, int bufferSize, int flushThreshold);
    }
}