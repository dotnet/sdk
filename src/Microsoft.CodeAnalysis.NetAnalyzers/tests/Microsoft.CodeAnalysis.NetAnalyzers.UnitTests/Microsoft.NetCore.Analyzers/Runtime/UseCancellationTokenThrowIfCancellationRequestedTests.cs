// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseCancellationTokenThrowIfCancellationRequested,
    Microsoft.NetCore.Analyzers.Runtime.UseCancellationTokenThrowIfCancellationRequestedFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseCancellationTokenThrowIfCancellationRequested,
    Microsoft.NetCore.Analyzers.Runtime.UseCancellationTokenThrowIfCancellationRequestedFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseCancellationTokenThrowIfCancellationRequestedTests
    {
    }
}
