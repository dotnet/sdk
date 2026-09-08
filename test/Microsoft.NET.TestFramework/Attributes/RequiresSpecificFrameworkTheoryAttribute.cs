// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETCOREAPP

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.NET.TestFramework
{
    public class RequiresSpecificFrameworkTheoryAttribute : TheoryAttribute
    {
        public RequiresSpecificFrameworkTheoryAttribute(string framework, [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(framework))
            {
                Skip = $"This test requires a shared framework that isn't present: {framework}";
            }
        }
    }
}

#endif
