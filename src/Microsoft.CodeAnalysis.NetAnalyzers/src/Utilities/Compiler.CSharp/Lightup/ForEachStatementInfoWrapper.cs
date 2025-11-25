// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Analyzer.Utilities.Lightup
{
    internal static class ForEachStatementInfoWrapper
    {
        private static Func<ForEachStatementInfo, AwaitExpressionInfo>? s_MoveNextAwaitableInfoAccessor;
        private static Func<ForEachStatementInfo, AwaitExpressionInfo>? s_DisposeAwaitableInfoAccessor;

        extension(ForEachStatementInfo info)
        {
            public AwaitExpressionInfo MoveNextAwaitableInfo
            {
                get
                {
                    LazyInitializer.EnsureInitialized(ref s_MoveNextAwaitableInfoAccessor, () =>
                    {
                        return LightupHelpers.CreatePropertyAccessor<ForEachStatementInfo, AwaitExpressionInfo>(
                            typeof(ForEachStatementInfo),
                            "info",
                            "MoveNextAwaitableInfo",
                            fallbackResult: default);
                    });

                    RoslynDebug.Assert(s_MoveNextAwaitableInfoAccessor is not null);
                    return s_MoveNextAwaitableInfoAccessor(info);
                }
            }

            public AwaitExpressionInfo DisposeAwaitableInfo
            {
                get
                {
                    LazyInitializer.EnsureInitialized(ref s_DisposeAwaitableInfoAccessor, () =>
                    {
                        return LightupHelpers.CreatePropertyAccessor<ForEachStatementInfo, AwaitExpressionInfo>(
                            typeof(ForEachStatementInfo),
                            "info",
                            "DisposeAwaitableInfo",
                            fallbackResult: default);
                    });

                    RoslynDebug.Assert(s_DisposeAwaitableInfoAccessor is not null);
                    return s_DisposeAwaitableInfoAccessor(info);
                }
            }
        }
    }
}
