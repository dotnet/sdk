using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    internal class ConditionalConfig : IOperationConfig
    {
        public int Order => -7000;

        public string Key => "conditionals";

        public IEnumerable<IOperationProvider> Process(JObject rawConfiguration, IDirectory templateRoot, IVariableCollection variables, IParameterSet parameters)
        {
            IReadOnlyList<string> ifToken = rawConfiguration.ArrayAsStrings("if");
            IReadOnlyList<string> elseToken = rawConfiguration.ArrayAsStrings("else");
            IReadOnlyList<string> elseIfToken = rawConfiguration.ArrayAsStrings("elseif");
            IReadOnlyList<string> actionableIfToken = rawConfiguration.ArrayAsStrings("actionableIf");
            IReadOnlyList<string> actionableElseToken = rawConfiguration.ArrayAsStrings("actionableElse");
            IReadOnlyList<string> actionableElseIfToken = rawConfiguration.ArrayAsStrings("actionableElseif");
            IReadOnlyList<string> actionsToken = rawConfiguration.ArrayAsStrings("actions");
            IReadOnlyList<string> endIfToken = rawConfiguration.ArrayAsStrings("endif");
            string evaluatorName = rawConfiguration.ToString("evaluator") ?? string.Empty;
            string id = rawConfiguration.ToString("id");
            bool trim = rawConfiguration.ToBool("trim");
            bool wholeLine = rawConfiguration.ToBool("wholeLine");
            ConditionEvaluator evaluator;

            switch (evaluatorName)
            {
                case "C++":
                case "":
                    evaluator = CppStyleEvaluatorDefinition.CppStyleEvaluator;
                    break;
                default:
                    throw new Exception($"Unrecognized evaluator {evaluatorName}");
            }

            ConditionalTokens tokenVariants = new ConditionalTokens
            {
                IfTokens = ifToken,
                ElseTokens = elseToken,
                ElseIfTokens = elseIfToken,
                EndIfTokens = endIfToken,
                ActionableElseIfTokens = actionableElseIfToken,
                ActionableElseTokens = actionableElseToken,
                ActionableIfTokens = actionableIfToken,
                ActionableOperations = actionsToken
            };

            yield return new Conditional(tokenVariants, wholeLine, trim, evaluator, id);
        }
    }
}