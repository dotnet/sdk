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
    public class RegionTests : TestBase, IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;
        public RegionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(VerifyRegionExclude))]
        public void VerifyRegionExclude()
        {
            string value = @"test value value x test foo bar";
            string expected = @"test  bar";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("value".TokenConfig(), "foo".TokenConfig(), false, false, false, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyRegionInclude))]
        public void VerifyRegionInclude()
        {
            string value = @"test value value x test foo bar";
            string expected = @"test   x test  bar";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("value".TokenConfig(), "foo".TokenConfig(), true, false, false, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyRegionStrayEnd))]
        public void VerifyRegionStrayEnd()
        {
            string value = @"test foo value bar foo";
            string expected = @"test   bar ";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("value".TokenConfig(), "foo".TokenConfig(), true, false, false, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyRegionIncludeToggle))]
        public void VerifyRegionIncludeToggle()
        {
            string value = @"test region value x test region bar";
            string expected = @"test  value x test  bar";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("region".TokenConfig(), "region".TokenConfig(), true, false, false, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyRegionExcludeToggle))]
        public void VerifyRegionExcludeToggle()
        {
            string value = @"test region value x test region bar";
            string expected = @"test  bar";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("region".TokenConfig(), "region".TokenConfig(), false, false, false, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyRegionIncludeWhitespaceFixup))]
        public void VerifyRegionIncludeWhitespaceFixup()
        {
            string value = @"test value value x test foo bar";
            string expected = @"testx testbar";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("value".TokenConfig(), "foo".TokenConfig(), true, false, true, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyRegionIncludeWhitespaceFixup2))]
        public void VerifyRegionIncludeWhitespaceFixup2()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
foo
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin".TokenConfig(), "#end".TokenConfig(), true, false, true, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyRegionIncludeWholeLine))]
        public void VerifyRegionIncludeWholeLine()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin".TokenConfig(), "#end".TokenConfig(), true, true, true, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyNoRegion))]
        public void VerifyNoRegion()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
    #begin foo
value
    #end
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin2".TokenConfig(), "#end2".TokenConfig(), true, true, true, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyTornRegion1))]
        public void VerifyTornRegion1()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin".TokenConfig(), "#end".TokenConfig(), true, true, true, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 14);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyTornRegion2))]
        public void VerifyTornRegion2()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin".TokenConfig(), "#end".TokenConfig(), true, true, true, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 36);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyTornRegion3))]
        public void VerifyTornRegion3()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin".TokenConfig(), "#end".TokenConfig(), true, true, true, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyTinyPageRegion))]
        public void VerifyTinyPageRegion()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin".TokenConfig(), "#end".TokenConfig(), true, true, true, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VerifyTornPageInCloseSeekRegion))]
        public void VerifyTornPageInCloseSeekRegion()
        {
            string value = @"Hello
    #begin foo
value
value
value
value
value
value
value
value
    #end
There";
            string expected = @"Hello
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin".TokenConfig(), "#end".TokenConfig(), false, true, true, null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }
    }
}
