using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    // interface for macros created via generated symbols.
    public interface IDeferredMacro : IMacro
    {
        IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig);
    }
}
