// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Sdk;

namespace Microsoft.CodeAnalysis.Tools.Tests.XUnit
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Microsoft.CodeAnalysis.Tools.Tests.XUnit.MSBuildTheoryDiscoverer", "dotnet-format.UnitTests")]
    public sealed class MSBuildTheoryAttribute : ConditionalTheoryAttribute
    {
        public MSBuildTheoryAttribute(params Type[] skipConditions)
            : base(skipConditions)
        {
        }
    }
}
