// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
