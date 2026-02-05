// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Sdk;

namespace Microsoft.CodeAnalysis.Tools.Tests.XUnit
{

    public sealed class MSBuildTheoryDiscoverer : IXunitTestCaseDiscoverer
    {
        private readonly TheoryDiscoverer _theoryDiscoverer;

        public MSBuildTheoryDiscoverer(IMessageSink diagnosticMessageSink)
        {
            _theoryDiscoverer = new TheoryDiscoverer(diagnosticMessageSink);
        }

        public IEnumerable<IXunitTestCase> Discover(
            ITestFrameworkDiscoveryOptions discoveryOptions,
            ITestMethod testMethod,
            IAttributeInfo factAttribute)
        {
            return _theoryDiscoverer
                .Discover(discoveryOptions, testMethod, factAttribute)
                .Select(testCase => new MSBuildTestCase(testCase));
        }
    }
}
