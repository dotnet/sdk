namespace Microsoft.TemplateEngine.Core.Contracts
{
    // Base interface for all macro configurations
    public interface IMacroConfig
    {
        string VariableName { get; }

        string Type { get; }

        // soon to be deprecated. Avoid new uses for this property
        string Action { get; }
    }
}
