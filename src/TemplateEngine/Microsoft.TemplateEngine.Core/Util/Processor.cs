// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Core.Util
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

        public IProcessor CloneAndAppendOperations(IReadOnlyList<IOperationProvider> tempOperations)
        {
            return new Processor(Config, new CombinedList<IOperationProvider>(_operations, tempOperations));
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
