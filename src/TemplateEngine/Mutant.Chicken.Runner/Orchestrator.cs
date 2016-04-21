using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mutant.Chicken.Runner
{
    public abstract class Orchestrator
    {
        public void Run(string runSpecPath, string sourceDir, string targetDir)
        {
            IGlobalRunSpec spec;
            using (FileStream stream = File.OpenRead(runSpecPath))
            {
                spec = RunSpecLoader(stream);
                EngineConfig config = new EngineConfig(EngineConfig.DefaultWhitespaces, EngineConfig.DefaultLineEndings, spec.RootVariableCollection);
                IProcessor processor = Processor.Create(config, spec.Operations);
                stream.Position = 0;
                using (MemoryStream ms = new MemoryStream())
                {
                    processor.Run(stream, ms);
                    ms.Position = 0;
                    spec = RunSpecLoader(ms);
                }
            }

            RunInternal(this, sourceDir, targetDir, spec);
        }

        public void Run(IGlobalRunSpec spec, string sourceDir, string targetDir)
        {
            RunInternal(this, sourceDir, targetDir, spec);
        }

        protected abstract IGlobalRunSpec RunSpecLoader(Stream runSpec);

        protected virtual bool TryGetBufferSize(string sourceFile, out int bufferSize)
        {
            bufferSize = -1;
            return false;
        }

        protected virtual bool TryGetFlushThreshold(string sourceFile, out int threshold)
        {
            threshold = -1;
            return false;
        }

        private static void RunInternal(Orchestrator self, string sourceDir, string targetDir, IGlobalRunSpec spec)
        {
            EngineConfig cfg = new EngineConfig(EngineConfig.DefaultWhitespaces, EngineConfig.DefaultLineEndings, spec.RootVariableCollection);
            IProcessor fallback = Processor.Create(cfg, spec.Operations);

            Dictionary<IPathMatcher, IProcessor> specializations = spec.Special
                .ToDictionary(
                    x => x.Key,
                    x =>
                    {
                        IReadOnlyList<IOperationProvider> operations = x.Value.GetOperations(spec.Operations);
                        EngineConfig config = new EngineConfig(EngineConfig.DefaultWhitespaces, EngineConfig.DefaultLineEndings, spec.RootVariableCollection);
                        IProcessor processor = Processor.Create(config, operations);
                        return processor;
                    });

            foreach (string filePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                foreach (IPathMatcher include in spec.Include)
                {
                    if (include.IsMatch(filePath))
                    {
                        bool excluded = false;
                        foreach (IPathMatcher exclude in spec.Exclude)
                        {
                            if (exclude.IsMatch(filePath))
                            {
                                excluded = true;
                                break;
                            }
                        }

                        if (!excluded)
                        {
                            ProcessFile(self, filePath, sourceDir, targetDir, fallback, specializations);
                        }

                        break;
                    }
                }
            }
        }

        private static void ProcessFile(Orchestrator self, string filePath, string sourceDir, string targetDir, IProcessor fallback, IReadOnlyDictionary<IPathMatcher, IProcessor> specializations)
        {
            string sourceRel = filePath.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar);
            string targetRel = sourceRel;
            IProcessor runner = specializations.FirstOrDefault(x => x.Key.IsMatch(filePath)).Value ?? fallback;
            string targetPath = Path.Combine(targetDir, targetRel);

            int bufferSize,
                flushThreshold;

            bool customBufferSize = self.TryGetBufferSize(filePath, out bufferSize);
            bool customFlushThreshold = self.TryGetFlushThreshold(filePath, out flushThreshold);
            string fullTargetDir = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(fullTargetDir);

            using (FileStream source = File.OpenRead(filePath))
            using (FileStream target = File.Create(targetPath))
            {
                if (!customBufferSize)
                {
                    runner.Run(source, target);
                }
                else
                {
                    if (!customFlushThreshold)
                    {
                        runner.Run(source, target, bufferSize);
                    }
                    else
                    {
                        runner.Run(source, target, bufferSize, flushThreshold);
                    }
                }
            }
        }
    }
}
