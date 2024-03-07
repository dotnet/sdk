// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Xunit.Sdk;

#nullable enable

namespace Microsoft.CodeAnalysis.Tools.Tests.XUnit
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Microsoft.CodeAnalysis.Tools.Tests.XUnit.MSBuildFactDiscoverer", "dotnet-format.UnitTests")]
    public sealed class MSBuildFactAttribute : ConditionalFactAttribute
    {
        public MSBuildFactAttribute(params Type[] skipConditions)
            : base(skipConditions)
        {
        }
    }
}
