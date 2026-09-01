// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.New.MSBuildEvaluation;

namespace Microsoft.DotNet.Cli.New.Tests
{
    [TestClass]
    public class CapabilityExpressionEvaluationTests
    {
        [TestMethod]
        [DataRow("Capability", "", false)]
        [DataRow("Cap1", "cap1", true)]
        [DataRow("Cap1 | Cap2", "Cap3", false)]
        [DataRow("Cap1 | Cap2", "Cap1", true)]
        [DataRow("Cap1 & Cap2", "Cap3", false)]
        [DataRow("Cap1 & Cap2", "Cap1", false)]
        [DataRow("Cap1 & Cap2", "Cap1|Cap2", true)]
        [DataRow("Cap1 + Cap2", "Cap3", false)]
        [DataRow("Cap1 + Cap2", "Cap1", false)]
        [DataRow("Cap1 + Cap2", "Cap1|Cap2", true)]
        [DataRow("!Cap3", "Cap3", false)]
        [DataRow("!Cap3", "Cap1", true)]
        [DataRow("(Cap1 | Cap2) + (Cap3 | Cap4)", "Cap3", false)]
        [DataRow("(Cap1 | Cap2) + (Cap3 | Cap4)", "Cap3|Cap1", true)]
        [DataRow("(Cap1 | Cap2) | (Cap3 | Cap4)", "Cap3", true)]
        [DataRow("Cap1 | Cap2 + Cap3 | Cap4", "Cap3", false)]
        [DataRow("Cap1 | Cap2 + Cap3 | Cap4", "Cap3|Cap1", true)]
        [DataRow("Cap1 | Cap2 & Cap3 | Cap4", "Cap3", false)]
        [DataRow("Cap1 | Cap2 & Cap3 | Cap4", "Cap3|Cap1", true)]
        public void EvaluateCapabilityExpression(string expression, string availableCapabilities, bool expectedResult)
        {
            IReadOnlyList<string> projectCapabilites = availableCapabilities.Split("|");
            Assert.AreEqual(expectedResult, CapabilityExpressionEvaluator.Evaluate(expression, projectCapabilites));
        }

        [TestMethod]
        [DataRow("Cap1 |", "Cap3")]
        [DataRow("(Cap1 | Cap2", "Cap1")]
        [DataRow("(Cap1 | Cap2) + ((Cap3 | Cap4)", "Cap3")]
        public void EvaluateCapabilityExpression_ThrowsOnInvalidExpression(string expression, string availableCapabilities)
        {
            IReadOnlyList<string> projectCapabilites = availableCapabilities.Split("|");
            Assert.ThrowsExactly<ArgumentException>(() => CapabilityExpressionEvaluator.Evaluate(expression, projectCapabilites));
        }
    }
}
