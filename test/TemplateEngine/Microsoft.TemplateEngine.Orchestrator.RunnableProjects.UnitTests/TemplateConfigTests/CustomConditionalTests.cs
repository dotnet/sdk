// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    [TestClass]
    public class CustomConditionalTests
    {
        // defines a template configuration with a custom conditional configuration.
        // The style is omitted, which implies custon
        private static JsonObject CustomConditionalSetupNoStyleSpecification
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject CustomConditionalSetupExplicitStyleSpecification
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject LineConditionalSetup
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                        "style": "line",
                        "token": "//"
                }
                """;
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject BlockConditionalSetup
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        [TestMethod(DisplayName = nameof(TestCustomConditionalSetupNoStyleSpecification))]
        public void TestCustomConditionalSetupNoStyleSpecification()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJson(CustomConditionalSetupNoStyleSpecification.ToString(), A.Fake<IDirectory>());
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.ContainsSingle(operations);
            Assert.IsTrue(operations[0] is Conditional);

            Conditional? conditionalOp = operations[0] as Conditional;
            Assert.IsNotNull(conditionalOp);
            Assert.ContainsSingle(conditionalOp!.Tokens.ActionableIfTokens);
            Assert.AreEqual("<!--#if", conditionalOp.Tokens.ActionableIfTokens[0].Value);

            Assert.HasCount(2, conditionalOp.Tokens.ActionableElseTokens);
            Assert.Contains("<!--#else", conditionalOp.Tokens.ActionableElseTokens.Select(x => x.Value));
            Assert.Contains("#else", conditionalOp.Tokens.ActionableElseTokens.Select(x => x.Value));

            Assert.HasCount(2, conditionalOp.Tokens.ActionableElseIfTokens);
            Assert.Contains("<!--#elseif", conditionalOp.Tokens.ActionableElseIfTokens.Select(x => x.Value));
            Assert.Contains("#elseif", conditionalOp.Tokens.ActionableElseIfTokens.Select(x => x.Value));

            Assert.HasCount(2, conditionalOp.Tokens.EndIfTokens);
            Assert.Contains("<!--#endif", conditionalOp.Tokens.EndIfTokens.Select(x => x.Value));
            Assert.Contains("#endif", conditionalOp.Tokens.EndIfTokens.Select(x => x.Value));

            Assert.IsTrue(conditionalOp.WholeLine);
            Assert.IsTrue(conditionalOp.TrimWhitespace);
        }

        [TestMethod(DisplayName = nameof(TestCustomConditionalSetupExplicitStyleSpecification))]
        public void TestCustomConditionalSetupExplicitStyleSpecification()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJson(CustomConditionalSetupExplicitStyleSpecification.ToString(), A.Fake<IDirectory>());
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.ContainsSingle(operations);
            Assert.IsTrue(operations[0] is Conditional);

            Conditional? conditionalOp = operations[0] as Conditional;
            Assert.IsNotNull(conditionalOp);
            Assert.ContainsSingle(conditionalOp!.Tokens.ActionableIfTokens);
            Assert.AreEqual("<!--#if", conditionalOp.Tokens.ActionableIfTokens[0].Value);

            Assert.HasCount(2, conditionalOp.Tokens.ActionableElseTokens);
            Assert.Contains("<!--#else", conditionalOp.Tokens.ActionableElseTokens.Select(x => x.Value));
            Assert.Contains("#else", conditionalOp.Tokens.ActionableElseTokens.Select(x => x.Value));

            Assert.HasCount(2, conditionalOp.Tokens.ActionableElseIfTokens);
            Assert.Contains("<!--#elseif", conditionalOp.Tokens.ActionableElseIfTokens.Select(x => x.Value));
            Assert.Contains("#elseif", conditionalOp.Tokens.ActionableElseIfTokens.Select(x => x.Value));

            Assert.HasCount(2, conditionalOp.Tokens.EndIfTokens);
            Assert.Contains("<!--#endif", conditionalOp.Tokens.EndIfTokens.Select(x => x.Value));
            Assert.Contains("#endif", conditionalOp.Tokens.EndIfTokens.Select(x => x.Value));

            Assert.IsTrue(conditionalOp.WholeLine);
            Assert.IsTrue(conditionalOp.TrimWhitespace);
        }

        [TestMethod(DisplayName = nameof(TestLineCommentConditionalSetup))]
        public void TestLineCommentConditionalSetup()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJson(LineConditionalSetup.ToString(), A.Fake<IDirectory>());
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.HasCount(3, operations);
            Assert.IsTrue(operations[0] is Conditional);

            Conditional? conditionalOp = operations[0] as Conditional;
            Assert.IsNotNull(conditionalOp);

            Assert.ContainsSingle(conditionalOp!.Tokens.IfTokens);
            Assert.AreEqual("//#if", conditionalOp.Tokens.IfTokens[0].Value);

            Assert.HasCount(2, conditionalOp.Tokens.ElseIfTokens);
            Assert.Contains("//#elseif", conditionalOp.Tokens.ElseIfTokens.Select(x => x.Value));
            Assert.Contains("//#elif", conditionalOp.Tokens.ElseIfTokens.Select(x => x.Value));

            Assert.ContainsSingle(conditionalOp.Tokens.ElseTokens);
            Assert.AreEqual("//#else", conditionalOp.Tokens.ElseTokens[0].Value);

            Assert.HasCount(2, conditionalOp.Tokens.EndIfTokens);
            Assert.Contains("//#endif", conditionalOp.Tokens.EndIfTokens.Select(x => x.Value));
            Assert.Contains("////#endif", conditionalOp.Tokens.EndIfTokens.Select(x => x.Value));

            Assert.ContainsSingle(conditionalOp.Tokens.ActionableIfTokens);
            Assert.AreEqual("////#if", conditionalOp.Tokens.ActionableIfTokens[0].Value);

            Assert.HasCount(2, conditionalOp.Tokens.ActionableElseIfTokens);
            Assert.Contains("////#elseif", conditionalOp.Tokens.ActionableElseIfTokens.Select(x => x.Value));
            Assert.Contains("////#elif", conditionalOp.Tokens.ActionableElseIfTokens.Select(x => x.Value));

            Assert.ContainsSingle(conditionalOp.Tokens.ActionableElseTokens);
            Assert.AreEqual("////#else", conditionalOp.Tokens.ActionableElseTokens[0].Value);

            Assert.IsTrue(conditionalOp.WholeLine);
            Assert.IsTrue(conditionalOp.TrimWhitespace);
        }

        [TestMethod(DisplayName = nameof(TestBlockCommentConditionalSetup))]
        public void TestBlockCommentConditionalSetup()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJson(BlockConditionalSetup.ToString(), A.Fake<IDirectory>());
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.HasCount(2, operations);  // conditional & pseudo comment balancer
            Assert.IsTrue(operations[0] is Conditional);

            Conditional? conditionalOp = operations[0] as Conditional;
            Assert.IsNotNull(conditionalOp);

            Assert.HasCount(2, conditionalOp!.Tokens.EndIfTokens);
            Assert.Contains("#endif", conditionalOp.Tokens.EndIfTokens.Select(x => x.Value));
            Assert.Contains("/*#endif", conditionalOp.Tokens.EndIfTokens.Select(x => x.Value));

            Assert.ContainsSingle(conditionalOp.Tokens.ActionableIfTokens);
            Assert.AreEqual("/*#if", conditionalOp.Tokens.ActionableIfTokens[0].Value);

            Assert.HasCount(4, conditionalOp.Tokens.ActionableElseIfTokens);
            Assert.Contains("#elseif", conditionalOp.Tokens.ActionableElseIfTokens.Select(x => x.Value));
            Assert.Contains("/*#elseif", conditionalOp.Tokens.ActionableElseIfTokens.Select(x => x.Value));
            Assert.Contains("#elif", conditionalOp.Tokens.ActionableElseIfTokens.Select(x => x.Value));
            Assert.Contains("/*#elif", conditionalOp.Tokens.ActionableElseIfTokens.Select(x => x.Value));

            Assert.HasCount(2, conditionalOp.Tokens.ActionableElseTokens);
            Assert.Contains("#else", conditionalOp.Tokens.ActionableElseTokens.Select(x => x.Value));
            Assert.Contains("/*#else", conditionalOp.Tokens.ActionableElseTokens.Select(x => x.Value));

            Assert.IsTrue(conditionalOp.WholeLine);
            Assert.IsTrue(conditionalOp.TrimWhitespace);
        }
    }
}
