// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Tools.Workspaces;
using Xunit.v3;

namespace Microsoft.CodeAnalysis.Tools.Tests.XUnit
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class MSBuildFactAttribute : ConditionalFactAttribute, IBeforeAfterTestAttribute
    {
        public MSBuildFactAttribute(params Type[] skipConditions)
            : base(skipConditions)
        {
        }

        public MSBuildFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
            : base(Array.Empty<Type>(), sourceFilePath, sourceLineNumber)
        {
        }

        public void Before(MethodInfo methodUnderTest, IXunitTest test)
        {
            MSBuildWorkspaceLoader.Guard.Wait();
        }

        public void After(MethodInfo methodUnderTest, IXunitTest test)
        {
            MSBuildWorkspaceLoader.Guard.Release();
        }
    }
}
