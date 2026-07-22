// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    [TestClass]
    public class Cpp2EvaluatorTests : TestBase
    {
        private static TestLoggerFactory s_loggerFactory = null!;
        private readonly ILogger _logger;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_loggerFactory = new TestLoggerFactory();

        [ClassCleanup]
        public static void ClassCleanup() => s_loggerFactory?.Dispose();

        public Cpp2EvaluatorTests()
        {
            _logger = s_loggerFactory.CreateLogger();
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorTrueLiteral()
        {
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "true", new VariableCollection(), out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorFalseLiteral()
        {
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "false", new VariableCollection(), out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorStringLiteral()
        {
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "blah blah", new VariableCollection(), out string? faultedMessage);
            Assert.IsNotNull(faultedMessage);
            Assert.IsFalse(result);
            Assert.Contains("was not recognized as a valid Boolean", faultedMessage);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorUnknownVariable()
        {
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF == true", new VariableCollection(), out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorUnknownVariableEroneousExpression()
        {
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF == true blah blah", new VariableCollection(), out string? faultedMessage);
            Assert.IsNotNull(faultedMessage);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorTrueStringVariable()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = "true"
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorTrueBooleanVariable()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = true
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorStringBoolAndExpression()
        {
            // Validates that And operator handles string-typed bool values (e.g. from template parameters
            // stored as strings) without throwing InvalidCastException.
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = "false",
                ["SECOND_IF"] = "true"
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF && SECOND_IF", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorStringBoolOrExpression()
        {
            // Validates that Or operator handles string-typed bool values without throwing InvalidCastException.
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = "false",
                ["SECOND_IF"] = "true"
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF || SECOND_IF", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorEmptyStringVariableAsBool()
        {
            // Validates that a variable with empty-string value (e.g. a choice parameter with defaultValue: "")
            // is treated as false in a boolean context, without setting a faultedMessage.
            VariableCollection vc = new VariableCollection
            {
                ["ALLOW_PRERELEASE"] = "",
                ["NO_SDK_VERSION"] = false
            };
            // Bare empty-string variable should evaluate to false without error
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "ALLOW_PRERELEASE", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorEmptyStringAndBoolExpression()
        {
            // Validates that And with an empty-string operand doesn't throw.
            VariableCollection vc = new VariableCollection
            {
                ["ALLOW_PRERELEASE"] = "",
                ["NO_SDK_VERSION"] = false
            };
            // (!NO_SDK_VERSION || ALLOW_PRERELEASE!="") - a representative condition from global.json template
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "(!NO_SDK_VERSION || ALLOW_PRERELEASE!=\"\")", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result); // !false = true, regardless of right side
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorTrueVariableErroneousExpression()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = true
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF = false", vc, out string? faultedMessage);
            Assert.IsNotNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorFalse()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = false
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorAndEqualsNot()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = false,
                ["SECOND_IF"] = false
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF == SECOND_IF && !FIRST_IF ", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
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
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
            Assert.HasCount(2, keys);
            Assert.IsTrue(keys.SequenceEqual(new[] { "FIRST_IF", "SECOND_IF" }));
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorBitShiftAddEquals()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST"] = 8,
                ["SECOND"] = 5
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST >> 1 + 2 == 1 + SECOND", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorMultipleNotEqualsAnd()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = false,
                ["SECOND_IF"] = false
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "!!!FIRST_IF && !SECOND_IF == !FIRST_IF", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorStringEquals()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = "1.2.3"
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST_IF == '1.2.3'", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorNumerics()
        {
            VariableCollection vc = new VariableCollection();
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "0x20 == '32'", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorShifts()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST"] = "4",
                ["SECOND"] = "64"
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "FIRST << 2 == SECOND >> 2", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyCpp2EvaluatorMath()
        {
            VariableCollection vc = new VariableCollection();
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_logger, "4 + 9 / (2 + 1) == (0x38 >> 2) / (1 << 0x01)", vc, out string? faultedMessage);
            Assert.IsNull(faultedMessage);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyEvaluableExpressionNoVarsCollectionProvided()
        {
            VariableCollection vc = new VariableCollection();
            HashSet<string> referencedVariablesKeys = new HashSet<string>();
            var result = Cpp2StyleEvaluatorDefinition.GetEvaluableExpression(
                _logger, "navigate == true", vc, out string? faultedMessage, referencedVariablesKeys);

            Assert.IsNull(faultedMessage);
            Assert.IsNotNull(result);
            Assert.IsEmpty(vc);
        }

        [TestMethod]
        public void VerifyEvaluableExpressionVarsCollectionProvided()
        {
            VariableCollection vc = new VariableCollection()
            {
                { "navigate", "true" }
            };
            HashSet<string> referencedVariablesKeys = new HashSet<string>();
            var result = Cpp2StyleEvaluatorDefinition.GetEvaluableExpression(
                _logger, "navigate == true", vc, out string? faultedMessage, referencedVariablesKeys);

            Assert.IsNull(faultedMessage);
            Assert.IsNotNull(result);
            Assert.ContainsSingle(referencedVariablesKeys);
            Assert.AreEqual("navigate", referencedVariablesKeys.First());
        }
    }
}
