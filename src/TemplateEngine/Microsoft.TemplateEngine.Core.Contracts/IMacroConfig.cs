namespace Microsoft.TemplateEngine.Core.Contracts
{
    // Base interface for all macro configurations
    public interface IMacroConfig
    {
        string VariableName { get; }

        string Type { get; }
    }
}
