namespace Microsoft.TemplateEngine.Core.Operations
{
    public class MarkupTokens
    {
        public MarkupTokens(string openOpenElementToken, string openCloseElementToken, string closeElementTagToken, string selfClosingElementEndToken, string openConditionExpression, string closeConditionExpression)
        {
            OpenOpenElementToken = openOpenElementToken;
            OpenCloseElementToken = openCloseElementToken;
            CloseElementTagToken = closeElementTagToken;
            SelfClosingElementEndToken = selfClosingElementEndToken;
            OpenConditionExpression = openConditionExpression;
            CloseConditionExpression = closeConditionExpression;
        }

        public string CloseConditionExpression { get; }

        public string CloseElementTagToken { get; }

        public string OpenCloseElementToken { get; }

        public string OpenConditionExpression { get; }

        public string OpenOpenElementToken { get; }

        public string SelfClosingElementEndToken { get; }
    }
}