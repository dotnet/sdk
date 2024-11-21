// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.CodeAnalysis.Tools.Workspaces;
using Xunit.Sdk;

namespace Microsoft.CodeAnalysis.Tools.Tests.XUnit
{
    [DebuggerDisplay(@"\{ class = {TestMethod.TestClass.Class.Name}, method = {TestMethod.Method.Name}, display = {DisplayName}, skip = {SkipReason} \}")]
    public sealed class MSBuildTestCase : Xunit.LongLivedMarshalByRefObject, IXunitTestCase
    {
        private IXunitTestCase _testCase;

        public string DisplayName => _testCase.DisplayName;
        public IMethodInfo Method => _testCase.Method;
        public string SkipReason => _testCase.SkipReason;
        public ITestMethod TestMethod => _testCase.TestMethod;
        public object[] TestMethodArguments => _testCase.TestMethodArguments;
        public Dictionary<string, List<string>> Traits => _testCase.Traits;
        public string UniqueID => _testCase.UniqueID;

        public ISourceInformation SourceInformation
        {
            get => _testCase.SourceInformation;
            set => _testCase.SourceInformation = value;
        }

        public Exception InitializationException => _testCase.InitializationException;

        public int Timeout => _testCase.Timeout;

        public MSBuildTestCase(IXunitTestCase testCase)
        {
            _testCase = testCase ?? throw new ArgumentNullException(nameof(testCase));
        }

        [Obsolete("Called by the deserializer", error: true)]
        public MSBuildTestCase() { }

        public async Task<RunSummary> RunAsync(
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            object[] constructorArguments,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
        {
            await MSBuildWorkspaceLoader.Guard.WaitAsync();
            try
            {
                var runner = new XunitTestCaseRunner(this, DisplayName, SkipReason, constructorArguments, TestMethodArguments, messageBus, aggregator, cancellationTokenSource);
                return await runner.RunAsync();
            }
            finally
            {
                MSBuildWorkspaceLoader.Guard.Release();
            }
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            _testCase = info.GetValue<IXunitTestCase>("InnerTestCase");
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("InnerTestCase", _testCase);
        }
    }
}
