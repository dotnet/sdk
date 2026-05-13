// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Xunit.Sdk;

#nullable enable

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
