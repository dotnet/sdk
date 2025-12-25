// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
