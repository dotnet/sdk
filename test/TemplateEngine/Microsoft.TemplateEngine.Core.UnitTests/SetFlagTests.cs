// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    [TestClass]
    public class SetFlagTests : TestBase
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper(NullMessageSink.Instance);

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        public SetFlagTests()
        {
            _environmentSettingsHelper = s_environmentSettingsHelper;
        }

        // Demonstrate that conditional operations processing is correctly enabled & disabled with Emit flags.
        [TestMethod]
        public void TurnOffConditionalTest()
        {
            string originalValue = @"lead stuff
//#if (true)
    true stuff
//#else
    false stuff
//#endif
Entering Conditional processing = off
//-:cnd
...in
//#if (true)
    in-conditional true (IF) stuff
//#else
    in-conditional ELSE stuff
//#endif
...about to exit
//+:cnd
After Conditional processing = off
//#if (false)
    After conditional IF(false) stuff
//#else
    After conditional ELSE(true) stuff
//#endif
Final stuff";

            string expectedValue = @"lead stuff
    true stuff
Entering Conditional processing = off
//-:cnd
...in
//#if (true)
    in-conditional true (IF) stuff
//#else
    in-conditional ELSE stuff
//#endif
...about to exit
//+:cnd
After Conditional processing = off
    After conditional ELSE(true) stuff
Final stuff";

            VariableCollection vc = new VariableCollection();
            IProcessor partialProcessor = new ConditionalTests(_environmentSettingsHelper).SetupCStyleWithCommentsProcessor(vc);

            string on = "//+:cnd";
            string off = "//-:cnd";
            List<IOperationProvider> flagOperations = new List<IOperationProvider>
            {
                new SetFlag(Conditional.OperationName, on.TokenConfig(), off.TokenConfig(), string.Empty.TokenConfig(), string.Empty.TokenConfig(), null, true)
            };

            IProcessor processor = partialProcessor.CloneAndAppendOperations(flagOperations);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        // Demonstrate that conditional operations processing is correctly enabled & disabled with the noEmit flags.
        [TestMethod]
        public void TurnOffConditionalAndFlagsDontEmitTest()
        {
            string originalValue = @"lead stuff
//#if (true)
    true stuff
//#else
    false stuff
//#endif
Entering Conditional processing = off
//-:cnd:noEmit
...in
//#if (true)
    in-conditional true (IF) stuff
//#else
    in-conditional ELSE stuff
//#endif
...about to exit
//+:cnd:noEmit
After Conditional processing = off
//#if (false)
    After conditional IF(false) stuff
//#else
    After conditional ELSE(true) stuff
//#endif
Final stuff";

            string expectedValue = @"lead stuff
    true stuff
Entering Conditional processing = off
...in
//#if (true)
    in-conditional true (IF) stuff
//#else
    in-conditional ELSE stuff
//#endif
...about to exit
After Conditional processing = off
    After conditional ELSE(true) stuff
Final stuff";

            VariableCollection vc = new VariableCollection();
            IProcessor partialProcessor = new ConditionalTests(_environmentSettingsHelper).SetupCStyleWithCommentsProcessor(vc);

            string on = "//+:cnd";
            string onNoEmit = on + ":noEmit";
            string off = "//-:cnd";
            string offNoEmit = off + ":noEmit";
            List<IOperationProvider> flagOperations = new List<IOperationProvider>
            {
                new SetFlag(Conditional.OperationName, on.TokenConfig(), off.TokenConfig(), onNoEmit.TokenConfig(), offNoEmit.TokenConfig(), null, true)
            };

            IProcessor processor = partialProcessor.CloneAndAppendOperations(flagOperations);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        // Demonstrate that the newlines for the noEmit flags do not get emitted.
        [TestMethod]
        public void ValidateDontEmitFlagDoesntAddNewline()
        {
            string originalValue = @"Start
//-:cnd:noEmit
//+:cnd:noEmit
End";

            string expectedValue = @"Start
End";

            VariableCollection vc = new VariableCollection();
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            EngineConfig engineConfig = new EngineConfig(environmentSettings.Host.Logger, vc);

            string on = "//+:cnd";
            string onNoEmit = on + ":noEmit";
            string off = "//-:cnd";
            string offNoEmit = off + ":noEmit";
            IOperationProvider[] operations =
            {
                new SetFlag(Conditional.OperationName, on.TokenConfig(), off.TokenConfig(), onNoEmit.TokenConfig(), offNoEmit.TokenConfig(), null, true),
            };
            IProcessor processor = Processor.Create(engineConfig, operations);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        // Using //-:cnd:noEmit on the first line of *.cs file corrupts file content when templating new project
        // https://github.com/dotnet/templating/issues/2913
        [TestMethod]
        public void ValidateConditionOnStartOfFileWithBOM()
        {
            string originalValue = @"//-:cnd:noEmit
#if DEBUG
#endif
//+:cnd:noEmit";

            string expectedValue = @"#if DEBUG
#endif
";

            VariableCollection vc = new();
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            EngineConfig engineConfig = new(environmentSettings.Host.Logger, vc);

            ConditionalTokens tokens = new()
            {
                IfTokens = new[] { "#if" }.TokenConfigs()
            };

            string on = "//+:cnd";
            string onNoEmit = on + ":noEmit";
            string off = "//-:cnd";
            string offNoEmit = off + ":noEmit";
            IOperationProvider[] operations =
            {
                new SetFlag(Conditional.OperationName, on.TokenConfig(), off.TokenConfig(), onNoEmit.TokenConfig(), offNoEmit.TokenConfig(), null, true),
                new Conditional(tokens, true, true, Expressions.Cpp.CppStyleEvaluatorDefinition.Evaluate, null, true)
            };
            IProcessor processor = Processor.Create(engineConfig, operations);
            RunAndVerify(originalValue, expectedValue, processor, 9999, emitBOM: true);
        }
    }
}
