using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
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
            using (Stream stream = sourceDir.MountPoint.EnvironmentSettings.Host.FileSystem.OpenRead(runSpecPath))
            {
                spec = RunSpecLoader(stream);
                EngineConfig config = new EngineConfig(sourceDir.MountPoint.EnvironmentSettings, EngineConfig.DefaultWhitespaces, EngineConfig.DefaultLineEndings, spec.RootVariableCollection);
                IProcessor processor = Processor.Create(config, spec.Operations);
                stream.Position = 0;
                using (MemoryStream ms = new MemoryStream())
                {
                    processor.Run(stream, ms);
                    ms.Position = 0;
                    spec = RunSpecLoader(ms);
                }
            }

            RunInternal(sourceDir.MountPoint.EnvironmentSettings, sourceDir, targetDir, spec);
        }

        public IReadOnlyList<IFileChange2> GetFileChanges(string runSpecPath, IDirectory sourceDir, string targetRoot, string targetDir)
        {
            IGlobalRunSpec spec;
            using (Stream stream = sourceDir.MountPoint.EnvironmentSettings.Host.FileSystem.OpenRead(runSpecPath))
            {
                spec = RunSpecLoader(stream);
                EngineConfig config = new EngineConfig(sourceDir.MountPoint.EnvironmentSettings, EngineConfig.DefaultWhitespaces, EngineConfig.DefaultLineEndings, spec.RootVariableCollection);
                IProcessor processor = Processor.Create(config, spec.Operations);
                stream.Position = 0;
                using (MemoryStream ms = new MemoryStream())
                {
                    processor.Run(stream, ms);
                    ms.Position = 0;
                    spec = RunSpecLoader(ms);
                }
            }

            return GetFileChangesInternal(sourceDir.MountPoint.EnvironmentSettings, sourceDir, targetRoot, targetDir, spec);
        }

        public void Run(IGlobalRunSpec spec, IDirectory sourceDir, string targetDir)
        {
            RunInternal(sourceDir.MountPoint.EnvironmentSettings, sourceDir, targetDir, spec);
        }

        public IReadOnlyList<IFileChange2> GetFileChanges(IGlobalRunSpec spec, IDirectory sourceDir, string targetRoot, string targetDir)
        {
            return GetFileChangesInternal(sourceDir.MountPoint.EnvironmentSettings, sourceDir, targetRoot, targetDir, spec);
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

        private static List<KeyValuePair<IPathMatcher, IProcessor>> CreateFileGlobProcessors(IEngineEnvironmentSettings environmentSettings, IGlobalRunSpec spec)
        {
            List<KeyValuePair<IPathMatcher, IProcessor>> processorList = new List<KeyValuePair<IPathMatcher, IProcessor>>();

            if (spec.Special != null)
            {
                foreach (KeyValuePair<IPathMatcher, IRunSpec> runSpec in spec.Special)
                {
                    IReadOnlyList<IOperationProvider> operations = runSpec.Value.GetOperations(spec.Operations);
                    EngineConfig config = new EngineConfig(environmentSettings, EngineConfig.DefaultWhitespaces, EngineConfig.DefaultLineEndings, spec.RootVariableCollection, runSpec.Value.VariableFormatString);
                    IProcessor processor = Processor.Create(config, operations);

                    processorList.Add(new KeyValuePair<IPathMatcher, IProcessor>(runSpec.Key, processor));
                }
            }

            return processorList;
        }

        private IReadOnlyList<IFileChange2> GetFileChangesInternal(IEngineEnvironmentSettings environmentSettings, IDirectory sourceDir, string targetRoot, string targetDir, IGlobalRunSpec spec)
        {
            List<IFileChange2> changes = new List<IFileChange2>();
            string fullTargetPath = Path.Combine(targetRoot, targetDir);

            foreach (IFile file in sourceDir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                string sourceRel = file.PathRelativeTo(sourceDir);
                string fileName = Path.GetFileName(sourceRel);
                bool checkingDirWithPlaceholderFile = false;

                if (spec.IgnoreFileNames.Contains(fileName))
                {   // The placeholder file should never get copied / created / processed. It just causes the dir to get created if needed.
                    // The change checking / reporting is different, setting this variable tracks it.
                    checkingDirWithPlaceholderFile = true;
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
                            if (!spec.TryGetTargetRelPath(sourceRel, out string targetRel))
                            {
                                targetRel = sourceRel;
                            }

                            string targetPath = Path.Combine(fullTargetPath, targetRel);
                            string targetRelativePath = targetDir.CombinePaths(targetRel).TrimStart('/');
                            string sourceRelativePath = file.FullPath.CombinePaths().TrimStart('/');

                            if (checkingDirWithPlaceholderFile)
                            {
                                targetPath = Path.GetDirectoryName(targetPath);

                                if (environmentSettings.Host.FileSystem.DirectoryExists(targetPath))
                                {
                                    changes.Add(new FileChange(sourceRelativePath, targetRelativePath, ChangeKind.Overwrite));
                                }
                                else
                                {
                                    changes.Add(new FileChange(sourceRelativePath, targetRelativePath, ChangeKind.Create));
                                }
                            }
                            else if (environmentSettings.Host.FileSystem.FileExists(targetPath))
                            {
                                changes.Add(new FileChange(sourceRelativePath, targetRelativePath, ChangeKind.Overwrite));
                            }
                            else
                            {
                                changes.Add(new FileChange(sourceRelativePath, targetRelativePath, ChangeKind.Create));
                            }
                        }
                        break;
                    }
                }
            }
            return changes;
        }

        private void RunInternal(IEngineEnvironmentSettings environmentSettings, IDirectory sourceDir, string targetDir, IGlobalRunSpec spec)
        {
            EngineConfig cfg = new EngineConfig(environmentSettings, EngineConfig.DefaultWhitespaces, EngineConfig.DefaultLineEndings, spec.RootVariableCollection);
            IProcessor fallback = Processor.Create(cfg, spec.Operations);

            List<KeyValuePair<IPathMatcher, IProcessor>> fileGlobProcessors = CreateFileGlobProcessors(sourceDir.MountPoint.EnvironmentSettings, spec);

            foreach (IFile file in sourceDir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                string sourceRel = file.PathRelativeTo(sourceDir);
                string fileName = Path.GetFileName(sourceRel);
                bool checkingDirWithPlaceholderFile = false;

                if (spec.IgnoreFileNames.Contains(fileName))
                {   // The placeholder file should never get copied / created / processed. It just causes the dir to get created if needed.
                    // The change checking / reporting is different, setting this variable tracks it.
                    checkingDirWithPlaceholderFile = true;
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

                            spec.LocalizationOperations.TryGetValue(sourceRel, out IReadOnlyList<IOperationProvider> locOperations);

                            if (checkingDirWithPlaceholderFile)
                            {
                                CreateTargetDir(environmentSettings, sourceRel, targetDir, spec);
                            }
                            else if (!copy)
                            {
                                ProcessFile(file, sourceRel, targetDir, spec, fallback, fileGlobProcessors, locOperations);
                            }
                            else
                            {
                                string targetPath = CreateTargetDir(environmentSettings, sourceRel, targetDir, spec);

                                using (Stream sourceStream = file.OpenRead())
                                using (Stream targetStream = environmentSettings.Host.FileSystem.CreateFile(targetPath))
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

        private static string CreateTargetDir(IEngineEnvironmentSettings environmentSettings, string sourceRel, string targetDir, IGlobalRunSpec spec)
        {
            if (!spec.TryGetTargetRelPath(sourceRel, out string targetRel))
            {
                targetRel = sourceRel;
            }

            string targetPath = Path.Combine(targetDir, targetRel);
            string fullTargetDir = Path.GetDirectoryName(targetPath);
            environmentSettings.Host.FileSystem.CreateDirectory(fullTargetDir);

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

            if (!spec.TryGetTargetRelPath(sourceRel, out string targetRel))
            {
                targetRel = sourceRel;
            }

            string targetPath = Path.Combine(targetDir, targetRel);
            //TODO: Update context with the current file & such here

            bool customBufferSize = TryGetBufferSize(sourceFile, out int bufferSize);
            bool customFlushThreshold = TryGetFlushThreshold(sourceFile, out int flushThreshold);
            string fullTargetDir = Path.GetDirectoryName(targetPath);
            sourceFile.MountPoint.EnvironmentSettings.Host.FileSystem.CreateDirectory(fullTargetDir);

            try
            {
                using (Stream source = sourceFile.OpenRead())
                using (Stream target = sourceFile.MountPoint.EnvironmentSettings.Host.FileSystem.CreateFile(targetPath))
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
                throw new ContentGenerationException($"Error while processing file {sourceFile.FullPath}", ex);
            }
        }
    }
}
