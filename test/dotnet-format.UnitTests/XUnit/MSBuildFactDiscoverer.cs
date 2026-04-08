// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Sdk;

namespace Microsoft.CodeAnalysis.Tools.Tests.XUnit
{

    public sealed class MSBuildFactDiscoverer : IXunitTestCaseDiscoverer
    {
        private readonly FactDiscoverer _factDiscoverer;

        public MSBuildFactDiscoverer(IMessageSink diagnosticMessageSink)
        {
            _factDiscoverer = new FactDiscoverer(diagnosticMessageSink);
        }

        public IEnumerable<IXunitTestCase> Discover(
            ITestFrameworkDiscoveryOptions discoveryOptions,
            ITestMethod testMethod,
            IAttributeInfo factAttribute)
        {
            return _factDiscoverer
                .Discover(discoveryOptions, testMethod, factAttribute)
                .Select(testCase => new MSBuildTestCase(testCase));
        }
    }
}
