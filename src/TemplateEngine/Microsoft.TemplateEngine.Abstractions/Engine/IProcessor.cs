using System.IO;

namespace Microsoft.TemplateEngine.Abstractions.Engine
{
    public interface IProcessor
    {
        bool Run(Stream source, Stream target);

        bool Run(Stream source, Stream target, int bufferSize);

        bool Run(Stream source, Stream target, int bufferSize, int flushThreshold);
    }
}