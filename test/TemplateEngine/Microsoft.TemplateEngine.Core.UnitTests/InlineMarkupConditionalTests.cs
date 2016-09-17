using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.MSBuild;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class InlineMarkupConditionalTests : TestBase
    {
        private static IProcessor SetupXmlPlusCppProcessor(IVariableCollection vc)
        {
            EngineConfig cfg = new EngineConfig(vc, "$({0})");
            return Processor.Create(cfg, new InlineMarkupConditional(
                new MarkupTokens("<", "</", ">", "/>", "Condition=\"", "\""),
                true,
                true,
                MSBuildStyleEvaluatorDefinition.MSBuildStyleEvaluator,
                null
            ));
        }

        [Fact]
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
            IProcessor processor = SetupXmlPlusCppProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
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
            IProcessor processor = SetupXmlPlusCppProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
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
            IProcessor processor = SetupXmlPlusCppProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
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
            IProcessor processor = SetupXmlPlusCppProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
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
            IProcessor processor = SetupXmlPlusCppProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
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
            IProcessor processor = SetupXmlPlusCppProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
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
            IProcessor processor = SetupXmlPlusCppProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
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
            IProcessor processor = SetupXmlPlusCppProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
        public void VerifyInlineMarkupExpandedConditionsUndefinedSymbolEmitsOriginal()
        {
            string originalValue = @"<root>
    <element Condition=""'$(FIRST_IF)' == '1.2.3'"" />
</root>";

            string expectedValue = @"<root>
    <element Condition=""'$(FIRST_IF)' == '1.2.3'"" />
</root>";

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupXmlPlusCppProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999, true);
        }

        [Fact]
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
            IProcessor processor = SetupXmlPlusCppProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
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
            IProcessor processor = SetupXmlPlusCppProcessor(vc);
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
    }
}
