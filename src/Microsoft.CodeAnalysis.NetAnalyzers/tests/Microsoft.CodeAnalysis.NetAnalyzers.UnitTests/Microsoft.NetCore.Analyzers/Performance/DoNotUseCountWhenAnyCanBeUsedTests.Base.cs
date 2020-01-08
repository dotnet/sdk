// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public abstract partial class DoNotUseCountWhenAnyCanBeUsedTestsBase
    {
        protected DoNotUseCountWhenAnyCanBeUsedTestsBase(
            TestsSourceCodeProvider sourceProvider,
            VerifierBase verifier,
            ITestOutputHelper output)
        {
            SourceProvider = sourceProvider;
            Verifier = verifier;
            Output = output;
        }

        protected TestsSourceCodeProvider SourceProvider { get; }
        protected VerifierBase Verifier { get; }
        public ITestOutputHelper Output { get; }

        protected async Task VerifyAsync(string testSource, string extensionsSource)
        {
            try
            {
                await this.Verifier.VerifyAsync(new string[] { testSource, extensionsSource });
            }
            catch
            {
                this.Output.WriteLine($"{SourceProvider.CommentPrefix} Source code:{Environment.NewLine}{testSource}");

                if (!string.IsNullOrEmpty(extensionsSource))
                {
                    this.Output.WriteLine($"{SourceProvider.CommentPrefix} Extensions code:{Environment.NewLine}{extensionsSource}");
                }

                throw;
            }
        }

        protected async Task VerifyAsync(string methodName, string testSource, string fixedSource, string extensionsSource)
        {
            try
            {
                await this.Verifier.VerifyAsync(
                    methodName,
                    new string[] { testSource, extensionsSource },
                    new string[] { fixedSource, extensionsSource });
            }
            catch
            {
                this.Output.WriteLine($"{SourceProvider.CommentPrefix} Source code:{Environment.NewLine}{testSource}");

                this.Output.WriteLine($"{SourceProvider.CommentPrefix} Fixed code for:{Environment.NewLine}{fixedSource}");

                if (!string.IsNullOrEmpty(extensionsSource))
                {
                    this.Output.WriteLine($"{SourceProvider.CommentPrefix} Extensions code:{Environment.NewLine}{extensionsSource}");
                }

                throw;
            }
        }

    }
}
