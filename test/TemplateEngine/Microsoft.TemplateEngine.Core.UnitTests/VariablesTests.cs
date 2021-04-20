// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class VariablesTests : TestBase
    {
        [Fact(DisplayName = nameof(VerifyVariables))]
        public void VerifyVariables()
        {
            string value = @"test %PATH% test";
            string expected = @"test " + EnvironmentSettings.Environment.GetEnvironmentVariable("PATH") + " test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new ExpandVariables(null, true) };
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "%{0}%");
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
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, vc, "%{0}%");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }
    }
}
