// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class PhasedOperationTests : TestBase
    {
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

            Phase root = new Phase("package".TokenConfig(), new[] { "/>" }.TokenConfigs());

            //package > name
            Phase name = new Phase("name".TokenConfig(), new[] { "/>" }.TokenConfigs());
            root.Next.Add(name);

            //package > name > test > 1.0.0
            Phase test = new Phase("test".TokenConfig(), new[] { "/>" }.TokenConfigs());
            name.Next.Add(test);
            Phase testValue = new Phase("1.0.0".TokenConfig(), "9.9.9", new[] { "/>" }.TokenConfigs());
            test.Next.Add(testValue);

            //package > name > foo > 1.0.0
            Phase foo = new Phase("foo".TokenConfig(), new[] { "/>" }.TokenConfigs());
            name.Next.Add(foo);
            Phase fooValue = new Phase("1.0.0".TokenConfig(), "1.2.3", new[] { "/>" }.TokenConfigs());
            foo.Next.Add(fooValue);

            IProcessor processor = SetupConfig(vc, root);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        private IProcessor SetupConfig(IVariableCollection vc, params Phase[] phases)
        {
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, vc, "$({0})");
            return Processor.Create(cfg, new PhasedOperation(null, phases, true));
        }
    }
}
