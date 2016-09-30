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

        private static List<KeyValuePair<IPathMatcher, IProcessor>> CreateSpecialProcessors(IGlobalRunSpec spec)
        {
            List<KeyValuePair<IPathMatcher, IProcessor>> processorList = new List<KeyValuePair<IPathMatcher, IProcessor>>();

            foreach (KeyValuePair<IPathMatcher, IRunSpec> runSpec in spec.Special)
            {
                IReadOnlyList<IOperationProvider> operations = runSpec.Value.GetOperations(spec.Operations);
                EngineConfig config = new EngineConfig(EngineConfig.DefaultWhitespaces, EngineConfig.DefaultLineEndings, spec.RootVariableCollection);
                IProcessor processor = Processor.Create(config, operations);

                processorList.Add(new KeyValuePair<IPathMatcher, IProcessor>(runSpec.Key, processor));
            }

            return processorList;
        }

        private static void RunInternal(Orchestrator self, IDirectory sourceDir, string targetDir, IGlobalRunSpec spec)
        {
            EngineConfig cfg = new EngineConfig(EngineConfig.DefaultWhitespaces, EngineConfig.DefaultLineEndings, spec.RootVariableCollection);
            IProcessor fallback = Processor.Create(cfg, spec.Operations);

            List<KeyValuePair<IPathMatcher, IProcessor>> fileGlobProcessors = CreateSpecialProcessors(spec);

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

                            // *** LOC NOTES ***
                            //
                            //// decide if we need to loc here, branch accordingly
                            //IProcessor locProcessor = null;
                            //IReadOnlyList<IOperationProvider> locOperations;
                            //if (localizedFiles.TryGetOperations(sourceRel, out locOperations))
                            //{
                            //    locProcessor = Processor.Create(cfg, locOperations);
                            //}

                            if (!copy)
                            {
                                ProcessFile(self, file, sourceRel, targetDir, spec, fallback, fileGlobProcessors);

                                // *** LOC NOTES ***
                                //ProcessFile(self, file, sourceRel, targetDir, spec, fallback, specializations, locProcessor);
                            }
                            // *** LOC NOTES ***
                            //else if (locProcessor != null)
                            //{
                            //    ProcessFile(self, file, sourceRel, targetDir, spec, null, Empty<KeyValuePair<IPathMatcher, IProcessor>>.List.Value, locProcessor);
                            //}
                            else
                            {
                                string targetPath = CreateTargetDir(sourceRel, targetDir, spec);

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

        private static string CreateTargetDir(string sourceRel, string targetDir, IGlobalRunSpec spec)
        {
            string targetRel;
            if (!spec.TryGetTargetRelPath(sourceRel, out targetRel))
            {
                targetRel = sourceRel;
            }

            string targetPath = Path.Combine(targetDir, targetRel);
            string fullTargetDir = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(fullTargetDir);

            return targetPath;
        }

        // *** LOC NOTES ***
        //
        //private static void ProcessFile(Orchestrator self, IFile sourceFile, string sourceRel, string targetDir, IGlobalRunSpec spec, IProcessor fallback, IEnumerable<KeyValuePair<IPathMatcher, IProcessor>> specializations, IProcessor locProcessor)

        private static void ProcessFile(Orchestrator self, IFile sourceFile, string sourceRel, string targetDir, IGlobalRunSpec spec, IProcessor fallback, IEnumerable<KeyValuePair<IPathMatcher, IProcessor>> fileGlobProcessors)
        {
            IProcessor runner = fileGlobProcessors.FirstOrDefault(x => x.Key.IsMatch(sourceRel)).Value ?? fallback;

            // *** LOC NOTES ***
            // TODO: append loc operations to the runner here - need new code to accomplish
            //  not exactly append - we need to leave the original as-is
            //runner = runner?.Plus(locProcessor) ?? locProcessor;

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
