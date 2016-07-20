using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Runner;
using Microsoft.TemplateEngine.Core;

namespace Microsoft.TemplateEngine.Edge.Runner
{
    public class Orchestrator : IOrchestrator
    {
        public void Run(string runSpecPath, IDirectory sourceDir, string targetDir)
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

        public void Run(IGlobalRunSpec spec, IDirectory sourceDir, string targetDir)
        {
            RunInternal(this, sourceDir, targetDir, spec);
        }

        protected virtual IGlobalRunSpec RunSpecLoader(Stream runSpec)
        {
            return null;
        }

        protected virtual bool TryGetBufferSize(IFile sourceFile, out int bufferSize)
        {
            bufferSize = -1;
            return false;
        }

        protected virtual bool TryGetFlushThreshold(IFile sourceFile, out int threshold)
        {
            threshold = -1;
            return false;
        }

        private static void RunInternal(Orchestrator self, IDirectory sourceDir, string targetDir, IGlobalRunSpec spec)
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

            foreach (IFile file in sourceDir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                string sourceRel = file.PathRelativeTo(sourceDir);

                foreach (IPathMatcher include in spec.Include)
                {
                    if (include.IsMatch(sourceRel))
                    {
                        bool excluded = false;
                        foreach (IPathMatcher exclude in spec.Exclude)
                        {
                            if (exclude.IsMatch(sourceRel))
                            {
                                excluded = true;
                                break;
                            }
                        }

                        if (!excluded)
                        {
                            bool copy = false;
                            foreach(IPathMatcher copyOnly in spec.CopyOnly)
                            {
                                if (copyOnly.IsMatch(sourceRel))
                                {
                                    copy = true;
                                    break;
                                }
                            }

                            if (!copy)
                            {
                                ProcessFile(self, file, sourceRel, targetDir, spec, fallback, specializations);
                            }
                            else
                            {
                                string targetRel;
                                if (!spec.TryGetTargetRelPath(sourceRel, out targetRel))
                                {
                                    targetRel = sourceRel;
                                }

                                string targetPath = Path.Combine(targetDir, targetRel);
                                string fullTargetDir = Path.GetDirectoryName(targetPath);
                                Directory.CreateDirectory(fullTargetDir);

                                using (Stream sourceStream = file.OpenRead())
                                using (Stream targetStream = File.Create(targetPath))
                                {
                                    sourceStream.CopyTo(targetStream);
                                }
                            }
                        }

                        break;
                    }
                }
            }
        }

        private static void ProcessFile(Orchestrator self, IFile sourceFile, string sourceRel, string targetDir, IGlobalRunSpec spec, IProcessor fallback, IReadOnlyDictionary<IPathMatcher, IProcessor> specializations)
        {
            IProcessor runner = specializations.FirstOrDefault(x => x.Key.IsMatch(sourceRel)).Value ?? fallback;

            string targetRel;
            if (!spec.TryGetTargetRelPath(sourceRel, out targetRel))
            {
                targetRel = sourceRel;
            }

            string targetPath = Path.Combine(targetDir, targetRel);

            //TODO: Update context with the current file & such here

            int bufferSize,
                flushThreshold;

            bool customBufferSize = self.TryGetBufferSize(sourceFile, out bufferSize);
            bool customFlushThreshold = self.TryGetFlushThreshold(sourceFile, out flushThreshold);
            string fullTargetDir = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(fullTargetDir);

            try
            {
                using (Stream source = sourceFile.OpenRead())
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
            catch(Exception ex)
            {
                throw new Exception($"Error while processing file {sourceFile.FullPath}.\nCheck InnerException for details", ex);
            }
        }
    }
}
