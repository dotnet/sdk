using System.IO;

namespace N3P.StreamReplacer
{
    public interface IProcessor
    {
        bool Run(Stream source, Stream target);
    }
}