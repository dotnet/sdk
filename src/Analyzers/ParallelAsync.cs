// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    public static class ParallelAsync
    {
        public static Task ForEachAsync<T>(this IEnumerable<T> enumerable,
                                           Func<T, CancellationToken, Task> action,
                                           CancellationToken cancellationToken = default)
            => Task.WhenAll(enumerable
                .AsParallel()
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .WithCancellation(cancellationToken)
                .Select(x => action(x, cancellationToken)));
    }
}
