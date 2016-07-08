using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    public class Processor : IProcessor
    {
        private const int DefaultBufferSize = 8 * 1024 * 1024;
        private const int DefaultFlushThreshold = 8 * 1024 * 1024;
        private readonly IReadOnlyList<IOperationProvider> _operations;

        private Processor(EngineConfig config, IReadOnlyList<IOperationProvider> operations)
        {
            Config = config;
            _operations = operations;
        }

        public EngineConfig Config { get; }

        public static IProcessor Create(EngineConfig config, params IOperationProvider[] operations)
        {
            return new Processor(config, operations);
        }

        public static IProcessor Create(EngineConfig config, IReadOnlyList<IOperationProvider> operations)
        {
            return new Processor(config, operations);
        }

        public bool Run(Stream source, Stream target) => Run(source, target, DefaultBufferSize);

        public bool Run(Stream source, Stream target, int bufferSize) => Run(source, target, bufferSize, DefaultFlushThreshold);

        public bool Run(Stream source, Stream target, int bufferSize, int flushThreshold)
        {
            ProcessorState state = new ProcessorState(source, target, bufferSize, flushThreshold, Config, _operations);
            return state.Run();
        }
    }
}
