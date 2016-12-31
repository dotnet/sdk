using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class PhasedOperationTests : TestBase
    {
        private static IProcessor SetupConfig(IVariableCollection vc, params Phase[] phases)
        {
            EngineConfig cfg = new EngineConfig(vc, "$({0})");
            return Processor.Create(cfg, new PhasedOperation(null, phases));
        }

        [Fact(DisplayName = nameof(VerifyPhasedOperationStateProgression))]
        public void VerifyPhasedOperationStateProgression()
        {
            string originalValue = @"
<something name=""test"" value=""1.0.0"" />
<package name=""foo"" value=""1.0.0"" />
<package name=""foo"" />
<package value=""1.0.0"" />
<package name=""test"" value=""1.0.0"" />
<somethingDifferent name=""test"" value=""1.0.0"" />";

            string expectedValue = @"
<something name=""test"" value=""1.0.0"" />
<package name=""foo"" value=""1.2.3"" />
<package name=""foo"" />
<package value=""1.0.0"" />
<package name=""test"" value=""9.9.9"" />
<somethingDifferent name=""test"" value=""1.0.0"" />";

            VariableCollection vc = new VariableCollection();

            Phase root = new Phase("package", new[] { "/>" });

            //package > name
            Phase name = new Phase("name", new[] { "/>" });
            root.Next.Add(name);

            //package > name > test > 1.0.0
            Phase test = new Phase("test", new[] { "/>" });
            name.Next.Add(test);
            Phase testValue = new Phase("1.0.0", "9.9.9", new[] { "/>" });
            test.Next.Add(testValue);

            //package > name > foo > 1.0.0
            Phase foo = new Phase("foo", new[] { "/>" });
            name.Next.Add(foo);
            Phase fooValue = new Phase("1.0.0", "1.2.3", new[] { "/>" });
            foo.Next.Add(fooValue);

            IProcessor processor = SetupConfig(vc, root);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }
    }
}
