// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Analyzer.Utilities.Lightup
{
    internal static class AwaitExpressionInfoWrapper
    {
        private static Func<AwaitExpressionInfo, IMethodSymbol?>? s_RuntimeAwaitMethodAccessor;

        extension(AwaitExpressionInfo info)
        {
            public IMethodSymbol? RuntimeAwaitMethod
            {
                get
                {
                    LazyInitializer.EnsureInitialized(ref s_RuntimeAwaitMethodAccessor, () =>
                    {
                        return LightupHelpers.CreatePropertyAccessor<AwaitExpressionInfo, IMethodSymbol?>(
                            typeof(AwaitExpressionInfo),
                            "info",
                            "RuntimeAwaitMethod",
                            fallbackResult: null);
                    });

                    RoslynDebug.Assert(s_RuntimeAwaitMethodAccessor is not null);
                    return s_RuntimeAwaitMethodAccessor(info);
                }
            }
        }
    }
}
