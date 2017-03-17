namespace Microsoft.TemplateEngine.Core.Expressions
{
    public class TokenScope<TToken> : IEvaluable
    {
        public TokenScope(IEvaluable parent, Token<TToken> token)
        {
            Parent = parent;
            Token = token;
        }

        public bool IsFull => true;

        public bool IsIndivisible => true;

        public bool IsQuoted { get; set; }

        public IEvaluable Parent { get; set; }

        public Token<TToken> Token { get; }

        public object Evaluate()
        {
            return Token.Value;
        }

        public override string ToString()
        {
            return $@"""{Token.Value}""";
        }

        public bool TryAccept(IEvaluable child) => false;
    }
}
