// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class CustomConditionalTests
    {
        // defines a template configuration with a custom conditional configuration.
        // The style is omitted, which implies custon
        private static JObject CustomConditionalSetupNoStyleSpecification
        {
            get
            {
                string configString = /*lang=json*/ """
                {
                             "actionableIf": [ "<!--#if" ],
                             "actionableElse": [ "#else", "<!--#else" ],
                             "actionableElseif": [ "#elseif", "<!--#elseif" ],
                             "endif": [ "#endif", "<!--#endif" ],
                             "trim" : "true",
                             "wholeLine": "true",
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject CustomConditionalSetupExplicitStyleSpecification
        {
            get
            {
                string configString = /*lang=json*/ """
                {
                             "style": "custom",
                             "actionableIf": [ "<!--#if" ],
                             "actionableElse": [ "#else", "<!--#else" ],
                             "actionableElseif": [ "#elseif", "<!--#elseif" ],
                             "endif": [ "#endif", "<!--#endif" ],
                             "trim" : "true",
                             "wholeLine": "true",
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject LineConditionalSetup
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                        "style": "line",
                        "token": "//"
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject BlockConditionalSetup
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                        "style": "block",
                        "startToken": "/*",
                        "endToken": "*/",
                        "pseudoEndToken": "* /"
                }
                """;
                return JObject.Parse(configString);
            }
        }

        [Fact(DisplayName = nameof(TestCustomConditionalSetupNoStyleSpecification))]
        public void TestCustomConditionalSetupNoStyleSpecification()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJson(CustomConditionalSetupNoStyleSpecification.ToString(), A.Fake<IDirectory>());
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.Single(operations);
            Assert.True(operations[0] is Conditional);

            Conditional? conditionalOp = operations[0] as Conditional;
            Assert.NotNull(conditionalOp);
            Assert.Single(conditionalOp!.Tokens.ActionableIfTokens);
            Assert.Equal("<!--#if", conditionalOp.Tokens.ActionableIfTokens[0].Value);

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseTokens.Count);
            Assert.Contains(conditionalOp.Tokens.ActionableElseTokens, x => x.Value == "<!--#else");
            Assert.Contains(conditionalOp.Tokens.ActionableElseTokens, x => x.Value == "#else");

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseIfTokens.Count);
            Assert.Contains(conditionalOp.Tokens.ActionableElseIfTokens, x => x.Value == "<!--#elseif");
            Assert.Contains(conditionalOp.Tokens.ActionableElseIfTokens, x => x.Value == "#elseif");

            Assert.Equal(2, conditionalOp.Tokens.EndIfTokens.Count);
            Assert.Contains(conditionalOp.Tokens.EndIfTokens, x => x.Value == "<!--#endif");
            Assert.Contains(conditionalOp.Tokens.EndIfTokens, x => x.Value == "#endif");

            Assert.True(conditionalOp.WholeLine);
            Assert.True(conditionalOp.TrimWhitespace);
        }

        [Fact(DisplayName = nameof(TestCustomConditionalSetupExplicitStyleSpecification))]
        public void TestCustomConditionalSetupExplicitStyleSpecification()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJson(CustomConditionalSetupExplicitStyleSpecification.ToString(), A.Fake<IDirectory>());
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.Single(operations);
            Assert.True(operations[0] is Conditional);

            Conditional? conditionalOp = operations[0] as Conditional;
            Assert.NotNull(conditionalOp);
            Assert.Single(conditionalOp!.Tokens.ActionableIfTokens);
            Assert.Equal("<!--#if", conditionalOp.Tokens.ActionableIfTokens[0].Value);

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseTokens.Count);
            Assert.Contains(conditionalOp.Tokens.ActionableElseTokens, x => x.Value == "<!--#else");
            Assert.Contains(conditionalOp.Tokens.ActionableElseTokens, x => x.Value == "#else");

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseIfTokens.Count);
            Assert.Contains(conditionalOp.Tokens.ActionableElseIfTokens, x => x.Value == "<!--#elseif");
            Assert.Contains(conditionalOp.Tokens.ActionableElseIfTokens, x => x.Value == "#elseif");

            Assert.Equal(2, conditionalOp.Tokens.EndIfTokens.Count);
            Assert.Contains(conditionalOp.Tokens.EndIfTokens, x => x.Value == "<!--#endif");
            Assert.Contains(conditionalOp.Tokens.EndIfTokens, x => x.Value == "#endif");

            Assert.True(conditionalOp.WholeLine);
            Assert.True(conditionalOp.TrimWhitespace);
        }

        [Fact(DisplayName = nameof(TestLineCommentConditionalSetup))]
        public void TestLineCommentConditionalSetup()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJson(LineConditionalSetup.ToString(), A.Fake<IDirectory>());
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.Equal(3, operations.Count);
            Assert.True(operations[0] is Conditional);

            Conditional? conditionalOp = operations[0] as Conditional;
            Assert.NotNull(conditionalOp);

            Assert.Single(conditionalOp!.Tokens.IfTokens);
            Assert.Equal("//#if", conditionalOp.Tokens.IfTokens[0].Value);

            Assert.Equal(2, conditionalOp.Tokens.ElseIfTokens.Count);
            Assert.Contains(conditionalOp.Tokens.ElseIfTokens, x => x.Value == "//#elseif");
            Assert.Contains(conditionalOp.Tokens.ElseIfTokens, x => x.Value == "//#elif");

            Assert.Single(conditionalOp.Tokens.ElseTokens);
            Assert.Equal("//#else", conditionalOp.Tokens.ElseTokens[0].Value);

            Assert.Equal(2, conditionalOp.Tokens.EndIfTokens.Count);
            Assert.Contains(conditionalOp.Tokens.EndIfTokens, x => x.Value == "//#endif");
            Assert.Contains(conditionalOp.Tokens.EndIfTokens, x => x.Value == "////#endif");

            Assert.Single(conditionalOp.Tokens.ActionableIfTokens);
            Assert.Equal("////#if", conditionalOp.Tokens.ActionableIfTokens[0].Value);

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseIfTokens.Count);
            Assert.Contains(conditionalOp.Tokens.ActionableElseIfTokens, x => x.Value == "////#elseif");
            Assert.Contains(conditionalOp.Tokens.ActionableElseIfTokens, x => x.Value == "////#elif");

            Assert.Single(conditionalOp.Tokens.ActionableElseTokens);
            Assert.Equal("////#else", conditionalOp.Tokens.ActionableElseTokens[0].Value);

            Assert.True(conditionalOp.WholeLine);
            Assert.True(conditionalOp.TrimWhitespace);
        }

        [Fact(DisplayName = nameof(TestBlockCommentConditionalSetup))]
        public void TestBlockCommentConditionalSetup()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJson(BlockConditionalSetup.ToString(), A.Fake<IDirectory>());
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.Equal(2, operations.Count);  // conditional & pseudo comment balancer
            Assert.True(operations[0] is Conditional);

            Conditional? conditionalOp = operations[0] as Conditional;
            Assert.NotNull(conditionalOp);

            Assert.Equal(2, conditionalOp!.Tokens.EndIfTokens.Count);
            Assert.Contains(conditionalOp.Tokens.EndIfTokens, x => x.Value == "#endif");
            Assert.Contains(conditionalOp.Tokens.EndIfTokens, x => x.Value == "/*#endif");

            Assert.Single(conditionalOp.Tokens.ActionableIfTokens);
            Assert.Equal("/*#if", conditionalOp.Tokens.ActionableIfTokens[0].Value);

            Assert.Equal(4, conditionalOp.Tokens.ActionableElseIfTokens.Count);
            Assert.Contains(conditionalOp.Tokens.ActionableElseIfTokens, x => x.Value == "#elseif");
            Assert.Contains(conditionalOp.Tokens.ActionableElseIfTokens, x => x.Value == "/*#elseif");
            Assert.Contains(conditionalOp.Tokens.ActionableElseIfTokens, x => x.Value == "#elif");
            Assert.Contains(conditionalOp.Tokens.ActionableElseIfTokens, x => x.Value == "/*#elif");

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseTokens.Count);
            Assert.Contains(conditionalOp.Tokens.ActionableElseTokens, x => x.Value == "#else");
            Assert.Contains(conditionalOp.Tokens.ActionableElseTokens, x => x.Value == "/*#else");

            Assert.True(conditionalOp.WholeLine);
            Assert.True(conditionalOp.TrimWhitespace);
        }
    }
}
