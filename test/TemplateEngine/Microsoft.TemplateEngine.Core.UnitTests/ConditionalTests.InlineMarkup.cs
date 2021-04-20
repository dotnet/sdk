// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.MSBuild;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class InlineMarkupConditionalTests : TestBase
    {
        private IProcessor SetupXmlPlusMsBuildProcessor(IVariableCollection vc)
        {
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, vc, "$({0})");
            return Processor.Create(cfg, new InlineMarkupConditional(
                new MarkupTokens("<".TokenConfig(), "</".TokenConfig(), ">".TokenConfig(), "/>".TokenConfig(), "Condition=\"".TokenConfig(), "\"".TokenConfig()),
                true,
                true,
                MSBuildStyleEvaluatorDefinition.Evaluate,
                "$({0})",
                null,
                true
            ));
        }

        private IProcessor SetupXmlPlusMsBuildProcessorAndReplacement(IVariableCollection vc)
        {
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, vc, "$({0})");
            return Processor.Create(
                cfg,
                new InlineMarkupConditional(
                    new MarkupTokens("<".TokenConfig(), "</".TokenConfig(), ">".TokenConfig(), "/>".TokenConfig(), "Condition=\"".TokenConfig(), "\"".TokenConfig()),
                true,
                true,
                MSBuildStyleEvaluatorDefinition.Evaluate,
                "$({0})",
                null,
                true),
                new Replacement("ReplaceMe".TokenConfig(), "I've been replaced", null, true));
        }

        private IProcessor SetupXmlPlusMsBuildProcessorAndReplacementWithLookaround(IVariableCollection vc)
        {
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, vc, "$({0})");
            return Processor.Create(
                cfg,
                new InlineMarkupConditional(
                    new MarkupTokens("<".TokenConfig(), "</".TokenConfig(), ">".TokenConfig(), "/>".TokenConfig(), "Condition=\"".TokenConfig(), "\"".TokenConfig()),
                true,
                true,
                MSBuildStyleEvaluatorDefinition.Evaluate,
                "$({0})",
                null,
                true),
                new Replacement("ReplaceMe".TokenConfigBuilder().OnlyIfAfter("Condition=\"Exists("), "I've been replaced", null, true));
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupTrue))]
        public void VerifyInlineMarkupTrue()
        {
            string originalValue = @"<root>
    <element Condition=""$(FIRST_IF)"" />
    <element Condition=""$(SECOND_IF)"">
        <child>
            <grandchild />
        </child>
    </element>
</root>";

            string expectedValue = @"<root>
    <element />
    <element>
        <child>
            <grandchild />
        </child>
    </element>
</root>";
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = true,
                ["SECOND_IF"] = true
            };
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupSelfClosedFalse))]
        public void VerifyInlineMarkupSelfClosedFalse()
        {
            string originalValue = @"<root>
    <element Condition=""$(FIRST_IF)"" />
    <element Condition=""$(SECOND_IF)"">
        <child>
            <grandchild />
        </child>
    </element>
</root>";

            string expectedValue = @"<root>
    <element>
        <child>
            <grandchild />
        </child>
    </element>
</root>";
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = false,
                ["SECOND_IF"] = true
            };
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupElementWithChildrenFalse))]
        public void VerifyInlineMarkupElementWithChildrenFalse()
        {
            string originalValue = @"<root>
    <element Condition=""$(FIRST_IF)"" />
    <element Condition=""$(SECOND_IF)"">
        <child>
            <grandchild />
        </child>
    </element>
</root>";

            string expectedValue = @"<root>
    <element />
</root>";
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = true,
                ["SECOND_IF"] = false
            };
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupFalse))]
        public void VerifyInlineMarkupFalse()
        {
            string originalValue = @"<root>
    <element Condition=""$(FIRST_IF)"" />
    <element Condition=""$(SECOND_IF)"">
        <child>
            <grandchild />
        </child>
    </element>
</root>";

            string expectedValue = @"<root>
</root>";
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = false,
                ["SECOND_IF"] = false
            };
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupExpandedConditions1))]
        public void VerifyInlineMarkupExpandedConditions1()
        {
            string originalValue = @"<root>
    <element Condition=""('$(FIRST_IF)' == '$(SECOND_IF)') AND !$(FIRST_IF)"" />
</root>";

            string expectedValue = @"<root>
    <element />
</root>";
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = false,
                ["SECOND_IF"] = false
            };
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupExpandedConditions2))]
        public void VerifyInlineMarkupExpandedConditions2()
        {
            string originalValue = @"<root>
    <element Condition=""!!!$(FIRST_IF) AND !$(SECOND_IF) == !$(FIRST_IF)"" />
</root>";

            string expectedValue = @"<root>
    <element />
</root>";
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = false,
                ["SECOND_IF"] = false
            };
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupExpandedConditionsEscaping))]
        public void VerifyInlineMarkupExpandedConditionsEscaping()
        {
            string originalValue = @"<root>
    <element Condition=""'$(FIRST_IF)' == '&gt;&lt;&#x0020;&amp;&apos;&#0032;&quot;'"" />
</root>";

            string expectedValue = @"<root>
    <element />
</root>";
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = ">< &' \""
            };
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupExpandedConditionsVersion))]
        public void VerifyInlineMarkupExpandedConditionsVersion()
        {
            string originalValue = @"<root>
    <element Condition=""'$(FIRST_IF)' == '1.2.3'"" />
</root>";

            string expectedValue = @"<root>
    <element />
</root>";
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = "1.2.3"
            };
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupExpandedConditionsUndefinedSymbolEmitsOriginal))]
        public void VerifyInlineMarkupExpandedConditionsUndefinedSymbolEmitsOriginal()
        {
            string originalValue = @"<root>
    <element Condition=""'$(FIRST_IF)' == '1.2.3'"" />
</root>";

            string expectedValue = @"<root>
    <element Condition=""'$(FIRST_IF)' == '1.2.3'"" />
</root>";

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999, true);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupExpandedConditionsUndefinedSymbolEmitsOriginal2))]
        public void VerifyInlineMarkupExpandedConditionsUndefinedSymbolEmitsOriginal2()
        {
            string originalValue = @"<PaketExePath Condition="" '$(PaketExePath)' == '' "">$(PaketToolsPath)paket.exe</PaketExePath>";

            string expectedValue = @"<PaketExePath Condition="" '$(PaketExePath)' == '' "">$(PaketToolsPath)paket.exe</PaketExePath>";

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999, true);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupExpandedConditionsNumerics))]
        public void VerifyInlineMarkupExpandedConditionsNumerics()
        {
            string originalValue = @"<root>
    <element Condition=""0x20 == '32'"" />
</root>";

            string expectedValue = @"<root>
    <element />
</root>";
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = ">< &' \""
            };
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupExpandedConditions3))]
        public void VerifyInlineMarkupExpandedConditions3()
        {
            string originalValue = @"<root>
    <element Condition=""'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'"" />
</root>";

            string expectedValue = @"<root>
    <element />
</root>";
            VariableCollection vc = new VariableCollection
            {
                ["Configuration"] = "Debug",
                ["Platform"] = "AnyCPU"
            };
            IProcessor processor = SetupXmlPlusMsBuildProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            originalValue = @"<root>
    <element Condition=""'$(Configuration)|$(Platform)' == 'debug|anycpu'"" />
</root>";

            expectedValue = @"<root>
    <element />
</root>";

            RunAndVerify(originalValue, expectedValue, processor, 9999);

            originalValue = @"<root>
    <element Condition=""'$(Configuration)|$(Platform)' == 'Debug|x86'"" />
</root>";

            expectedValue = @"<root>
</root>";

            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupRejectGetsProcessed))]
        public void VerifyInlineMarkupRejectGetsProcessed()
        {
            string originalValue = @"<root>
    <element Condition=""Exists(ReplaceMe)"" />
</root>";

            string expectedValue = @"<root>
    <element Condition=""Exists(I've been replaced)"" />
</root>";
            VariableCollection vc = new VariableCollection { };
            IProcessor processor = SetupXmlPlusMsBuildProcessorAndReplacement(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyInlineMarkupRejectGetsProcessedWithLookaround))]
        public void VerifyInlineMarkupRejectGetsProcessedWithLookaround()
        {
            string originalValue = @"<root>
    <element Condition=""Exists(ReplaceMe)"" />
</root>";

            string expectedValue = @"<root>
    <element Condition=""Exists(I've been replaced)"" />
</root>";
            VariableCollection vc = new VariableCollection { };
            IProcessor processor = SetupXmlPlusMsBuildProcessorAndReplacementWithLookaround(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }
    }
}
