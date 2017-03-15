namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IReplacementTokens
    {
        string VariableName { get; }

        ITokenConfig OriginalValue { get; }
    }
}
