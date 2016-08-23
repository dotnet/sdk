using System;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class VariablesTests : TestBase
    {
        [Fact]
        public void VerifyVariables()
        {
            string value = @"test %PATH% test";
            string expected = @"test " + Environment.GetEnvironmentVariable("PATH") + " test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new ExpandVariables("ExpandVariablesOperationId") };
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "%{0}%");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyVariablesNull()
        {
            string value = @"test %NULL% test";
            string expected = @"test null test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new ExpandVariables("ExpandVariablesOperationId") };
            VariableCollection vc = new VariableCollection
            {
                ["NULL"] = null
            };
            EngineConfig cfg = new EngineConfig(vc, "%{0}%");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }
    }
}
