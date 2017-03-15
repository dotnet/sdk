namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface ITokenTrieEvaluator
    {
        int BytesToKeepInBuffer { get; }

        bool Accept(byte data, ref int bufferPosition, out int token);

        bool TryFinalizeMatchesInProgress(ref int bufferPosition, out int token);
    }
}