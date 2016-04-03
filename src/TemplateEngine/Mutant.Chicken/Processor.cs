using System.IO;

namespace Mutant.Chicken
{
    public class Processor : IProcessor
    {
        private const int DefaultBufferSize = 8 * 1024 * 1024;
        private const int DefaultFlushThreshold = 8 * 1024 * 1024;
        private readonly IOperationProvider[] _operations;

        private Processor(EngineConfig config, IOperationProvider[] operations)
        {
            Config = config;
            _operations = operations;
        }

        public EngineConfig Config { get; }

        public static IProcessor Create(EngineConfig config, params IOperationProvider[] operations)
        {
            return new Processor(config, operations);
        }

        public bool Run(Stream source, Stream target)
        {
            ProcessorState state = new ProcessorState(source, target, DefaultBufferSize, DefaultFlushThreshold, Config, _operations);
            return state.Run();
        }
    }
}
