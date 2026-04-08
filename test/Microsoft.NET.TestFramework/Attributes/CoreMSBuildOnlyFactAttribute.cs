// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.TestFramework
{
    public class CoreMSBuildOnlyFactAttribute : FactAttribute
    {
        public CoreMSBuildOnlyFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (SdkTestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild)
            {
                Skip = "This test requires Core MSBuild to run";
            }
        }
    }
}
