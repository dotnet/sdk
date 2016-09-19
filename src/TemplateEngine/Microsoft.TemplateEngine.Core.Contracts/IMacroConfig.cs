using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    // Base interface for all macro configurations
    public interface IMacroConfig : IIdentifiedComponent
    {
        string VariableName { get; }

        string Type { get; }

        string Action { get; }

        IMacroConfig ConfigFromDeferredConfig(IMacroConfig deferredConfig);
    }
}
