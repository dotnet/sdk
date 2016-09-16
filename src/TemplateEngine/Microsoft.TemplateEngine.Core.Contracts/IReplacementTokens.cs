namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IReplacementTokens
    {
        string VariableName { get; }

        string OriginalValue { get; }
    }
}
