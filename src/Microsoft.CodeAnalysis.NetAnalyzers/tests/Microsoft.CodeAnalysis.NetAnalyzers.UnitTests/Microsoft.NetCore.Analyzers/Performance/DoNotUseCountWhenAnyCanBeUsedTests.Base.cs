// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public abstract partial class DoNotUseCountWhenAnyCanBeUsedTestsBase
    {
        protected DoNotUseCountWhenAnyCanBeUsedTestsBase(
            TestsSourceCodeProvider sourceProvider,
            VerifierBase verifier)
        {
            SourceProvider = sourceProvider;
            Verifier = verifier;
        }

        protected TestsSourceCodeProvider SourceProvider { get; }
        protected VerifierBase Verifier { get; }

        protected Task VerifyAsync(string testSource, string extensionsSource)
                => Verifier.VerifyAsync(new string[] { testSource, extensionsSource });

        protected Task VerifyAsync(string methodName, string testSource, string fixedSource, string extensionsSource)
            => Verifier.VerifyAsync(
                methodName,
                new string[] { testSource, extensionsSource },
                new string[] { fixedSource, extensionsSource },
                line: VerifierBase.GetNumberOfLines(testSource) - 3,
                column: 21);

        protected Task VerifyAsync(string methodName, string testSource, string fixedSource, string extensionsSource, int line, int column)
            => Verifier.VerifyAsync(
                    methodName,
                    new string[] { testSource, extensionsSource },
                    new string[] { fixedSource, extensionsSource },
                    line, column);
    }
}
