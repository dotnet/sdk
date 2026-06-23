// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    [TestClass]
    public class VariablesTests : TestBase
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper(NullMessageSink.Instance);

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        public VariablesTests()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [TestMethod]
        public void VerifyVariables()
        {
            string value = @"test VAL test";
            string expected = @"test testValue test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VAL"] = "testValue"
            };

            IOperationProvider[] operations = { new ExpandVariables(null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, vc);
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyVariablesNull()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                VariableCollection vc = new()
                {
                    ["NULL"] = null!
                };
            });
        }
    }
}
