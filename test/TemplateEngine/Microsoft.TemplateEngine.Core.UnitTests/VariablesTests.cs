// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class VariablesTests : TestBase, IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public VariablesTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(VerifyVariables))]
        public void VerifyVariables()
        {
            string value = @"test %PATH% test";
            string expected = @"test " + _engineEnvironmentSettings.Environment.GetEnvironmentVariable("PATH") + " test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new ExpandVariables(null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "%{0}%");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyVariablesNull))]
        public void VerifyVariablesNull()
        {
            string value = @"test %NULL% test";
            string expected = @"test null test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new ExpandVariables(null, true) };
            VariableCollection vc = new VariableCollection
            {
                ["NULL"] = null
            };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, vc, "%{0}%");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }
    }
}
