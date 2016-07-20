using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions.Runner
{
    public interface IOrchestrator
    {
        void Run(string runSpecPath, IDirectory sourceDir, string targetDir);

        void Run(IGlobalRunSpec spec, IDirectory sourceDir, string targetDir);
    }
}