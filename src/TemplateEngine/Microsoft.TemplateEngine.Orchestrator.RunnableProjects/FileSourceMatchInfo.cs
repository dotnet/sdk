using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class FileSourceEvaluable
    {
        public FileSourceEvaluable(IReadOnlyList<string> include, IReadOnlyList<string> exclude, IReadOnlyList<string> copyOnly)
        {
            Include = include;
            Exclude = exclude;
            CopyOnly = copyOnly;
        }

        public IReadOnlyList<string> Include { get; }

        public IReadOnlyList<string> Exclude { get; }

        public IReadOnlyList<string> CopyOnly { get; }
    }

    public class FileSourceMatchInfo
    {
        public FileSourceMatchInfo(string source, string target, FileSourceEvaluable topLevelEvaluable, IReadOnlyDictionary<string, string> renames, IReadOnlyList<FileSourceEvaluable> modifiers)
        {
            Source = source;
            Target = target;
            TopLevelEvaluable = topLevelEvaluable;
            Renames = renames;
            Modifiers = modifiers;
        }

        public string Source { get; }

        public string Target { get; }

        public FileSourceEvaluable TopLevelEvaluable { get; }

        public IReadOnlyDictionary<string, string> Renames { get; }

        public IReadOnlyList<FileSourceEvaluable> Modifiers { get; }
    }
}
