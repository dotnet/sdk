using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class CustomConditionalTests : TestBase
    {
        // defines a template configuration with a custom conditional configuration.
        // The style is omitted, which implies custon
        private static JObject CustomConditionalSetupNoStyleSpecification
        {
            get
            {
                string configString = @"
{
             ""actionableIf"": [ ""<!--#if"" ],
             ""actionableElse"": [ ""#else"", ""<!--#else"" ],
             ""actionableElseif"": [ ""#elseif"", ""<!--#elseif"" ],
             ""endif"": [ ""#endif"", ""<!--#endif"" ],
             ""trim"" : ""true"",
             ""wholeLine"": ""true"",
}
";
                return JObject.Parse(configString);
            }
        }


        [Fact(DisplayName = nameof(TestCustomConditionalSetupNoStyleSpecification))]
        public void TestCustomConditionalSetupNoStyleSpecification()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJObject(CustomConditionalSetupNoStyleSpecification, null);
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.Equal(1, operations.Count);
            Assert.True(operations[0] is Conditional);

            Conditional conditionalOp = operations[0] as Conditional;
            Assert.Equal(1, conditionalOp.Tokens.ActionableIfTokens.Count);
            Assert.Equal("<!--#if", conditionalOp.Tokens.ActionableIfTokens[0]);

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseTokens.Count);
            Assert.True(conditionalOp.Tokens.ActionableElseTokens.Contains("<!--#else"));
            Assert.True(conditionalOp.Tokens.ActionableElseTokens.Contains("#else"));

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseIfTokens.Count);
            Assert.True(conditionalOp.Tokens.ActionableElseIfTokens.Contains("<!--#elseif"));
            Assert.True(conditionalOp.Tokens.ActionableElseIfTokens.Contains("#elseif"));

            Assert.Equal(2, conditionalOp.Tokens.EndIfTokens.Count);
            Assert.True(conditionalOp.Tokens.EndIfTokens.Contains("<!--#endif"));
            Assert.True(conditionalOp.Tokens.EndIfTokens.Contains("#endif"));

            Assert.True(conditionalOp.WholeLine);
            Assert.True(conditionalOp.TrimWhitespace);
        }

        private static JObject CustomConditionalSetupExplicitStyleSpecification
        {
            get
            {
                string configString = @"
{
             ""commentStyle"": ""custom"",
             ""actionableIf"": [ ""<!--#if"" ],
             ""actionableElse"": [ ""#else"", ""<!--#else"" ],
             ""actionableElseif"": [ ""#elseif"", ""<!--#elseif"" ],
             ""endif"": [ ""#endif"", ""<!--#endif"" ],
             ""trim"" : ""true"",
             ""wholeLine"": ""true"",
}";
                return JObject.Parse(configString);
            }
        }

        [Fact(DisplayName = nameof(TestCustomConditionalSetupExplicitStyleSpecification))]
        public void TestCustomConditionalSetupExplicitStyleSpecification()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJObject(CustomConditionalSetupExplicitStyleSpecification, null);
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.Equal(1, operations.Count);
            Assert.True(operations[0] is Conditional);

            Conditional conditionalOp = operations[0] as Conditional;
            Assert.Equal(1, conditionalOp.Tokens.ActionableIfTokens.Count);
            Assert.Equal("<!--#if", conditionalOp.Tokens.ActionableIfTokens[0]);

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseTokens.Count);
            Assert.True(conditionalOp.Tokens.ActionableElseTokens.Contains("<!--#else"));
            Assert.True(conditionalOp.Tokens.ActionableElseTokens.Contains("#else"));

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseIfTokens.Count);
            Assert.True(conditionalOp.Tokens.ActionableElseIfTokens.Contains("<!--#elseif"));
            Assert.True(conditionalOp.Tokens.ActionableElseIfTokens.Contains("#elseif"));

            Assert.Equal(2, conditionalOp.Tokens.EndIfTokens.Count);
            Assert.True(conditionalOp.Tokens.EndIfTokens.Contains("<!--#endif"));
            Assert.True(conditionalOp.Tokens.EndIfTokens.Contains("#endif"));

            Assert.True(conditionalOp.WholeLine);
            Assert.True(conditionalOp.TrimWhitespace);
        }

        private static JObject LineConditionalSetup
        {
            get
            {
                string configString = @"
{
        ""commentStyle"": ""line"",
        ""comment"": ""//""
}
";
                return JObject.Parse(configString);
            }
        }

        [Fact(DisplayName = nameof(TestLineCommentConditionalSetup))]
        public void TestLineCommentConditionalSetup()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJObject(LineConditionalSetup, null);
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.Equal(3, operations.Count);
            Assert.True(operations[0] is Conditional);

            Conditional conditionalOp = operations[0] as Conditional;

            Assert.Equal(conditionalOp.Tokens.IfTokens.Count, 1);
            Assert.Equal(conditionalOp.Tokens.IfTokens[0], "//#if");

            Assert.Equal(conditionalOp.Tokens.ElseIfTokens.Count, 1);
            Assert.Equal(conditionalOp.Tokens.ElseIfTokens[0], "//#elseif");

            Assert.Equal(conditionalOp.Tokens.ElseTokens.Count, 1);
            Assert.Equal(conditionalOp.Tokens.ElseTokens[0], "//#else");

            Assert.Equal(conditionalOp.Tokens.EndIfTokens.Count, 2);
            Assert.True(conditionalOp.Tokens.EndIfTokens.Contains("//#endif"));
            Assert.True(conditionalOp.Tokens.EndIfTokens.Contains("////#endif"));

            Assert.Equal(conditionalOp.Tokens.ActionableIfTokens.Count, 1);
            Assert.Equal(conditionalOp.Tokens.ActionableIfTokens[0], "////#if");

            Assert.Equal(conditionalOp.Tokens.ActionableElseIfTokens.Count, 1);
            Assert.Equal(conditionalOp.Tokens.ActionableElseIfTokens[0], "////#elseif");

            Assert.Equal(conditionalOp.Tokens.ActionableElseTokens.Count, 1);
            Assert.Equal(conditionalOp.Tokens.ActionableElseTokens[0], "////#else");

            Assert.True(conditionalOp.WholeLine);
            Assert.True(conditionalOp.TrimWhitespace);
        }

        private static JObject BlockConditionalSetup
        {
            get
            {
                string configString = @"
{
        ""commentStyle"": ""block"",
        ""startComment"": ""/*"",
        ""endComment"": ""*/"",
        ""pseudoEndComment"": ""* /""
}";
                return JObject.Parse(configString);
            }
        }

        [Fact(DisplayName = nameof(TestBlockCommentConditionalSetup))]
        public void TestBlockCommentConditionalSetup()
        {
            IEnumerable<IOperationProvider> ops = new ConditionalConfig().ConfigureFromJObject(BlockConditionalSetup, null);
            IList<IOperationProvider> operations = new List<IOperationProvider>(ops);

            Assert.Equal(2, operations.Count);  // conditional & pseudo comment balancer
            Assert.True(operations[0] is Conditional);

            Conditional conditionalOp = operations[0] as Conditional;

            Assert.Equal(2, conditionalOp.Tokens.EndIfTokens.Count);
            Assert.True(conditionalOp.Tokens.EndIfTokens.Contains("#endif"));
            Assert.True(conditionalOp.Tokens.EndIfTokens.Contains("/*#endif"));

            Assert.Equal(1, conditionalOp.Tokens.ActionableIfTokens.Count);
            Assert.Equal("/*#if", conditionalOp.Tokens.ActionableIfTokens[0]);

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseIfTokens.Count);
            Assert.True(conditionalOp.Tokens.ActionableElseIfTokens.Contains("#elseif"));
            Assert.True(conditionalOp.Tokens.ActionableElseIfTokens.Contains("/*#elseif"));

            Assert.Equal(2, conditionalOp.Tokens.ActionableElseTokens.Count);
            Assert.True(conditionalOp.Tokens.ActionableElseTokens.Contains("#else"));
            Assert.True(conditionalOp.Tokens.ActionableElseTokens.Contains("/*#else"));

            Assert.True(conditionalOp.WholeLine);
            Assert.True(conditionalOp.TrimWhitespace);
        }
    }
}
