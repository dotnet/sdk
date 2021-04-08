using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class FileSourceEvaluable
    {
        internal FileSourceEvaluable(IReadOnlyList<string> include, IReadOnlyList<string> exclude, IReadOnlyList<string> copyOnly)
        {
            Include = include;
            Exclude = exclude;
            CopyOnly = copyOnly;
        }

        internal IReadOnlyList<string> Include { get; }

        internal IReadOnlyList<string> Exclude { get; }

        internal IReadOnlyList<string> CopyOnly { get; }
    }

    internal class FileSourceMatchInfo
    {
        internal FileSourceMatchInfo(string source, string target, FileSourceEvaluable topLevelEvaluable, IReadOnlyDictionary<string, string> renames, IReadOnlyList<FileSourceEvaluable> modifiers)
        {
            Source = source;
            Target = target;
            TopLevelEvaluable = topLevelEvaluable;
            Renames = renames;
            Modifiers = modifiers;
        }

        internal string Source { get; }

        internal string Target { get; }

        internal FileSourceEvaluable TopLevelEvaluable { get; }

        internal IReadOnlyDictionary<string, string> Renames { get; }

        internal IReadOnlyList<FileSourceEvaluable> Modifiers { get; }
    }
}
