using System.IO;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IProcessor
    {
        bool Run(Stream source, Stream target);

        bool Run(Stream source, Stream target, int bufferSize);

        bool Run(Stream source, Stream target, int bufferSize, int flushThreshold);
    }
}