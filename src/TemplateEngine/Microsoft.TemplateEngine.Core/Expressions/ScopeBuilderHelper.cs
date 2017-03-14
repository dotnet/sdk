using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Expressions
{
    public static class ScopeBuilderHelper
    {
        public static ScopeBuilder<TOperator, TToken> ScopeBuilder<TOperator, TToken>(this IProcessorState processor, ITokenTrie tokens, IOperatorMap<TOperator, TToken> operatorMap, bool dereferenceInLiterals = false)
            where TOperator : struct
            where TToken : struct
        {
            return new ScopeBuilder<TOperator, TToken>(processor, tokens, operatorMap, dereferenceInLiterals);
        }
    }
}
