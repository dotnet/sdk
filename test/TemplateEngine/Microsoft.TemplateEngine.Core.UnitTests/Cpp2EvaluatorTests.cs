// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class Cpp2EvaluatorTests : TestBase, IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;
        public Cpp2EvaluatorTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorTrue))]
        public void VerifyCpp2EvaluatorTrue()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = true
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_engineEnvironmentSettings, "FIRST_IF", vc);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorFalse))]
        public void VerifyCpp2EvaluatorFalse()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = false
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_engineEnvironmentSettings, "FIRST_IF", vc);
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
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_engineEnvironmentSettings, "FIRST_IF == SECOND_IF && !FIRST_IF", vc);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorBitShiftAddEquals))]
        public void VerifyCpp2EvaluatorBitShiftAddEquals()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST"] = 8,
                ["SECOND"] = 5
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_engineEnvironmentSettings, "FIRST >> 1 + 2 == 1 + SECOND", vc);
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
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_engineEnvironmentSettings, "!!!FIRST_IF && !SECOND_IF == !FIRST_IF", vc);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorStringEquals))]
        public void VerifyCpp2EvaluatorStringEquals()
        {
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = "1.2.3"
            };
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_engineEnvironmentSettings, "FIRST_IF == '1.2.3'", vc);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorNumerics))]
        public void VerifyCpp2EvaluatorNumerics()
        {
            VariableCollection vc = new VariableCollection();
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_engineEnvironmentSettings, "0x20 == '32'", vc);
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
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_engineEnvironmentSettings, "FIRST << 2 == SECOND >> 2", vc);
            Assert.True(result);
        }

        [Fact(DisplayName = nameof(VerifyCpp2EvaluatorMath))]
        public void VerifyCpp2EvaluatorMath()
        {
            VariableCollection vc = new VariableCollection();
            bool result = Cpp2StyleEvaluatorDefinition.EvaluateFromString(_engineEnvironmentSettings, "4 + 9 / (2 + 1) == (0x38 >> 2) / (1 << 0x01)", vc);
            Assert.True(result);
        }
    }
}
