using System.IO;

namespace N3P.StreamReplacer
{
    public class Processor : IProcessor
    {
        private const int DefaultBufferSize = 8 * 1024 * 1024;
        private const int DefaultFlushThreshold = 8 * 1024 * 1024;
        private readonly IOperationProvider[] _operations;

        private Processor(IOperationProvider[] operations)
        {
            _operations = operations;
        }

        public static IProcessor Create(params IOperationProvider[] operations)
        {
            return new Processor(operations);
        }

        public bool Run(Stream source, Stream target)
        {
            ProcessorState state = new ProcessorState(source, target, DefaultBufferSize, DefaultFlushThreshold, _operations);
            return state.Run();
        }
    }
}
