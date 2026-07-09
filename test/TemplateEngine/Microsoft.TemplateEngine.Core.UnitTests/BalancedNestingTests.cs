// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Test change to validate conditional test filtering

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    [TestClass]
    public class BalancedNestingTests : TestBase
    {
        private static TestLoggerFactory s_loggerFactory = null!;
        private readonly ILogger _logger;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_loggerFactory = new TestLoggerFactory();

        [ClassCleanup]
        public static void ClassCleanup() => s_loggerFactory?.Dispose();

        public BalancedNestingTests()
        {
            _logger = s_loggerFactory.CreateLogger();
        }

        // The initial construction of the BalancedNesting operation is supposed to have comment fixing off by default.
        // (Usually, it gets toggled by conditional processing).
        // This test ensures that it's off by default.
        [TestMethod]
        public void VerifyPseudoCommentFixingIsOffByDefault()
        {
            string originalValue = @"<!-- commented with trailing pseudo comment -- >";
            string expectedValue = @"<!-- commented with trailing pseudo comment -- >"; // pseudo comment remains

            IProcessor processor = SetupXmlBalancedNestingProcessor();
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [TestMethod]
        public void VerifyPseudoCommentFixingDoesNotOccurWhenExplicitlyOff()
        {
            string originalValue = @"<!-- commented with trailing pseudo comment -- >";
            string expectedValue = @"<!-- commented with trailing pseudo comment -- >"; // pseudo comment remains

            IProcessor processor = SetupXmlBalancedNestingProcessor(false);  // comment fixing off
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [TestMethod]
        public void VerifyPseudoCommentFixingOccursWhenExplicitlyOn()
        {
            string originalValue = @"<!-- commented with trailing pseudo comment -- >";
            string expectedValue = @"<!-- commented with trailing pseudo comment -->";  // pseudo comment is fixed

            IProcessor processor = SetupXmlBalancedNestingProcessor(true);  // comment fixing on
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        // Inner pseudo comments should never get fixed, regardless of whether outer comment fixing occurs,
        // and regardless of whether comment fixing is turned on or off.
        [TestMethod]
        public void VerifyInnerPseudoCommentIsNotFixed()
        {
            string originalValue = @"<!-- <!-- commented with trailing pseudo comment -- > -->";
            string expectedValue = @"<!-- <!-- commented with trailing pseudo comment -- > -->";  // inner pseudo comment is not changed

            IProcessor processor = SetupXmlBalancedNestingProcessor(true);  // comment fixing on
            RunAndVerify(originalValue, expectedValue, processor, 9999, changeOverride: true);  // value doesn't change, but processer implies it does (thus the test check override)
        }

        // Inner pseudo comments should never get fixed, regardless of whether outer comment fixing occurs,
        // and regardless of whether comment fixing is turned on or off.
        [TestMethod]
        public void VerifyInnerPseudoCommentIsNotFixedWhenOuterCommentIsFixed()
        {
            string originalValue = @"<!-- <!-- commented with trailing pseudo comment -- > -- >";
            string expectedValue = @"<!-- <!-- commented with trailing pseudo comment -- > -->";  // outer is fixed, inner is not changed

            IProcessor processor = SetupXmlBalancedNestingProcessor(true);  // comment fixing on
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        // Lead heavy comments are not correctly dealt with. It would require too much look-ahead.
        // This sort of situation is really an authoring problem.
        // This tests that the values remain unchanged -
        // (which isn't ideal, but as good as we can hope for without significant performance loss).
        // The trailing pseudo comment is not an outer-balanced comment, and so remains unchanged.
        [TestMethod]
        public void VerifyUnbalancedLeadHeavyCommentsAreHandledSanely()
        {
            string originalValue = @"<!-- <!-- two lead comments, 1 trailing pseudo comment -- >";
            string expectedValue = @"<!-- <!-- two lead comments, 1 trailing pseudo comment -- >";

            IProcessor processor = SetupXmlBalancedNestingProcessor(true);  // comment fixing on
            RunAndVerify(originalValue, expectedValue, processor, 9999, changeOverride: true);  // value doesn't change, but processer implies it does (thus the test check override)
        }

        // This situation is also indicative of an authoring problem.
        // The first pseudo-comment is made real because it's balanced with the leading comment.
        // But the second (pseudo) comment is unbalanced and remains unchanged.
        [TestMethod]
        public void VerifyUnbalancedTrailingHeavyPseudoCommentsAreHandledSanely()
        {
            string originalValue = @"<!-- one lead comment, two trailing pseudo comments -- > -- >";
            string expectedValue = @"<!-- one lead comment, two trailing pseudo comments --> -- >";

            IProcessor processor = SetupXmlBalancedNestingProcessor(true);  // comment fixing on
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        // This situation is also indicative of an authoring problem.
        // The first pseudo-comment is made real because it's balanced with the leading comment.
        // But the second (real) end comment is unbalanced and remains unchanged.
        [TestMethod]
        public void VerifyUnbalancedTrailingHeavyRealCommentIsHandledSanely()
        {
            string originalValue = @"<!-- one lead comment, two trailing pseudo comments -- > -->";
            string expectedValue = @"<!-- one lead comment, two trailing pseudo comments --> -->";

            IProcessor processor = SetupXmlBalancedNestingProcessor(true);  // comment fixing on
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        private IProcessor SetupXmlBalancedNestingProcessor(bool? isCommentFixingInitiallyOn = null)
        {
            string commentFixOperationId = "Fix Comments";
            string resetId = "Reset";

            IOperationProvider[] operations =
            {
                new BalancedNesting("<!--".TokenConfig(), "-->".TokenConfig(), "-- >".TokenConfig(), commentFixOperationId, resetId, isCommentFixingInitiallyOn ?? false),
            };
            VariableCollection variables = new VariableCollection();
            EngineConfig engineConfig = new EngineConfig(_logger, variables);
            IProcessor processor = Processor.Create(engineConfig, operations);

            if (isCommentFixingInitiallyOn.HasValue)
            {
                engineConfig.Flags[commentFixOperationId] = isCommentFixingInitiallyOn.Value;
            }

            return processor;
        }
    }
}
