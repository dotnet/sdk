namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IToken
    {
        byte[] Value { get; }

        int Start { get; }

        int End { get; }

        int Length { get; }
    }
}
