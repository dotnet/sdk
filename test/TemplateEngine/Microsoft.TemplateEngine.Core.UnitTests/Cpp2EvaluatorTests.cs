// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class Cpp2EvaluatorTests : TestBase, IClassFixture<TestLoggerFactory>
    {
        private readonly ILogger _logger;

        public Cpp2EvaluatorTests(TestLoggerFactory testLoggerFactory)
        {
            _logger = testLoggerFactory.CreateLogger();
        }

        [Fact]
        public void VerifyCpp2EvaluatorTrueLiteral()
        {
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "true", new VariableCollection(), out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.True(result);
        }

        [Fact]
        public void VerifyCpp2EvaluatorFalseLiteral()
        {
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "false", new VariableCollection(), out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.False(result);
        }

        [Fact]
        public void VerifyCpp2EvaluatorStringLiteral()
        {
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "blah blah", new VariableCollection(), out string? faultedMessage);
            Assert.NotNull(faultedMessage);
            Assert.False(result);
            Assert.Contains("was not recognized as a valid Boolean", faultedMessage);
        }

        [Fact]
        public void VerifyCpp2EvaluatorUnknownVariable()
        {
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF == true", new VariableCollection(), out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.False(result);
        }

        [Fact]
        public void VerifyCpp2EvaluatorUnknownVariableEroneousExpression()
        {
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF == true blah blah", new VariableCollection(), out string? faultedMessage);
            Assert.NotNull(faultedMessage);
            Assert.False(result);
        }

        [Fact]
        public void VerifyCpp2EvaluatorTrueStringVariable()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = "true"
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF", vc, out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.True(result);
        }

        [Fact]
        public void VerifyCpp2EvaluatorTrueBooleanVariable()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = true
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF", vc, out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.True(result);
        }

        [Fact]
        public void VerifyCpp2EvaluatorTrueVariableErroneousExpression()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = true
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF = false", vc, out string? faultedMessage);
            Assert.NotNull(faultedMessage);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorFalse))]
        public void VerifyCpp2EvaluatorFalse()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = false
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF", vc, out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.False(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorAndEqualsNot))]
        public void VerifyCpp2EvaluatorAndEqualsNot()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = false,
                ["SECOND_IF"] = false
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF == SECOND_IF && !FIRST_IF ", vc, out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorUsedVariablesSet))]
        public void VerifyCpp2EvaluatorUsedVariablesSet()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF2"] = false,
                ["FIRST_IF3"] = false,
                ["FIRST_IF0"] = false,
                ["FIRST_IF"] = false,
                ["BB"] = false,
                ["0SECOND_IF"] = false,
                ["SECOND_IF"] = false,
                ["5SECOND_IF"] = false,
                ["BB2"] = false,
                ["BB3"] = false,

            };
            HashSet<string> keys = new HashSet<string>();
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF == SECOND_IF && !FIRST_IF", vc, out string? faultedMessage, keys);
            Assert.Null(faultedMessage);
            Assert.True(result);
            Assert.Equal(2, keys.Count);
            Assert.True(keys.SequenceEqual(new[] { "FIRST_IF", "SECOND_IF" }));
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorBitShiftAddEquals))]
        public void VerifyCpp2EvaluatorBitShiftAddEquals()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST"] = 8,
                ["SECOND"] = 5
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST >> 1 + 2 == 1 + SECOND", vc, out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorMultipleNotEqualsAnd))]
        public void VerifyCpp2EvaluatorMultipleNotEqualsAnd()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = false,
                ["SECOND_IF"] = false
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "!!!FIRST_IF && !SECOND_IF == !FIRST_IF", vc, out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorStringEquals))]
        public void VerifyCpp2EvaluatorStringEquals()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = "1.2.3"
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF == '1.2.3'", vc, out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorNumerics))]
        public void VerifyCpp2EvaluatorNumerics()
        {
            VariableCollection vc = new VariableCollection();
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "0x20 == '32'", vc, out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorShifts))]
        public void VerifyCpp2EvaluatorShifts()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST"] = "4",
                ["SECOND"] = "64"
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST << 2 == SECOND >> 2", vc, out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorMath))]
        public void VerifyCpp2EvaluatorMath()
        {
            VariableCollection vc = new VariableCollection();
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "4 + 9 / (2 + 1) == (0x38 >> 2) / (1 << 0x01)", vc, out string? faultedMessage);
            Assert.Null(faultedMessage);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyEvaluableExpressionNoVarsCollectionProvided))]
        public void VerifyEvaluableExpressionNoVarsCollectionProvided()
        {
            VariableCollection vc = new VariableCollection();
            HashSet<string> referencedVariablesKeys = new HashSet<string>();
            var result = Cpp2StyleEvaluatorDefinition.GetEvaluableExpression(
                _logger, "navigate == true", vc, out string? faultedMessage, referencedVariablesKeys);

            Assert.Null(faultedMessage);
            Assert.NotNull(result);
            Assert.Empty(vc);
        }

        [Fact(DisplayName = nameof(VerifyEvaluableExpressionVarsCollectionProvided))]
        public void VerifyEvaluableExpressionVarsCollectionProvided()
        {
            VariableCollection vc = new VariableCollection()
            {
                { "navigate", "true" }
            };
            HashSet<string> referencedVariablesKeys = new HashSet<string>();
            var result = Cpp2StyleEvaluatorDefinition.GetEvaluableExpression(
                _logger, "navigate == true", vc, out string? faultedMessage, referencedVariablesKeys);

            Assert.Null(faultedMessage);
            Assert.NotNull(result);
            Assert.Single(referencedVariablesKeys);
            Assert.Equal("navigate", referencedVariablesKeys.First());
        }
    }
}
