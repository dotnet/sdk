// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.TestFramework
{
    public class RequiresMSBuildVersionFactAttribute : FactAttribute
    {
        /// <summary>
        /// Can be used to document the reason a test needs a specific version of MSBuild
        /// </summary>
        public string? Reason { get; set; }

        public RequiresMSBuildVersionFactAttribute(string version, [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
            : base(sourceFilePath, sourceLineNumber)
        {
            RequiresMSBuildVersionTheoryAttribute.CheckForRequiredMSBuildVersion(this, version);
        }
    }
}
