namespace Microsoft.TemplateEngine.Core.Expressions
{
    public class Token<TToken>
    {
        public Token(TToken family, string value)
        {
            Family = family;
            Value = value;
        }

        public TToken Family { get; }

        public string Value { get; }
    }
}
