// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class ReplacementTests : TestBase
    {
        [Fact(DisplayName = nameof(VerifyReplacement))]
        public void VerifyReplacement()
        {
            string value = @"test value test";
            string expected = @"test foo test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacement("value".TokenConfig(), "foo", null, true) };
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyNoReplacement))]
        public void VerifyNoReplacement()
        {
            string value = @"test value test";
            string expected = @"test value test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacement("value2".TokenConfig(), "foo", null, true) };
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyTornReplacement))]
        public void VerifyTornReplacement()
        {
            string value = @"test value test";
            string expected = @"test foo test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacement("value".TokenConfig(), "foo", null, true) };
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 6);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyTinyPageReplacement))]
        public void VerifyTinyPageReplacement()
        {
            string value = @"test value test";
            string expected = @"test foo test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacement("value".TokenConfig(), "foo", null, true) };
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }
    }
}
