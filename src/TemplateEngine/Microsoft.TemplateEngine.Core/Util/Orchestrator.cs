using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Core.Util
{
    public class Orchestrator : IOrchestrator
    {
        public void Run(string runSpecPath, IDirectory sourceDir, string targetDir)
        {
            IGlobalRunSpec spec;
            using (Stream stream = EngineEnvironmentSettings.Host.FileSystem.OpenRead(runSpecPath))
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

            RunInternal(sourceDir, targetDir, spec);
        }

        public void Run(IGlobalRunSpec spec, IDirectory sourceDir, string targetDir)
        {
            RunInternal(sourceDir, targetDir, spec);
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

        private static List<KeyValuePair<IPathMatcher, IProcessor>> CreateFileGlobProcessors(IGlobalRunSpec spec)
        {
            List<KeyValuePair<IPathMatcher, IProcessor>> processorList = new List<KeyValuePair<IPathMatcher, IProcessor>>();

            if (spec.Special != null)
            {
                foreach (KeyValuePair<IPathMatcher, IRunSpec> runSpec in spec.Special)
                {
                    IReadOnlyList<IOperationProvider> operations = runSpec.Value.GetOperations(spec.Operations);
                    EngineConfig config = new EngineConfig(EngineConfig.DefaultWhitespaces, EngineConfig.DefaultLineEndings, spec.RootVariableCollection);
                    IProcessor processor = Processor.Create(config, operations);

                    processorList.Add(new KeyValuePair<IPathMatcher, IProcessor>(runSpec.Key, processor));
                }
            }

            return processorList;
        }

        private void RunInternal(IDirectory sourceDir, string targetDir, IGlobalRunSpec spec)
        {
            EngineConfig cfg = new EngineConfig(EngineConfig.DefaultWhitespaces, EngineConfig.DefaultLineEndings, spec.RootVariableCollection);
            IProcessor fallback = Processor.Create(cfg, spec.Operations);

            List<KeyValuePair<IPathMatcher, IProcessor>> fileGlobProcessors = CreateFileGlobProcessors(spec);

            foreach (IFile file in sourceDir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                string sourceRel = file.PathRelativeTo(sourceDir);
                string fileName = Path.GetFileName(sourceRel);

                if (fileName == spec.PlaceholderFilename)
                {   // The placeholder file should never get copied / created / processed. It just causes the dir to get created if needed.
                    // So this happens before all the include / exclude / copy checks.
                    CreateTargetDir(sourceRel, targetDir, spec);
                    continue;
                }

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

                            IReadOnlyList<IOperationProvider> locOperations;
                            spec.LocalizationOperations.TryGetValue(sourceRel, out locOperations);

                            if (!copy)
                            {
                                ProcessFile(file, sourceRel, targetDir, spec, fallback, fileGlobProcessors, locOperations);
                            }
                            else if (locOperations != null)
                            {
                                ProcessFile(file, sourceRel, targetDir, spec, fallback, Empty<KeyValuePair<IPathMatcher, IProcessor>>.List.Value, locOperations);
                            }
                            else
                            {
                                string targetPath = CreateTargetDir(sourceRel, targetDir, spec);

                                using (Stream sourceStream = file.OpenRead())
                                using (Stream targetStream = EngineEnvironmentSettings.Host.FileSystem.CreateFile(targetPath))
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

        private static string CreateTargetDir(string sourceRel, string targetDir, IGlobalRunSpec spec)
        {
            string targetRel;
            if (!spec.TryGetTargetRelPath(sourceRel, out targetRel))
            {
                targetRel = sourceRel;
            }

            string targetPath = Path.Combine(targetDir, targetRel);
            string fullTargetDir = Path.GetDirectoryName(targetPath);
            EngineEnvironmentSettings.Host.FileSystem.CreateDirectory(fullTargetDir);

            return targetPath;
        }

        private void ProcessFile(IFile sourceFile, string sourceRel, string targetDir, IGlobalRunSpec spec, IProcessor fallback, IEnumerable<KeyValuePair<IPathMatcher, IProcessor>> fileGlobProcessors, IReadOnlyList<IOperationProvider> locOperations)
        {
            IProcessor runner = fileGlobProcessors.FirstOrDefault(x => x.Key.IsMatch(sourceRel)).Value ?? fallback;
            if (runner == null)
            {
                throw new InvalidOperationException("At least one of [runner] or [fallback] cannot be null");
            }

            if (locOperations != null)
            {
                runner = runner.CloneAndAppendOperations(locOperations);
            }

            string targetRel;
            if (!spec.TryGetTargetRelPath(sourceRel, out targetRel))
            {
                targetRel = sourceRel;
            }

            string targetPath = Path.Combine(targetDir, targetRel);

            //TODO: Update context with the current file & such here

            int bufferSize;
            int flushThreshold;

            bool customBufferSize = TryGetBufferSize(sourceFile, out bufferSize);
            bool customFlushThreshold = TryGetFlushThreshold(sourceFile, out flushThreshold);
            string fullTargetDir = Path.GetDirectoryName(targetPath);
            EngineEnvironmentSettings.Host.FileSystem.CreateDirectory(fullTargetDir);

            try
            {
                using (Stream source = sourceFile.OpenRead())
                using (Stream target = EngineEnvironmentSettings.Host.FileSystem.CreateFile(targetPath))
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
